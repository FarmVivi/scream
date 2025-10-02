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
        private const int PRE_BUFFER_THRESHOLD_PERCENT = 40;  // Remplir à 40% avant de démarrer
        private const int LOW_BUFFER_THRESHOLD_PERCENT = 15;  // Pause si descend sous 15%
        private const int RESUME_BUFFER_THRESHOLD_PERCENT = 40; // Reprendre à 40%
        
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
            
            // Prevent multiple calls without stopping
            this.startLock.WaitOne();

            // Create a new token for this run
            this.cancellationTokenSource = new CancellationTokenSource();

            // Initialize adaptive buffer manager (une seule fois, puis réutiliser)
            if (this.bufferManager == null)
            {
                this.bufferManager = new AdaptiveBufferManager(
                    this.BufferDuration, 
                    this.WasapiLatency, 
                    this.UseExclusiveMode,
                    this.BitWidth,
                    this.SampleRate
                );
                LogManager.Log("[UdpWaveStreamPlayer] AdaptiveBufferManager créé");
            }
            else
            {
                // Appliquer les recommandations de la session précédente
                this.bufferManager.ApplyRecommendedWasapiLatency();
                LogManager.Log($"[UdpWaveStreamPlayer] AdaptiveBufferManager réutilisé - Buffer cible: {bufferManager.CurrentBufferDurationMs}ms, WASAPI: {bufferManager.CurrentWasapiLatencyMs}ms");
            }

            // Run in background with high priority
            Task.Run(() =>
            {
                try
                {
                    // Set thread priority to high for better audio performance
                    System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.AboveNormal;
                    
                    // Recréer le UDP client si nécessaire (après Stop())
                    if (this.udpClient == null)
                    {
                        this.udpClient = new UdpClient
                        {
                            ExclusiveAddressUse = false
                        };
                        this.udpClient.Client.ReceiveTimeout = 10000;
                        LogManager.LogDebug("[UdpWaveStreamPlayer] UDP client recréé après Stop()");
                    }
                    
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
                    BufferedWaveProvider rsws = null;
                    bool outputInitialized = false;
                    int packetsBeforeInit = this.IsAutoDetectFormat ? 3 : 0;  // Attendre 3 paquets pour détecter le format en mode auto, 0 en mode manuel
                    
                    if (this.IsAutoDetectFormat)
                    {
                        LogManager.Log($"[UdpWaveStreamPlayer] Mode auto-détection: Waiting for {packetsBeforeInit} packets to detect format...");
                    }
                    else
                    {
                        LogManager.Log($"[UdpWaveStreamPlayer] Mode manuel: Using format {this.SampleRate}Hz, {this.BitWidth}bit, {this.ChannelCount}ch");
                        // Créer immédiatement le buffer avec le format spécifié
                        rsws = new BufferedWaveProvider(new WaveFormat(this.SampleRate, this.BitWidth, this.ChannelCount))
                        {
                            BufferDuration = bufferDuration,
                            DiscardOnBufferOverflow = true
                        };
                        
                        // NE PAS initialiser WASAPI maintenant - attendre d'avoir des données dans le buffer
                        // WASAPI sera initialisé au premier paquet (packetsBeforeInit = 0)
                        LogManager.Log($"[UdpWaveStreamPlayer] Buffer created with manual format, WASAPI will start on first packet");
                    }

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
                            
                            // Mettre à jour le statut de connexion au premier paquet
                            if (localPacketCount == 1)
                            {
                                lock (statsLock)
                                {
                                    currentStats.IsConnected = true;
                                    currentStats.RemoteEndpoint = localEp.ToString();
                                }
                                LogManager.Log($"[UdpWaveStreamPlayer] Connected to {localEp}");
                            }
                            
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
                            
                            if (localPacketCount <= 3) // Log first 3 packets only
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

                                bool isFirstInit = !outputInitialized;
                                if (isFirstInit)
                                {
                                    LogManager.Log($"[UdpWaveStreamPlayer] Format detected: {newRate}Hz, {currentWidth}bit, {currentChannels}ch");
                                    
                                    // Première détection : ne PAS recréer le bufferManager !
                                    // Il existe déjà avec les bonnes valeurs adaptées (si restart)
                                    // On se contente de récupérer sa capacité actuelle pour le nouveau buffer
                                    LogManager.LogDebug($"[UdpWaveStreamPlayer] Conservation du BufferManager existant (adaptations préservées)");
                                }
                                else
                                {
                                    LogManager.Log($"[UdpWaveStreamPlayer] Format change detected: {newRate}Hz, {currentWidth}bit, {currentChannels}ch");
                                    // Stop old output before re-initializing
                                    this.output?.Stop();
                                    
                                    // Changement de format réel : recréer le bufferManager
                                    // MAIS utiliser les valeurs adaptées au lieu des valeurs initiales
                                    int adaptedBufferDuration = (int)this.bufferManager.CurrentBufferDurationMs;
                                    int adaptedWasapiLatency = (int)this.bufferManager.CurrentWasapiLatencyMs;
                                    
                                    this.bufferManager = new AdaptiveBufferManager(
                                        adaptedBufferDuration,  // Utiliser valeur adaptée au lieu de this.BufferDuration
                                        adaptedWasapiLatency,   // Utiliser valeur adaptée au lieu de this.WasapiLatency
                                        this.UseExclusiveMode,
                                        currentWidth,
                                        newRate
                                    );
                                    
                                    LogManager.LogDebug($"[UdpWaveStreamPlayer] BufferManager recréé avec valeurs adaptées: Buffer={adaptedBufferDuration}ms, WASAPI={adaptedWasapiLatency}ms");
                                }

                                bufferDuration = TimeSpan.FromMilliseconds(bufferManager.CurrentBufferDurationMs);
                                rsws = new BufferedWaveProvider(new WaveFormat(newRate, currentWidth, currentChannels))
                                {
                                    BufferDuration = bufferDuration,
                                    DiscardOnBufferOverflow = true
                                };

                                // Mettre à jour les stats avec le format détecté
                                lock (statsLock)
                                {
                                    currentStats.SampleRate = newRate;
                                    currentStats.BitDepth = currentWidth;
                                    currentStats.Channels = currentChannels;
                                }
                                
                                // Initialiser WASAPI seulement après avoir attendu quelques paquets
                                if (!outputInitialized && localPacketCount >= packetsBeforeInit)
                                {
                                    InitializeOutputDevice(rsws);
                                    outputInitialized = true;
                                    // isWasapiPlaying déjà à false, le pré-buffering démarrera la lecture
                                    LogManager.LogInfo($"[UdpWaveStreamPlayer] WASAPI initialized with detected format");
                                }
                                else if (outputInitialized)
                                {
                                    // Le format a changé, réinitialiser WASAPI avec le nouveau buffer
                                    InitializeOutputDevice(rsws);
                                    // Réinitialiser le flag pour forcer un nouveau pré-buffering
                                    isWasapiPlaying = false;
                                    LogManager.LogInfo($"[UdpWaveStreamPlayer] WASAPI re-initialized due to format change");
                                }
                            }
                            
                            // Si WASAPI n'est pas encore initialisé (mode manuel avec format correspondant),
                            // l'initialiser maintenant qu'on a reçu assez de paquets
                            if (!outputInitialized && rsws != null && localPacketCount >= packetsBeforeInit)
                            {
                                InitializeOutputDevice(rsws);
                                outputInitialized = true;
                                // isWasapiPlaying déjà à false, le pré-buffering démarrera la lecture
                                LogManager.LogInfo($"[UdpWaveStreamPlayer] WASAPI initialized (manual mode)");
                            }

                            // Audio data starts at byte 5
                            int audioDataSize = data.Length - 5;
                            if (audioDataSize <= 0)
                                continue;

                            // Add the audio data to the buffer as soon as rsws is created
                            // (even if WASAPI is not yet initialized, samples will be buffered)
                            if (rsws != null)
                            {
                                rsws.AddSamples(data, 5, audioDataSize);
                            
                                // Mettre à jour les stats du buffer à CHAQUE paquet pour un affichage fluide dans l'UI
                                var bufferedMs = rsws.BufferedDuration.TotalMilliseconds;
                                var bufferCapacityMs = rsws.BufferDuration.TotalMilliseconds;
                                var bufferFillPercent = (bufferedMs / bufferCapacityMs) * 100.0;
                                
                                // Mettre à jour les stats directement depuis AdaptiveBufferManager
                                lock (statsLock)
                                {
                                    currentStats.NetworkBufferedMs = bufferManager.NetworkBufferedMs;
                                    currentStats.NetworkBufferCapacityMs = bufferManager.NetworkBufferCapacityMs;
                                    currentStats.WasapiBufferedMs = bufferManager.WasapiBufferedMs;
                                    currentStats.WasapiBufferCapacityMs = bufferManager.WasapiBufferCapacityMs;
                                }
                                
                                // === PRÉ-BUFFERING INTELLIGENT ===
                                // Gestion automatique de Play/Pause basée sur le niveau du buffer
                                if (outputInitialized && this.output != null)
                                {
                                    if (!isWasapiPlaying)
                                    {
                                        // Attendre que le buffer soit suffisamment rempli avant de démarrer
                                        if (bufferFillPercent >= PRE_BUFFER_THRESHOLD_PERCENT)
                                        {
                                            this.output.Play();
                                            isWasapiPlaying = true;
                                            LogManager.LogDebug($"[UdpWaveStreamPlayer] ▶ Playback started after pre-buffering ({bufferFillPercent:F1}% - {bufferedMs:F1}ms/{bufferCapacityMs:F0}ms)");
                                        }
                                    }
                                    else
                                    {
                                        // Surveiller le niveau du buffer pendant la lecture
                                        if (bufferFillPercent < LOW_BUFFER_THRESHOLD_PERCENT)
                                        {
                                            // Buffer critique - mettre en pause pour éviter les underruns
                                            this.output.Pause();
                                            isWasapiPlaying = false;
                                            LogManager.LogDebug($"[UdpWaveStreamPlayer] ⏸ Buffer too low ({bufferFillPercent:F1}%), pausing for re-buffering...");
                                        }
                                    }
                                }
                                
                                // Enregistrer la mesure pour l'adaptation tous les 25 paquets (moins intensif)
                                if (localPacketCount % 25 == 0)
                                {
                                    bufferManager.RecordMeasurement(bufferedMs, (int)bufferCapacityMs, localPacketCount);
                                    
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
                        }
                        catch (SocketException ex)
                        {
                            if (ex.SocketErrorCode == SocketError.TimedOut)
                            {
                                // Timeout silencieux - continuer d'attendre sans logger
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
                this.output?.Stop();
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
                    this.output?.Stop();
                    this.output?.Dispose();
                    
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
            
            // NE PAS appeler Play() ici - le pré-buffering le fera automatiquement
            // quand le buffer sera suffisamment rempli
            isWasapiPlaying = false;
            LogManager.Log($"[UdpWaveStreamPlayer] ✓ WASAPI initialized, waiting for pre-buffering ({PRE_BUFFER_THRESHOLD_PERCENT}% of buffer)");
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
