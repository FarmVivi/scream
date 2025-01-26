using NAudio.Wave;
using System;
using System.Diagnostics;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreamReader
{
    internal abstract class UdpWaveStreamPlayer : IDisposable
    {
        #region instance variables
        private readonly Semaphore startLock;
        private readonly Semaphore shutdownLock;
        private CancellationTokenSource cancellationTokenSource;
        private UdpClient udpClient;
        private WasapiOut output;
        private IWaveProvider currentWaveProvider;
        private ManagementEventWatcher deviceWatcher; // to dispose in Dispose()

        private int volume;

        // Fields to store constructor parameters
        protected int BitWidth { get; set; }
        protected int SampleRate { get; set; }
        protected int ChannelCount { get; set; }
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
                Debug.WriteLine("get Volume = {0}", this.volume);
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
                    Debug.WriteLine("set Volume = {0}", this.volume);
                }
            }
        }
        #endregion

        /// <summary>
        /// Default constructor. Initializes common resources.
        /// </summary>
        public UdpWaveStreamPlayer()
        {
            this.startLock = new Semaphore(1, 1);
            this.shutdownLock = new Semaphore(0, 1);

            this.udpClient = new UdpClient
            {
                ExclusiveAddressUse = false
            };

            StartAudioDeviceWatcher();
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
        }

        /// <summary>
        /// Abstract method to let derived classes configure the UdpClient (bind, join group, etc.).
        /// </summary>
        protected abstract void ConfigureUdpClient(UdpClient udpClient, IPEndPoint localEp);

        /// <summary>
        /// Start reading from the UDP stream and playing audio.
        /// </summary>
        public virtual void Start()
        {
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
                    ConfigureUdpClient(this.udpClient, localEp);

                    // Use the user-specified format initially.
                    byte currentRate = (byte)((this.SampleRate == 44100) ? 129 : 1);
                    byte currentWidth = (byte)this.BitWidth;
                    byte currentChannels = (byte)this.ChannelCount;

                    // Basic channel map bytes: (stereo = 0x03, mono = 0x01, etc.)
                    byte currentChannelsMapLsb = (this.ChannelCount == 2) ? (byte)0x03 : (byte)0x01;
                    byte currentChannelsMapMsb = 0x00;

                    var rsws = new BufferedWaveProvider(
                        new WaveFormat(this.SampleRate, this.BitWidth, this.ChannelCount))
                    {
                        BufferDuration = TimeSpan.FromMilliseconds(50),
                        DiscardOnBufferOverflow = true
                    };

                    InitializeOutputDevice(rsws);

                    // Start reading loop
                    while (!this.cancellationTokenSource.IsCancellationRequested)
                    {
                        try
                        {
                            byte[] data = this.udpClient.Receive(ref localEp);

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

                                // This formula is used by Scream to indicate 44.1 or 48k with a multiplier.
                                int newRate = ((currentRate >= 128) ? 44100 : 48000)
                                              * (currentRate % 128);

                                // Stop old output before re-initializing
                                this.output?.Stop();

                                rsws = new BufferedWaveProvider(new WaveFormat(newRate,
                                    currentWidth, currentChannels))
                                {
                                    BufferDuration = TimeSpan.FromMilliseconds(50),
                                    DiscardOnBufferOverflow = true
                                };

                                InitializeOutputDevice(rsws);
                            }

                            // Add samples (starting after the 5-byte header)
                            rsws.AddSamples(data, 5, data.Length - 5);
                        }
                        catch (SocketException)
                        {
                            // Thrown if udpClient is closed or on cancellation
                            break;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.StackTrace, ex.Message);
                        }
                    }

                    // Once Stop() is called, we wait for shutdownLock
                    // to release, so the method can exit gracefully
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

        /// <summary>
        /// Watch for audio device changes on Windows and re-initialize the output device if needed.
        /// </summary>
        private void StartAudioDeviceWatcher()
        {
            try
            {
                this.deviceWatcher = new ManagementEventWatcher(
                    new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2"));

                this.deviceWatcher.EventArrived += (sender, args) =>
                {
                    Console.WriteLine("Audio device change detected. Switching output device...");

                    // Re-initialize output device with the current wave provider
                    if (this.output != null && this.output.PlaybackState == PlaybackState.Playing)
                    {
                        this.output.Stop();
                        this.output.Dispose();

                        if (this.currentWaveProvider != null)
                        {
                            InitializeOutputDevice(this.currentWaveProvider);
                        }
                    }
                };

                this.deviceWatcher.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start audio device watcher: {ex.Message}");
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

            this.currentWaveProvider = waveProvider;
            this.output = new WasapiOut();
            this.output.Init(this.currentWaveProvider);
            this.output.Volume = (float)this.volume / 100f;
            this.output.Play();
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
                    this.deviceWatcher?.Stop();
                    this.deviceWatcher?.Dispose();
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
