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

            // Run in background
            Task.Run(() =>
            {
                try
                {
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

                    var rsws = new BufferedWaveProvider(
                        new WaveFormat(this.SampleRate, this.BitWidth, this.ChannelCount))
                    {
                        BufferDuration = GetOptimalBufferDuration(), // Dynamically optimized based on system capabilities
                        DiscardOnBufferOverflow = true
                    };

                    InitializeOutputDevice(rsws);

                    LogManager.Log("[UdpWaveStreamPlayer] Starting UDP receive loop...");
                    LogManager.Log("[UdpWaveStreamPlayer] Waiting for audio data...");
                    
                    int packetCount = 0;
                    // Start reading loop
                    while (!this.cancellationTokenSource.IsCancellationRequested)
                    {
                        try
                        {
                            byte[] data = this.udpClient.Receive(ref localEp);
                            packetCount++;
                            if (packetCount <= 5) // Log first 5 packets in detail
                            {
                                LogManager.Log($"[UdpWaveStreamPlayer] Received packet #{packetCount}: {data.Length} bytes from {localEp}");
                                if (data.Length >= 5)
                                {
                                    LogManager.Log($"[UdpWaveStreamPlayer] Header: rate={data[0]}, width={data[1]}, channels={data[2]}, map_lsb={data[3]}, map_msb={data[4]}");
                                }
                            }
                            else if (packetCount % 100 == 0) // Log every 100th packet
                            {
                                LogManager.Log($"[UdpWaveStreamPlayer] Received {packetCount} packets so far...");
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

                                // Stop old output before re-initializing
                                this.output?.Stop();

                                rsws = new BufferedWaveProvider(new WaveFormat(newRate, currentWidth, currentChannels))
                                {
                                    BufferDuration = GetOptimalBufferDuration(), // Dynamically optimized based on system capabilities
                                    DiscardOnBufferOverflow = true
                                };

                                InitializeOutputDevice(rsws);
                            }

                            // Add samples (starting after the 5-byte header)
                            rsws.AddSamples(data, 5, data.Length - 5);
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
        /// Gets optimal buffer duration based on user settings or system capabilities.
        /// </summary>
        private TimeSpan GetOptimalBufferDuration()
        {
            // Use user-specified value if provided
            if (this.BufferDuration > 0)
            {
                LogManager.Log($"[UdpWaveStreamPlayer] Using user-specified buffer duration: {this.BufferDuration}ms");
                return TimeSpan.FromMilliseconds(this.BufferDuration);
            }

            // Auto-detect based on system capabilities
            try
            {
                using (var mmDeviceEnum = new MMDeviceEnumerator())
                {
                    var device = mmDeviceEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    
                    // Check if device supports low latency
                    var audioClient = device.AudioClient;
                    var format = audioClient.MixFormat;
                    
                    // For high sample rates (48kHz+), we can use smaller buffers
                    if (format.SampleRate >= 48000)
                    {
                        LogManager.Log("[UdpWaveStreamPlayer] Auto-detected: Using 20ms buffer for high-quality device");
                        return TimeSpan.FromMilliseconds(20); // Conservative for stability
                    }
                    else
                    {
                        LogManager.Log("[UdpWaveStreamPlayer] Auto-detected: Using 50ms buffer for standard device");
                        return TimeSpan.FromMilliseconds(50); // More conservative for lower sample rates
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Log($"[UdpWaveStreamPlayer] Failed to detect optimal buffer duration: {ex.Message}");
                LogManager.Log("[UdpWaveStreamPlayer] Using safe fallback: 50ms buffer");
                return TimeSpan.FromMilliseconds(50); // Safe fallback for stability
            }
        }

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
            // so that each time the default device changes, we can switch.
            using (var mmDeviceEnum = new MMDeviceEnumerator())
            {
                var device = mmDeviceEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                
                // Determine share mode
                var shareMode = this.UseExclusiveMode ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared;
                LogManager.Log($"[UdpWaveStreamPlayer] UseExclusiveMode = {this.UseExclusiveMode}, using {shareMode} mode");
                
                // Use user-specified latency if provided, otherwise auto-detect
                if (this.WasapiLatency > 0)
                {
                    try
                    {
                        this.output = new WasapiOut(device, shareMode, false, this.WasapiLatency);
                        LogManager.Log($"[UdpWaveStreamPlayer] Using user-specified {shareMode} mode with {this.WasapiLatency}ms latency");
                    }
                    catch (Exception ex)
                    {
                        LogManager.Log($"[UdpWaveStreamPlayer] Failed with user-specified latency: {ex.Message}");
                        LogManager.Log("[UdpWaveStreamPlayer] Falling back to auto-detection...");
                        InitializeWithAutoDetection(device, shareMode);
                    }
                }
                else
                {
                    InitializeWithAutoDetection(device, shareMode);
                }
            }
        }

        /// <summary>
        /// Initializes WasapiOut with auto-detected optimal latency settings.
        /// </summary>
        private void InitializeWithAutoDetection(MMDevice device, AudioClientShareMode shareMode)
        {
            // Try progressively higher latencies until one works
            int[] latencyOptions = shareMode == AudioClientShareMode.Exclusive 
                ? new int[] { 10, 20, 30, 50 }  // Exclusive mode can handle lower latencies
                : new int[] { 20, 30, 50, 100 }; // Shared mode with reasonable latencies for mixer visibility
            
            bool success = false;
            
            foreach (int latency in latencyOptions)
            {
                try
                {
                    this.output = new WasapiOut(device, shareMode, false, latency);
                    LogManager.Log($"[UdpWaveStreamPlayer] Auto-detected: Using {shareMode} mode with {latency}ms latency");
                    success = true;
                    break;
                }
                catch (Exception ex)
                {
                    LogManager.Log($"[UdpWaveStreamPlayer] Failed to initialize with {latency}ms latency: {ex.Message}");
                    if (this.output != null)
                    {
                        this.output.Dispose();
                        this.output = null;
                    }
                }
            }
            
            if (!success)
            {
                throw new InvalidOperationException($"Failed to initialize WasapiOut with {shareMode} mode and any latency setting");
            }

            this.currentWaveProvider = waveProvider;
            LogManager.Log("[UdpWaveStreamPlayer] Initializing WasapiOut with wave provider...");
            this.output.Init(this.currentWaveProvider);
            LogManager.Log("[UdpWaveStreamPlayer] Setting volume...");
            this.output.Volume = (float)this.volume / 100f;
            LogManager.Log("[UdpWaveStreamPlayer] Starting audio playback...");
            this.output.Play();
            LogManager.Log("[UdpWaveStreamPlayer] Audio playback started successfully");
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
