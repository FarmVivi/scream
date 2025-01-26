using NAudio.Wave;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Management;

namespace ScreamReader
{
    internal abstract class UdpWaveStreamPlayer : IDisposable
    {
        #region instance variables        
        private Semaphore startLock;
        private Semaphore shutdownLock;
        private CancellationTokenSource cancellationTokenSource;
        private UdpClient udpClient;
        private WasapiOut output;
        private int volume;
        private IWaveProvider currentWaveProvider;
        #endregion

        #region public properties
        /// <summary>
        /// Used to control the volume. Valid values are [0, 100].
        /// </summary>
        public int Volume
        {
            get
            {
                if (this.output != null) this.volume = (int)(output.Volume * 100);
                Debug.WriteLine("get Volume = {0}", this.volume);
                return this.volume;
            }
            set
            {
                if (value < 0 || value > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

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
        /// Initialize the client with the specific address, port and format.
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

        protected abstract void ConfigureUdpClient(UdpClient udpClient, IPEndPoint localEp);

        /// <summary>
        /// Starts listening to the broadcast and plays back audio received from it.
        /// Subsequent calls to this method require to call <see cref="Stop"/> in between.
        /// </summary>
        public virtual void Start()
        {
            this.startLock.WaitOne();
            this.cancellationTokenSource = new CancellationTokenSource();

            Task.Factory.StartNew(() =>
            {
                var currentRate = 129;
                var currentWidth = 16;
                var currentChannels = 2;
                var currentChannelsMapLsb = 0x03; // stereo
                var currentChannelsMapMsb = 0x00;
                var currentChannelsMap = (currentChannelsMapMsb << 8) | currentChannelsMapLsb;
                IPEndPoint localEp = null;

                ConfigureUdpClient(this.udpClient, localEp);

                var rsws = new BufferedWaveProvider(new WaveFormat(44100, currentWidth, currentChannels))
                {
                    BufferDuration = TimeSpan.FromMilliseconds(200),
                    DiscardOnBufferOverflow = true
                };

                InitializeOutputDevice(rsws);

                Task.Factory.StartNew(() =>
                {
                    while (!this.cancellationTokenSource.IsCancellationRequested)
                    {
                        try
                        {
                            byte[] data = this.udpClient.Receive(ref localEp);

                            if (data[0] != currentRate || data[1] != currentWidth || data[2] != currentChannels
                                || data[3] != currentChannelsMapLsb || data[4] != currentChannelsMapMsb)
                            {
                                currentRate = data[0];
                                currentWidth = data[1];
                                currentChannels = data[2];
                                currentChannelsMapLsb = data[3];
                                currentChannelsMapMsb = data[4];
                                currentChannelsMap = (currentChannelsMapMsb << 8) | currentChannelsMapLsb;

                                // Stop current output
                                if (this.output != null)
                                {
                                    this.output.Stop();
                                }

                                var rate = ((currentRate >= 128) ? 44100 : 48000) * (currentRate % 128);
                                rsws = new BufferedWaveProvider(new WaveFormat(rate, currentWidth, currentChannels))
                                {
                                    BufferDuration = TimeSpan.FromMilliseconds(200),
                                    DiscardOnBufferOverflow = true
                                };
                                InitializeOutputDevice(rsws);
                            }

                            rsws.AddSamples(data, 5, data.Length - 5);
                        }
                        catch (SocketException)
                        {
                            // Usually when interrupted
                        }
                        catch (Exception e)
                        {
                            System.Windows.Forms.MessageBox.Show(e.StackTrace, e.Message);
                        }
                    }
                }, this.cancellationTokenSource.Token);

                this.shutdownLock.WaitOne();

                // Cleanup
                if (this.output != null)
                {
                    this.output.Stop();
                }
                this.udpClient.Close();
            }, this.cancellationTokenSource.Token);
        }

        /// <summary>
        /// Stops reading data from the broadcast and playing it back.
        /// </summary>
        public void Stop()
        {
            this.shutdownLock.Release();
            this.cancellationTokenSource.Cancel();
            this.startLock.Release();
        }

        private void StartAudioDeviceWatcher()
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    ManagementEventWatcher watcher = new ManagementEventWatcher(
                        new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2"));

                    watcher.EventArrived += (sender, args) =>
                    {
                        Console.WriteLine("Audio device change detected. Switching output device...");

                        // Re-initialize output device with the current wave provider
                        if (this.output != null && this.output.PlaybackState == PlaybackState.Playing)
                        {
                            this.output.Stop();
                            this.output.Dispose();

                            // Re-initialize the output device with the same wave provider
                            if (this.currentWaveProvider != null)
                            {
                                InitializeOutputDevice(this.currentWaveProvider);
                            }
                        }
                    };

                    watcher.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to start audio device watcher: {ex.Message}");
                }
            });
        }

        private void InitializeOutputDevice(IWaveProvider waveProvider)
        {
            if (waveProvider == null)
            {
                // If no wave provider, nothing to init
                return;
            }

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
            this.Dispose(true);
        }

        public virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.startLock.Dispose();
                this.shutdownLock.Dispose();
            }
        }
        #endregion
    }
}
