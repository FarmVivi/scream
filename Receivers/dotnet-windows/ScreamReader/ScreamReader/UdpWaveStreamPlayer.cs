using NAudio.Wave;
using NAudio.CoreAudioApi;      // For MMDeviceEnumerator & IMMNotificationClient
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.CoreAudioApi.Interfaces;

namespace ScreamReader
{
    internal abstract class UdpWaveStreamPlayer : IDisposable, IMMNotificationClient
    {
        #region instance variables
        private const int ScreamHeaderSize = 5;

        private readonly Semaphore startLock;
        private readonly Semaphore shutdownLock;
        private CancellationTokenSource cancellationTokenSource;
        private UdpClient udpClient;
        private WasapiOut output;
        private IWaveProvider currentWaveProvider;
        private readonly object outputLock = new object();
        private BufferedWaveProvider bufferedWaveProvider;

        private MMDeviceEnumerator deviceEnumerator; // For default device change notifications

        // Fields to store constructor parameters
        protected int BitWidth { get; set; }
        protected int SampleRate { get; set; }
        protected int ChannelCount { get; set; }
        protected bool IsAutoDetectFormat { get; set; } = true; // true = auto-detect format from stream
        protected int BufferDuration { get; set; } = -1; // -1 means auto-detect
        protected int WasapiLatency { get; set; } = -1; // -1 means auto-detect
        protected bool UseExclusiveMode { get; set; } = false;

        // Adaptive buffer management (persistant entre les sessions)
        private AdaptiveBufferManager bufferManager;
        
        // Pre-buffering and buffer monitoring
        private bool isWasapiPlaying = false;
        private bool hasEverPlayedAudio = false;
        private DateTime lastPlaybackDecisionLog = DateTime.MinValue;
        private const int PRE_BUFFER_THRESHOLD_PERCENT = 40;  // Remplir à 40% avant de démarrer
        private const int LOW_BUFFER_THRESHOLD_PERCENT = 15;  // Pause si descend sous 15%
        private const int RESUME_BUFFER_THRESHOLD_PERCENT = 40; // Reprendre à 40%
        private const double PRE_BUFFER_THRESHOLD_MS = 20.0;   // Alternative absolue pour démarrer
        private const double LOW_BUFFER_THRESHOLD_MS = 6.0;     // Pause si inférieur à 6ms bufferisés
        private const double RESUME_BUFFER_THRESHOLD_MS = 18.0; // Reprendre dès 18ms disponibles
        
        // Statistics tracking
        private StreamStats currentStats;
        private readonly object statsLock = new object();
        private DateTime connectionStartTime;
        private long packetCount = 0;
        private long byteCount = 0;
        private double packetsPerSecondSmoothed = 0;
        private double bytesPerSecondSmoothed = 0;
        #endregion

        #region Helper types
        private sealed class StreamFormatState
        {
            public byte RateCode { get; private set; }
            public byte BitDepth { get; private set; }
            public byte ChannelCount { get; private set; }
            public byte ChannelMapLsb { get; private set; }
            public byte ChannelMapMsb { get; private set; }
            public int SampleRate { get; private set; }

            private StreamFormatState() { }

            public static StreamFormatState CreateForAutoDetect() => new StreamFormatState
            {
                RateCode = byte.MaxValue,
                BitDepth = 0,
                ChannelCount = 0,
                ChannelMapLsb = 0xFF,
                ChannelMapMsb = 0xFF,
                SampleRate = 0
            };

            public static StreamFormatState CreateForManual(int sampleRate, int bitWidth, int channelCount)
            {
                return new StreamFormatState
                {
                    RateCode = EncodeRateCode(sampleRate),
                    BitDepth = (byte)Math.Max(bitWidth, 1),
                    ChannelCount = (byte)Math.Max(channelCount, 1),
                    ChannelMapLsb = channelCount == 2 ? (byte)0x03 : (byte)0x01,
                    ChannelMapMsb = 0x00,
                    SampleRate = Math.Max(sampleRate, 8000)
                };
            }

            public bool UpdateFromHeader(byte[] header)
            {
                if (header == null || header.Length < ScreamHeaderSize)
                {
                    return false;
                }

                bool changed = header[0] != RateCode || header[1] != BitDepth || header[2] != ChannelCount ||
                               header[3] != ChannelMapLsb || header[4] != ChannelMapMsb;

                if (!changed)
                {
                    return false;
                }

                RateCode = header[0];
                BitDepth = (byte)Math.Max(header[1], (byte)16);
                ChannelCount = (byte)Math.Max(header[2], (byte)1);
                ChannelMapLsb = header[3];
                ChannelMapMsb = header[4];
                SampleRate = DecodeSampleRate(header[0]);

                return true;
            }

            public WaveFormat BuildWaveFormat()
            {
                int rate = SampleRate > 0 ? SampleRate : 48000;
                int depth = BitDepth > 0 ? BitDepth : 16;
                int channels = ChannelCount > 0 ? ChannelCount : 2;
                return new WaveFormat(rate, depth, channels);
            }

            public string Describe() => $"{SampleRate}Hz, {BitDepth}bit, {ChannelCount}ch";

            private static byte EncodeRateCode(int sampleRate)
            {
                int baseRate = sampleRate == 44100 ? 128 : 0;
                int divisor = baseRate == 128 ? 44100 : 48000;
                int multiplier = Math.Max(sampleRate / divisor, 1);
                return (byte)(baseRate + multiplier);
            }

            private static int DecodeSampleRate(byte rateCode)
            {
                int baseRate = rateCode >= 128 ? 44100 : 48000;
                int multiplier = rateCode % 128;
                if (multiplier <= 0)
                {
                    multiplier = 1;
                }
                return baseRate * multiplier;
            }
        }

        private sealed class ReceiveLoopContext
        {
            public BufferedWaveProvider BufferProvider;
            public bool OutputInitialized;
            public bool HasDetectedFormat;
            public int PacketsBeforeInit;
            public int LocalPacketCount;
            public DateTime LastLogTime;
            public DateTime LastStatsLogTime;
            public DateTime LastStatsUpdateTime;
            public StreamFormatState FormatState;
            public TimeSpan BufferDuration;
        }
        #endregion

        #region public properties
        // Volume control removed - use system volume instead
        #endregion

        #region constructors
        /// <summary>
        /// Default constructor. Initializes common resources.
        /// </summary>
        public UdpWaveStreamPlayer()
        {
            this.startLock = new Semaphore(1, 1);
            this.shutdownLock = new Semaphore(0, 1);

            // Initialize stats
            this.currentStats = new StreamStats();

            // Prepare the UdpClient
            this.udpClient = new UdpClient
            {
                ExclusiveAddressUse = false
            };
            
            // Set receive timeout to detect if no data is coming
            this.udpClient.Client.ReceiveTimeout = 10000; // 10 seconds timeout

            // Set up device enumerator for default device change notifications
            try
            {
                this.deviceEnumerator = new MMDeviceEnumerator();
                this.deviceEnumerator.RegisterEndpointNotificationCallback(this);
            }
            catch (Exception ex)
            {
                LogManager.Log($"[UdpWaveStreamPlayer] Failed to register device notifications: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets the current stream statistics (thread-safe)
        /// </summary>
        public StreamStats GetCurrentStats()
        {
            lock (statsLock)
            {
                return currentStats?.Clone();
            }
        }

        /// <summary>
        /// Overloaded constructor for user-specified audio format.
        /// </summary>
        public UdpWaveStreamPlayer(int bitWidth, int rate, int channels)
            : this()
        {
            this.BitWidth = bitWidth;
            this.SampleRate = rate;
            this.ChannelCount = channels;
            // Ensure Shared mode by default
            this.UseExclusiveMode = false;
        }

        /// <summary>
        /// Constructor with audio optimization parameters.
        /// </summary>
        public UdpWaveStreamPlayer(int bitWidth, int rate, int channels, int bufferDuration, int wasapiLatency, bool useExclusiveMode, bool isAutoDetectFormat = true)
            : this()
        {
            this.BitWidth = bitWidth;
            this.SampleRate = rate;
            this.ChannelCount = channels;
            this.BufferDuration = bufferDuration;
            this.WasapiLatency = wasapiLatency;
            this.UseExclusiveMode = useExclusiveMode;
            this.IsAutoDetectFormat = isAutoDetectFormat;
        }
        #endregion

        #region Public Methods for Configuration
        
        /// <summary>
        /// Obtient la durée actuelle du buffer réseau (adaptée ou manuelle)
        /// </summary>
        public int GetCurrentBufferDuration()
        {
            return this.bufferManager != null ? (int)this.bufferManager.CurrentBufferDurationMs : this.BufferDuration;
        }

        /// <summary>
        /// Obtient la latence WASAPI actuelle (adaptée ou manuelle)
        /// </summary>
        public int GetCurrentWasapiLatency()
        {
            return this.bufferManager != null ? (int)this.bufferManager.CurrentWasapiLatencyMs : this.WasapiLatency;
        }

        /// <summary>
        /// Met à jour les paramètres manuels du buffer et WASAPI
        /// Utilisé quand l'utilisateur change les valeurs en mode manuel
        /// </summary>
        public void UpdateManualSettings(int bufferDuration, int wasapiLatency)
        {
            if (bufferDuration > 0)
            {
                this.BufferDuration = bufferDuration;
                LogManager.LogDebug($"[UdpWaveStreamPlayer] BufferDuration manuel mis à jour: {bufferDuration}ms");
            }
            
            if (wasapiLatency > 0)
            {
                this.WasapiLatency = wasapiLatency;
                LogManager.LogDebug($"[UdpWaveStreamPlayer] WasapiLatency manuel mis à jour: {wasapiLatency}ms");
            }
            
            // Forcer la mise à jour du bufferManager avec les nouvelles valeurs
            if (this.bufferManager != null && (bufferDuration > 0 || wasapiLatency > 0))
            {
                // Recréer le bufferManager avec les nouvelles valeurs manuelles
                this.bufferManager = new AdaptiveBufferManager(
                    bufferDuration > 0 ? bufferDuration : (int)this.bufferManager.CurrentBufferDurationMs,
                    wasapiLatency > 0 ? wasapiLatency : (int)this.bufferManager.CurrentWasapiLatencyMs,
                    this.UseExclusiveMode,
                    this.BitWidth,
                    this.SampleRate
                );
                
                LogManager.LogInfo($"[UdpWaveStreamPlayer] BufferManager recréé avec valeurs manuelles: Buffer={this.bufferManager.CurrentBufferDurationMs}ms, WASAPI={this.bufferManager.CurrentWasapiLatencyMs}ms");
            }
        }
        
        #endregion

        /// <summary>
        /// Abstract method to let derived classes configure the UdpClient (bind, join group, etc.).
        /// </summary>
        protected abstract void ConfigureUdpClient(UdpClient udpClient, IPEndPoint localEp);

        /// <summary>
        /// Start reading from the UDP stream and playing audio.
        /// </summary>
        public virtual void Start()
        {
            LogManager.Log("[UdpWaveStreamPlayer] Starting UDP audio stream...");

            this.startLock.WaitOne();
            PrepareBufferManager();

            this.cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => RunReceiveLoop(), this.cancellationTokenSource.Token);
        }

        private void PrepareBufferManager()
        {
            if (this.bufferManager == null)
            {
                this.bufferManager = new AdaptiveBufferManager(
                    this.BufferDuration,
                    this.WasapiLatency,
                    this.UseExclusiveMode,
                    this.BitWidth,
                    this.SampleRate);
                LogManager.Log("[UdpWaveStreamPlayer] AdaptiveBufferManager créé");
            }
            else
            {
                this.bufferManager.ApplyRecommendedWasapiLatency();
                LogManager.Log($"[UdpWaveStreamPlayer] AdaptiveBufferManager réutilisé - Buffer cible: {bufferManager.CurrentBufferDurationMs}ms, WASAPI: {bufferManager.CurrentWasapiLatencyMs}ms");
            }
        }

        private void RunReceiveLoop()
        {
            try
            {
                Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
                EnsureUdpClientReady();

                IPEndPoint remoteEndpoint = null;
                LogManager.Log("[UdpWaveStreamPlayer] Configuring UDP client...");
                ConfigureUdpClient(this.udpClient, remoteEndpoint);
                var localEndpointInfo = this.udpClient?.Client?.LocalEndPoint;
                LogManager.Log($"[UdpWaveStreamPlayer] UDP client configured, listening on {localEndpointInfo}");

                var context = CreateReceiveLoopContext();
                InitializeSessionStats(context);

                LogManager.Log("[UdpWaveStreamPlayer] Starting UDP receive loop...");
                LogManager.Log("[UdpWaveStreamPlayer] Waiting for audio data...");

                while (!this.cancellationTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        byte[] data = this.udpClient.Receive(ref remoteEndpoint);
                        context.LocalPacketCount++;
                        HandleReceivedPacket(data, ref remoteEndpoint, context);
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode == SocketError.TimedOut)
                        {
                            continue;
                        }

                        LogManager.Log($"[UdpWaveStreamPlayer] Socket error: {ex.SocketErrorCode} - {ex.Message}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogManager.Log($"[UdpWaveStreamPlayer] Error: {ex.Message}");
                        LogManager.Log($"[UdpWaveStreamPlayer] Stack trace: {ex.StackTrace}");
                        MessageBox.Show(ex.StackTrace, ex.Message);
                    }
                }

                this.shutdownLock.WaitOne();
                CleanupAfterReceiveLoop();
            }
            finally
            {
                this.startLock.Release();
            }
        }

        private void EnsureUdpClientReady()
        {
            if (this.udpClient == null)
            {
                this.udpClient = new UdpClient
                {
                    ExclusiveAddressUse = false
                };
                LogManager.LogDebug("[UdpWaveStreamPlayer] UDP client recréé après Stop()");
            }

            this.udpClient.ExclusiveAddressUse = false;
            this.udpClient.Client.ReceiveTimeout = 10000;
        }

        private ReceiveLoopContext CreateReceiveLoopContext()
        {
            var context = new ReceiveLoopContext
            {
                FormatState = this.IsAutoDetectFormat
                    ? StreamFormatState.CreateForAutoDetect()
                    : StreamFormatState.CreateForManual(this.SampleRate, this.BitWidth, this.ChannelCount),
                BufferDuration = TimeSpan.FromMilliseconds(this.bufferManager.CurrentBufferDurationMs),
                PacketsBeforeInit = this.IsAutoDetectFormat ? 3 : 0,
                LastLogTime = DateTime.Now,
                LastStatsLogTime = DateTime.Now,
                LastStatsUpdateTime = DateTime.Now,
                HasDetectedFormat = !this.IsAutoDetectFormat
            };

            if (this.IsAutoDetectFormat)
            {
                LogManager.Log($"[UdpWaveStreamPlayer] Mode auto-détection: Waiting for {context.PacketsBeforeInit} packets to detect format...");
            }
            else
            {
                LogManager.Log($"[UdpWaveStreamPlayer] Mode manuel: Using format {context.FormatState.Describe()}");
                context.BufferProvider = CreateBufferedWaveProvider(context.FormatState, context.BufferDuration);
                this.bufferedWaveProvider = context.BufferProvider;
                LogManager.Log("[UdpWaveStreamPlayer] Buffer created with manual format, WASAPI will start on first packet");
            }

            return context;
        }

        private void InitializeSessionStats(ReceiveLoopContext context)
        {
            connectionStartTime = DateTime.Now;
            context.LastStatsUpdateTime = DateTime.Now;
            this.packetCount = 0;
            this.byteCount = 0;
            this.packetsPerSecondSmoothed = 0;
            this.bytesPerSecondSmoothed = 0;

            lock (statsLock)
            {
                currentStats.IsConnected = false;
                currentStats.ConnectionTime = connectionStartTime;
                currentStats.SampleRate = this.IsAutoDetectFormat ? this.SampleRate : context.FormatState.SampleRate;
                currentStats.BitDepth = this.IsAutoDetectFormat ? this.BitWidth : context.FormatState.BitDepth;
                currentStats.Channels = this.IsAutoDetectFormat ? this.ChannelCount : context.FormatState.ChannelCount;
                currentStats.NetworkBufferedMs = 0;
                currentStats.NetworkBufferCapacityMs = context.BufferDuration.TotalMilliseconds;
            }
        }

        private void HandleReceivedPacket(byte[] data, ref IPEndPoint remoteEndpoint, ReceiveLoopContext context)
        {
            UpdateConnectionStats(data.Length, remoteEndpoint, context);
            LogPacketActivity(data, remoteEndpoint, context);

            if (data.Length < ScreamHeaderSize)
            {
                return;
            }

            bool formatChanged = TryHandleFormatChange(data, context);

            if (context.BufferProvider == null)
            {
                return;
            }

            MaybeInitializeOutput(context, formatChanged);
            BufferAudioData(data, context);

            var (bufferedMs, bufferCapacityMs) = UpdateBufferStatistics(context);
            UpdatePlaybackState(bufferedMs, bufferCapacityMs);

            if (bufferCapacityMs > 0 && context.LocalPacketCount % 25 == 0)
            {
                RecordAdaptiveMeasurement(context, bufferedMs, bufferCapacityMs);
            }

            LogPeriodicStatistics(context);
        }

        private void UpdateConnectionStats(int dataLength, IPEndPoint remoteEndpoint, ReceiveLoopContext context)
        {
            if (remoteEndpoint != null && context.LocalPacketCount == 1)
            {
                lock (statsLock)
                {
                    currentStats.IsConnected = true;
                    currentStats.RemoteEndpoint = remoteEndpoint.ToString();
                }
                LogManager.Log($"[UdpWaveStreamPlayer] Connected to {remoteEndpoint}");
            }
            else if (remoteEndpoint != null)
            {
                lock (statsLock)
                {
                    if (!currentStats.IsConnected)
                    {
                        currentStats.IsConnected = true;
                        currentStats.RemoteEndpoint = remoteEndpoint.ToString();
                        LogManager.LogInfo($"✓ Connecté à {remoteEndpoint}");
                    }
                }
            }

            this.packetCount++;
            this.byteCount += dataLength;

            lock (statsLock)
            {
                currentStats.TotalPacketsReceived = this.packetCount;
                currentStats.TotalBytesReceived = this.byteCount;

                var now = DateTime.Now;
                var elapsed = (now - context.LastStatsUpdateTime).TotalSeconds;

                if (elapsed >= 0.1)
                {
                    var instantPacketsPerSec = 1.0 / elapsed;
                    var instantBytesPerSec = dataLength / elapsed;

                    packetsPerSecondSmoothed = packetsPerSecondSmoothed * 0.8 + instantPacketsPerSec * 0.2;
                    bytesPerSecondSmoothed = bytesPerSecondSmoothed * 0.8 + instantBytesPerSec * 0.2;

                    currentStats.PacketsPerSecond = packetsPerSecondSmoothed;
                    currentStats.BytesPerSecond = bytesPerSecondSmoothed;

                    context.LastStatsUpdateTime = now;
                }
            }
        }

        private void LogPacketActivity(byte[] data, IPEndPoint remoteEndpoint, ReceiveLoopContext context)
        {
            if (context.LocalPacketCount <= 3)
            {
                LogManager.LogDebug($"[UdpWaveStreamPlayer] Received packet #{context.LocalPacketCount}: {data.Length} bytes from {remoteEndpoint}");
                if (data.Length >= ScreamHeaderSize)
                {
                    LogManager.LogInfo($"[UdpWaveStreamPlayer] Header: rate={data[0]}, width={data[1]}, channels={data[2]}, map_lsb={data[3]}, map_msb={data[4]}");
                }
            }
            else if (context.LocalPacketCount % 200 == 0)
            {
                var elapsed = DateTime.Now - context.LastLogTime;
                var packetsPerSecond = 200.0 / Math.Max(elapsed.TotalSeconds, 0.001);
                LogManager.LogDebug($"[UdpWaveStreamPlayer] Received {context.LocalPacketCount} packets so far... ({packetsPerSecond:F1} packets/sec)");
                context.LastLogTime = DateTime.Now;
            }
        }

        private bool TryHandleFormatChange(byte[] data, ReceiveLoopContext context)
        {
            bool headerChanged = context.FormatState.UpdateFromHeader(data);

            if (!context.HasDetectedFormat)
            {
                if (headerChanged)
                {
                    OnFormatDetected(context, true);
                    context.HasDetectedFormat = true;
                    return true;
                }

                return false;
            }

            if (headerChanged)
            {
                OnFormatDetected(context, false);
                return true;
            }

            return false;
        }

        private void OnFormatDetected(ReceiveLoopContext context, bool isInitialDetection)
        {
            if (isInitialDetection)
            {
                LogManager.Log($"[UdpWaveStreamPlayer] Format detected: {context.FormatState.Describe()}");
                LogManager.LogDebug("[UdpWaveStreamPlayer] Conservation du BufferManager existant (adaptations préservées)");
            }
            else
            {
                LogManager.Log($"[UdpWaveStreamPlayer] Format change detected: {context.FormatState.Describe()}");

                int adaptedBufferDuration = (int)this.bufferManager.CurrentBufferDurationMs;
                int adaptedWasapiLatency = (int)this.bufferManager.CurrentWasapiLatencyMs;

                this.bufferManager = new AdaptiveBufferManager(
                    adaptedBufferDuration,
                    adaptedWasapiLatency,
                    this.UseExclusiveMode,
                    context.FormatState.BitDepth,
                    context.FormatState.SampleRate);

                LogManager.LogDebug($"[UdpWaveStreamPlayer] BufferManager recréé avec valeurs adaptées: Buffer={adaptedBufferDuration}ms, WASAPI={adaptedWasapiLatency}ms");
            }

            context.BufferDuration = TimeSpan.FromMilliseconds(this.bufferManager.CurrentBufferDurationMs);
            context.BufferProvider = CreateBufferedWaveProvider(context.FormatState, context.BufferDuration);
            this.bufferedWaveProvider = context.BufferProvider;

            lock (statsLock)
            {
                currentStats.SampleRate = context.FormatState.SampleRate;
                currentStats.BitDepth = context.FormatState.BitDepth;
                currentStats.Channels = context.FormatState.ChannelCount;
            }
        }

        private BufferedWaveProvider CreateBufferedWaveProvider(StreamFormatState formatState, TimeSpan bufferDuration)
        {
            return new BufferedWaveProvider(formatState.BuildWaveFormat())
            {
                BufferDuration = bufferDuration,
                DiscardOnBufferOverflow = true
            };
        }

        private void MaybeInitializeOutput(ReceiveLoopContext context, bool dueToFormatChange)
        {
            if (context.BufferProvider == null)
            {
                return;
            }

            if (!context.OutputInitialized)
            {
                if (context.LocalPacketCount >= context.PacketsBeforeInit)
                {
                    InitializeOutputDevice(context.BufferProvider);
                    context.OutputInitialized = true;

                    if (dueToFormatChange || this.IsAutoDetectFormat)
                    {
                        LogManager.LogInfo("[UdpWaveStreamPlayer] WASAPI initialized with detected format");
                    }
                    else
                    {
                        LogManager.LogInfo("[UdpWaveStreamPlayer] WASAPI initialized (manual mode)");
                    }
                }
            }
            else if (dueToFormatChange)
            {
                InitializeOutputDevice(context.BufferProvider);
                isWasapiPlaying = false;
                LogManager.LogInfo("[UdpWaveStreamPlayer] WASAPI re-initialized due to format change");
            }
        }

        private void BufferAudioData(byte[] data, ReceiveLoopContext context)
        {
            int audioDataSize = data.Length - ScreamHeaderSize;
            if (audioDataSize <= 0)
            {
                return;
            }

            context.BufferProvider.AddSamples(data, ScreamHeaderSize, audioDataSize);
        }

        private (double bufferedMs, double bufferCapacityMs) UpdateBufferStatistics(ReceiveLoopContext context)
        {
            var bufferedMs = context.BufferProvider.BufferedDuration.TotalMilliseconds;
            var bufferCapacityMs = context.BufferProvider.BufferDuration.TotalMilliseconds;

            if (bufferCapacityMs <= 0)
            {
                bufferCapacityMs = this.bufferManager?.CurrentBufferDurationMs ?? (this.BufferDuration > 0 ? this.BufferDuration : 0);
            }

            context.BufferDuration = TimeSpan.FromMilliseconds(bufferCapacityMs);

            lock (statsLock)
            {
                currentStats.NetworkBufferedMs = bufferedMs;
                currentStats.NetworkBufferCapacityMs = bufferCapacityMs;

                if (bufferManager != null)
                {
                    currentStats.WasapiBufferedMs = bufferManager.WasapiBufferedMs;
                    currentStats.WasapiBufferCapacityMs = bufferManager.WasapiBufferCapacityMs;
                }
            }

            return (bufferedMs, bufferCapacityMs);
        }

        private void RecordAdaptiveMeasurement(ReceiveLoopContext context, double bufferedMs, double bufferCapacityMs)
        {
            if (this.bufferManager == null)
            {
                return;
            }

            bufferManager.RecordMeasurement(bufferedMs, (int)Math.Round(bufferCapacityMs), context.LocalPacketCount);
        }

        private void LogPeriodicStatistics(ReceiveLoopContext context)
        {
            if ((DateTime.Now - context.LastStatsLogTime).TotalSeconds >= 10)
            {
                if (bufferManager != null)
                {
                    LogManager.LogDebug($"[UdpWaveStreamPlayer] Stats: {bufferManager.GetStatistics()}");
                }
                context.LastStatsLogTime = DateTime.Now;
            }
        }

        private void CleanupAfterReceiveLoop()
        {
            lock (outputLock)
            {
                this.output?.Stop();
            }

            this.udpClient?.Close();
            this.udpClient?.Dispose();
            this.udpClient = null;
            this.bufferedWaveProvider = null;
            this.currentWaveProvider = null;
        }

        /// <summary>
        /// Stop reading and playing audio. Subsequent calls to <see cref="Start"/> are allowed.
        /// </summary>
        public void Stop()
        {
            // Analyser les performances long terme avant d'arrêter
            this.bufferManager?.AnalyzeLongTermPerformance();
            
            this.cancellationTokenSource?.Cancel();
            this.shutdownLock.Release();
            
            // Fermer le socket UDP pour libérer le port (mais garder bufferManager)
            try
            {
                this.udpClient?.Close();
                this.udpClient?.Dispose();
                this.udpClient = null;
            }
            catch (Exception ex)
            {
                LogManager.LogDebug($"[UdpWaveStreamPlayer] Erreur fermeture socket: {ex.Message}");
            }
            
            // Arrêter la lecture audio (mais garder output pour réutilisation)
            try
            {
                lock (outputLock)
                {
                    this.output?.Stop();
                    isWasapiPlaying = false;
                    hasEverPlayedAudio = false;
                }
            }
            catch (Exception ex)
            {
                LogManager.LogDebug($"[UdpWaveStreamPlayer] Erreur arrêt output: {ex.Message}");
            }
        }

        #region IMMNotificationClient Implementation

        // Called by Windows whenever the default render device changes
        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            // We only care about rendering device changes for the "Multimedia" role
            // or you might also want to check for "Console" or "Communications"
            if (flow == DataFlow.Render && role == Role.Multimedia)
            {
                LogManager.Log("Default playback device changed in Windows. Re-initializing output.");

                // Re-initialize output device with the current wave provider (if any)
                if (this.currentWaveProvider != null)
                {
                    var waveProvider = this.currentWaveProvider;
                    Task.Run(() => HandleDefaultDeviceChange(waveProvider));
                }
            }
        }

        public void OnDeviceAdded(string pwstrDeviceId) { }
        public void OnDeviceRemoved(string deviceId) { }
        public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }

        #endregion



        /// <summary>
        /// Sets up the WasapiOut device with the given wave provider, disposing the old one if present.
        /// Uses adaptive latency from the buffer manager for optimal performance.
        /// </summary>
        private void InitializeOutputDevice(IWaveProvider waveProvider)
        {
            if (waveProvider == null) return;

            lock (outputLock)
            {
                if (this.output != null)
                {
                    try
                    {
                        this.output.Stop();
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogDebug($"[UdpWaveStreamPlayer] Erreur lors de l'arrêt de l'ancienne sortie: {ex.Message}");
                    }

                    this.output.Dispose();
                    this.output = null;
                }

                // Create a WasapiOut associated with the *current* default device
                using (var mmDeviceEnum = new MMDeviceEnumerator())
                {
                    var device = mmDeviceEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    
                    // Determine share mode
                    var shareMode = this.UseExclusiveMode ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared;
                    
                    // Get adaptive latency from buffer manager
                    int targetLatency = bufferManager?.CurrentWasapiLatencyMs ?? 20;
                    
                    // Try to initialize with adaptive latency, with graceful fallback
                    bool success = false;
                    int[] latencyOptions = new int[] { 
                        targetLatency,                  // Latence optimale adaptative
                        targetLatency + 10,             // Fallback +10ms
                        targetLatency + 20,             // Fallback +20ms
                        50,                             // Fallback sûr
                        100                             // Dernière chance
                    };
                    
                    foreach (int latency in latencyOptions)
                    {
                        try
                        {
                            this.output = new WasapiOut(device, shareMode, false, latency);
                            LogManager.Log($"[UdpWaveStreamPlayer] ✓ Initialized {shareMode} mode with {latency}ms WASAPI latency");
                            
                            // Enregistrer la latence WASAPI réelle qui a fonctionné
                            bufferManager?.SetActualWasapiLatency(latency);
                            
                            success = true;
                            break;
                        }
                        catch (Exception ex)
                        {
                            LogManager.LogDebug($"[UdpWaveStreamPlayer] Failed with {latency}ms: {ex.Message}");
                            this.output?.Dispose();
                            this.output = null;
                        }
                    }
                    
                    if (!success)
                    {
                        throw new InvalidOperationException($"Failed to initialize WasapiOut with {shareMode} mode at any latency");
                    }
                }

                this.currentWaveProvider = waveProvider;
                this.bufferedWaveProvider = waveProvider as BufferedWaveProvider ?? this.bufferedWaveProvider;
                this.output.Init(this.currentWaveProvider);
                
                // NE PAS appeler Play() ici - le pré-buffering le fera automatiquement
                // quand le buffer sera suffisamment rempli
                isWasapiPlaying = false;
                hasEverPlayedAudio = false;
                lastPlaybackDecisionLog = DateTime.MinValue;
                LogManager.Log($"[UdpWaveStreamPlayer] ✓ WASAPI initialized, waiting for pre-buffering ({PRE_BUFFER_THRESHOLD_PERCENT}% of buffer)");
            }
        }

        private void UpdatePlaybackState(double bufferedMs, double bufferCapacityMs)
        {
            if (bufferCapacityMs <= 0)
                return;

            double bufferFillPercent = (bufferedMs / bufferCapacityMs) * 100.0;

            double startMsThreshold = Math.Min(bufferCapacityMs * (PRE_BUFFER_THRESHOLD_PERCENT / 100.0), PRE_BUFFER_THRESHOLD_MS);
            double resumeMsThreshold = Math.Min(bufferCapacityMs * (RESUME_BUFFER_THRESHOLD_PERCENT / 100.0), RESUME_BUFFER_THRESHOLD_MS);

            // Toujours garantir un minimum de 4ms pour éviter un démarrage sur un tampon vide
            startMsThreshold = Math.Max(startMsThreshold, 4.0);
            resumeMsThreshold = Math.Max(resumeMsThreshold, 6.0);

            bool startThresholdMet = (bufferedMs >= startMsThreshold);
            bool resumeThresholdMet = (bufferedMs >= resumeMsThreshold);
            bool hasEnoughToStart = hasEverPlayedAudio ? resumeThresholdMet : startThresholdMet;
            bool shouldPause = (bufferFillPercent < LOW_BUFFER_THRESHOLD_PERCENT) && (bufferedMs < LOW_BUFFER_THRESHOLD_MS);

            lock (outputLock)
            {
                if (this.output == null)
                    return;

                if (!isWasapiPlaying && hasEnoughToStart)
                {
                    try
                    {
                        this.output.Play();
                        isWasapiPlaying = true;
                        hasEverPlayedAudio = true;
                        LogManager.LogDebug($"[UdpWaveStreamPlayer] ▶ Playback started after pre-buffering ({bufferFillPercent:F1}% - {bufferedMs:F1}ms/{bufferCapacityMs:F0}ms)");
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogDebug($"[UdpWaveStreamPlayer] Échec démarrage lecture après pré-chargement: {ex.Message}");
                    }
                }
                else if (isWasapiPlaying && shouldPause)
                {
                    try
                    {
                        this.output.Pause();
                        isWasapiPlaying = false;
                        // Ne pas réinitialiser hasEverPlayedAudio pour permettre un redémarrage rapide
                        LogManager.LogDebug($"[UdpWaveStreamPlayer] ⏸ Buffer trop faible ({bufferFillPercent:F1}% / {bufferedMs:F1}ms), pause pour re-buffering...");
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogDebug($"[UdpWaveStreamPlayer] Échec mise en pause lecture: {ex.Message}");
                    }
                }
                else if (!isWasapiPlaying && bufferedMs > 0)
                {
                    var now = DateTime.Now;
                    if ((now - lastPlaybackDecisionLog).TotalSeconds >= 1.0)
                    {
                        LogManager.LogDebug($"[UdpWaveStreamPlayer] Buffer insuffisant pour démarrer (fill={bufferFillPercent:F1}%, {bufferedMs:F1}ms, start≥{startMsThreshold:F1}ms, resume≥{resumeMsThreshold:F1}ms, everPlayed={hasEverPlayedAudio})");
                        lastPlaybackDecisionLog = now;
                    }
                }
            }
        }

        private void AttemptAutoResumePlayback()
        {
            var buffer = this.bufferedWaveProvider;
            if (buffer == null)
                return;

            var bufferCapacityMs = buffer.BufferDuration.TotalMilliseconds;
            if (bufferCapacityMs <= 0)
                return;

            var bufferedMs = buffer.BufferedDuration.TotalMilliseconds;
            if (bufferedMs <= 0)
            {
                LogManager.LogDebug("[UdpWaveStreamPlayer] Auto-resume skipped: buffer empty après réinitialisation");
                return;
            }

            UpdatePlaybackState(bufferedMs, bufferCapacityMs);
        }

        private void HandleDefaultDeviceChange(IWaveProvider waveProvider)
        {
            const int maxAttempts = 3;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    InitializeOutputDevice(waveProvider);
                    AttemptAutoResumePlayback();
                    return;
                }
                catch (Exception ex)
                {
                    LogManager.Log($"[UdpWaveStreamPlayer] Échec réinitialisation périphérique par défaut (tentative {attempt}/{maxAttempts}): {ex.Message}");

                    if (attempt == maxAttempts)
                    {
                        LogManager.LogError($"[UdpWaveStreamPlayer] Impossible de réinitialiser WASAPI après changement de périphérique: {ex.Message}");
                        return;
                    }

                    Thread.Sleep(200 * attempt);
                }
            }
        }

        #region dispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    // Unregister from device notifications
                    this.deviceEnumerator?.UnregisterEndpointNotificationCallback(this);
                    this.deviceEnumerator?.Dispose();
                }
                catch { /* ignore */ }

                this.startLock.Dispose();
                this.shutdownLock.Dispose();

                // If user calls Dispose without calling Stop, attempt to clean up
                this.cancellationTokenSource?.Cancel();
                this.udpClient?.Close();
                this.udpClient?.Dispose();
                this.output?.Dispose();
            }
        }
        #endregion
    }
}
