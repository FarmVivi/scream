using NAudio.Wave;
using NAudio.CoreAudioApi;      // For MMDeviceEnumerator & IMMNotificationClient
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.CoreAudioApi.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace ScreamReader
{
    internal abstract class UdpWaveStreamPlayer : IDisposable, IMMNotificationClient
    {
        #region instance variables
        private readonly Semaphore startLock;
        private readonly Semaphore shutdownLock;
        private CancellationTokenSource cancellationTokenSource;
        private UdpClient udpClient;
        private WasapiOut output;
        private IWaveProvider currentWaveProvider;

        private MMDeviceEnumerator deviceEnumerator; // For default device change notifications

        private int volume = 50; // Default volume to 50% instead of 0%

        // Fields to store constructor parameters
        protected int BitWidth { get; set; }
        protected int SampleRate { get; set; }
        protected int ChannelCount { get; set; }
        protected int BufferDuration { get; set; } = -1; // -1 means auto-detect
        protected int WasapiLatency { get; set; } = -1; // -1 means auto-detect
        protected bool UseExclusiveMode { get; set; } = false;

        // Adaptive buffer management
        private AdaptiveBufferManager bufferManager;
        
        // Statistics tracking
        private StreamStats currentStats;
        private readonly object statsLock = new object();
        private DateTime connectionStartTime;
        private long packetCount = 0;
        private long byteCount = 0;
        private double packetsPerSecondSmoothed = 0;
        private double bytesPerSecondSmoothed = 0;
        #endregion

        #region public properties
        /// <summary>
        /// Volume property in [0..100].
        /// </summary>
        public int Volume
        {
            get
            {
                if (this.output != null)
                {
                    this.volume = (int)(this.output.Volume * 100);
                }
                LogManager.Log($"get Volume = {this.volume}");
                return this.volume;
            }
            set
            {
                if (value < 0 || value > 100)
                    throw new ArgumentOutOfRangeException(nameof(value));

                this.volume = value;
                if (this.output != null)
                {
                    this.output.Volume = (float)value / 100f;
                    LogManager.Log($"set Volume = {this.volume}");
                }
            }
        }
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

            // Initialize volume with current system volume instead of default
            // We'll set this after the device enumerator is created

            // Set up device enumerator for default device change notifications
            try
            {
                this.deviceEnumerator = new MMDeviceEnumerator();
                this.deviceEnumerator.RegisterEndpointNotificationCallback(this);
                
                // Now initialize volume with current system volume
                this.volume = GetCurrentSystemVolume();
            }
            catch (Exception ex)
            {
                LogManager.Log($"[UdpWaveStreamPlayer] Failed to register device notifications: {ex.Message}");
                // Fallback to default volume if we can't get system volume
                this.volume = 50;
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
        public UdpWaveStreamPlayer(int bitWidth, int rate, int channels, int bufferDuration, int wasapiLatency, bool useExclusiveMode)
            : this()
        {
            this.BitWidth = bitWidth;
            this.SampleRate = rate;
            this.ChannelCount = channels;
            this.BufferDuration = bufferDuration;
            this.WasapiLatency = wasapiLatency;
            this.UseExclusiveMode = useExclusiveMode;
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
            
            // Prevent multiple calls without stopping
            this.startLock.WaitOne();

            // Create a new token for this run
            this.cancellationTokenSource = new CancellationTokenSource();

            // Initialize adaptive buffer manager
            this.bufferManager = new AdaptiveBufferManager(
                this.BufferDuration, 
                this.WasapiLatency, 
                this.UseExclusiveMode,
                this.BitWidth,
                this.SampleRate
            );
            
            // Appliquer les recommandations WASAPI de la session précédente
            this.bufferManager.ApplyRecommendedWasapiLatency();

            // Run in background with high priority
            Task.Run(() =>
            {
                try
                {
                    // Set thread priority to high for better audio performance
                    System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.AboveNormal;
                    IPEndPoint localEp = null;
                    LogManager.Log("[UdpWaveStreamPlayer] Configuring UDP client...");
                    ConfigureUdpClient(this.udpClient, localEp);
                    LogManager.Log($"[UdpWaveStreamPlayer] UDP client configured, listening on {localEp}");

                    // Use the user-specified format initially.
                    byte currentRate = (byte)((this.SampleRate == 44100) ? 129 : 1);
                    byte currentWidth = (byte)this.BitWidth;
                    byte currentChannels = (byte)this.ChannelCount;

                    // Basic channel map bytes: stereo = 0x03, mono = 0x01, etc.
                    byte currentChannelsMapLsb = (this.ChannelCount == 2) ? (byte)0x03 : (byte)0x01;
                    byte currentChannelsMapMsb = 0x00;

                    var bufferDuration = TimeSpan.FromMilliseconds(bufferManager.CurrentBufferDurationMs);
                    var rsws = new BufferedWaveProvider(
                        new WaveFormat(this.SampleRate, this.BitWidth, this.ChannelCount))
                    {
                        BufferDuration = bufferDuration,
                        DiscardOnBufferOverflow = true
                    };
                    LogManager.Log($"[UdpWaveStreamPlayer] Created BufferedWaveProvider with {bufferDuration.TotalMilliseconds}ms buffer");

                    InitializeOutputDevice(rsws);

                    LogManager.Log("[UdpWaveStreamPlayer] Starting UDP receive loop...");
                    LogManager.Log("[UdpWaveStreamPlayer] Waiting for audio data...");
                    
                    int localPacketCount = 0;
                    var lastLogTime = DateTime.Now;
                    var lastStatsLogTime = DateTime.Now;
                    var lastStatsUpdateTime = DateTime.Now;
                    connectionStartTime = DateTime.Now;
                    
                    // Initialize stats
                    lock (statsLock)
                    {
                        currentStats.IsConnected = false;
                        currentStats.ConnectionTime = connectionStartTime;
                        currentStats.SampleRate = this.SampleRate;
                        currentStats.BitDepth = this.BitWidth;
                        currentStats.Channels = this.ChannelCount;
                    }
                    
                    // Start reading loop
                    while (!this.cancellationTokenSource.IsCancellationRequested)
                    {
                        try
                        {
                            byte[] data = this.udpClient.Receive(ref localEp);
                            localPacketCount++;
                            
                            // Update stats
                            lock (statsLock)
                            {
                                if (!currentStats.IsConnected)
                                {
                                    currentStats.IsConnected = true;
                                    currentStats.RemoteEndpoint = localEp.ToString();
                                    LogManager.LogInfo($"✓ Connecté à {localEp}");
                                }

                                this.packetCount++;
                                this.byteCount += data.Length;
                                currentStats.TotalPacketsReceived = this.packetCount;
                                currentStats.TotalBytesReceived = this.byteCount;

                                // Update rate calculations every 100ms
                                var now = DateTime.Now;
                                var elapsed = (now - lastStatsUpdateTime).TotalSeconds;
                                if (elapsed >= 0.1)
                                {
                                    var instantPacketsPerSec = 1.0 / elapsed;
                                    var instantBytesPerSec = data.Length / elapsed;
                                    
                                    // Smooth with exponential moving average
                                    packetsPerSecondSmoothed = packetsPerSecondSmoothed * 0.8 + instantPacketsPerSec * 0.2;
                                    bytesPerSecondSmoothed = bytesPerSecondSmoothed * 0.8 + instantBytesPerSec * 0.2;
                                    
                                    currentStats.PacketsPerSecond = packetsPerSecondSmoothed;
                                    currentStats.BytesPerSecond = bytesPerSecondSmoothed;
                                    
                                    lastStatsUpdateTime = now;
                                }
                            }
                            
                            if (localPacketCount <= 5) // Log first 5 packets in detail
                            {
                                LogManager.LogDebug($"[UdpWaveStreamPlayer] Received packet #{localPacketCount}: {data.Length} bytes from {localEp}");
                                if (data.Length >= 5)
                                {
                                    LogManager.LogInfo($"[UdpWaveStreamPlayer] Header: rate={data[0]}, width={data[1]}, channels={data[2]}, map_lsb={data[3]}, map_msb={data[4]}");
                                }
                            }
                            else if (localPacketCount % 200 == 0) // Log every 200th packet to reduce log spam
                            {
                                var elapsed = DateTime.Now - lastLogTime;
                                var packetsPerSecond = 200.0 / elapsed.TotalSeconds;
                                LogManager.LogDebug($"[UdpWaveStreamPlayer] Received {localPacketCount} packets so far... ({packetsPerSecond:F1} packets/sec)");
                                lastLogTime = DateTime.Now;
                            }

                            // Ensure data is long enough for the Scream protocol header
                            if (data.Length < 5)
                                continue;

                            // Check if there's a new format signaled by the first 5 bytes
                            if (data[0] != currentRate || data[1] != currentWidth ||
                                data[2] != currentChannels ||
                                data[3] != currentChannelsMapLsb || data[4] != currentChannelsMapMsb)
                            {
                                currentRate = data[0];
                                currentWidth = data[1];
                                currentChannels = data[2];
                                currentChannelsMapLsb = data[3];
                                currentChannelsMapMsb = data[4];

                                // Scream formula to indicate 44.1 or 48k plus a multiplier
                                int newRate = ((currentRate >= 128) ? 44100 : 48000)
                                              * (currentRate % 128);

                                LogManager.Log($"[UdpWaveStreamPlayer] Format change detected: {newRate}Hz, {currentWidth}bit, {currentChannels}ch");

                                // Stop old output before re-initializing
                                this.output?.Stop();

                                // Recreate buffer manager with new format
                                this.bufferManager = new AdaptiveBufferManager(
                                    this.BufferDuration, 
                                    this.WasapiLatency, 
                                    this.UseExclusiveMode,
                                    currentWidth,
                                    newRate
                                );

                                bufferDuration = TimeSpan.FromMilliseconds(bufferManager.CurrentBufferDurationMs);
                                rsws = new BufferedWaveProvider(new WaveFormat(newRate, currentWidth, currentChannels))
                                {
                                    BufferDuration = bufferDuration,
                                    DiscardOnBufferOverflow = true
                                };

                                InitializeOutputDevice(rsws);
                            }

                            // Add samples (starting after the 5-byte header)
                            rsws.AddSamples(data, 5, data.Length - 5);
                            
                            // Monitor buffer status and adapt every 25 packets (plus léger)
                            if (localPacketCount % 25 == 0)
                            {
                                var bufferedMs = rsws.BufferedDuration.TotalMilliseconds;
                                var bufferCapacityMs = rsws.BufferDuration.TotalMilliseconds;
                                
                                // Enregistrer la mesure pour l'adaptation
                                bufferManager.RecordMeasurement(bufferedMs, (int)bufferCapacityMs, localPacketCount);
                                
                                // Update buffer stats
                                lock (statsLock)
                                {
                                    currentStats.NetworkBuffer.UpdateMeasurement(bufferedMs, bufferCapacityMs);
                                    currentStats.WasapiBuffer.UpdateMeasurement(
                                        bufferManager.ActualWasapiLatencyMs,
                                        bufferManager.ActualWasapiLatencyMs
                                    );
                                }
                                
                                // Note: Le buffer réseau (BufferedWaveProvider) ne peut pas être ajusté dynamiquement
                                // Les ajustements prendront effet lors du prochain changement de format audio
                                // WASAPI ne peut également pas être changé sans recréer WasapiOut
                            }
                            
                            // Log statistiques périodiques en debug (stats déjà dans l'UI)
                            if ((DateTime.Now - lastStatsLogTime).TotalSeconds >= 10)
                            {
                                LogManager.LogDebug($"[UdpWaveStreamPlayer] Stats: {bufferManager.GetStatistics()}");
                                lastStatsLogTime = DateTime.Now;
                            }
                        }
                        catch (SocketException ex)
                        {
                            if (ex.SocketErrorCode == SocketError.TimedOut)
                            {
                                LogManager.Log("[UdpWaveStreamPlayer] No data received for 10 seconds - check if Scream is sending data");
                                continue; // Continue waiting
                            }
                            else
                            {
                                LogManager.Log($"[UdpWaveStreamPlayer] Socket error: {ex.SocketErrorCode} - {ex.Message}");
                                break; // Other socket errors - exit
                            }
                        }
                        catch (Exception ex)
                        {
                            LogManager.Log($"[UdpWaveStreamPlayer] Error: {ex.Message}");
                            LogManager.Log($"[UdpWaveStreamPlayer] Stack trace: {ex.StackTrace}");
                            MessageBox.Show(ex.StackTrace, ex.Message);
                        }
                    }

                    // Once Stop() is called, wait on shutdownLock so the method can exit gracefully
                    this.shutdownLock.WaitOne();

                    // Cleanup
                    this.output?.Stop();
                    this.udpClient.Close();
                    this.udpClient.Dispose();
                }
                finally
                {
                    // Release the semaphore so Start() can be called again if needed
                    this.startLock.Release();
                }
            }, this.cancellationTokenSource.Token);
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

                // Store current volume before re-initializing
                int currentVolume = this.volume;

                // Re-initialize output device with the current wave provider (if any)
                if (this.currentWaveProvider != null)
                {
                    this.output?.Stop();
                    this.output?.Dispose();
                    
                    // Update volume to current system volume for the new device
                    this.volume = GetCurrentSystemVolume();
                    
                    InitializeOutputDevice(this.currentWaveProvider);
                }
            }
        }

        public void OnDeviceAdded(string pwstrDeviceId) { }
        public void OnDeviceRemoved(string deviceId) { }
        public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }

        #endregion



        /// <summary>
        /// Gets the current system volume level for the default audio device.
        /// </summary>
        private int GetCurrentSystemVolume()
        {
            try
            {
                using (var mmDeviceEnum = new MMDeviceEnumerator())
                {
                    var device = mmDeviceEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    return (int)(device.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
                }
            }
            catch (Exception ex)
            {
                LogManager.Log($"[UdpWaveStreamPlayer] Failed to get system volume: {ex.Message}");
                return 50; // Default to 50% if we can't get the system volume
            }
        }

        /// <summary>
        /// Sets up the WasapiOut device with the given wave provider, disposing the old one if present.
        /// Uses adaptive latency from the buffer manager for optimal performance.
        /// </summary>
        private void InitializeOutputDevice(IWaveProvider waveProvider)
        {
            if (waveProvider == null) return;

            // Dispose of previous output if it exists
            if (this.output != null)
            {
                this.output.Stop();
                this.output.Dispose();
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
            this.output.Init(this.currentWaveProvider);
            this.output.Volume = (float)this.volume / 100f;
            this.output.Play();
            LogManager.Log($"[UdpWaveStreamPlayer] ✓ Audio playback started (Total latency: ~{bufferManager?.TotalLatencyMs ?? 0}ms)");
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
