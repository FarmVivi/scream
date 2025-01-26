using System;
using System.Net;
using System.Windows.Forms;

namespace ScreamReader
{
    static class Program
    {
        /// <summary>
        /// Main entry point of the application.
        /// </summary>
        static void Main(string[] args)
        {
            // Default values
            IPAddress ipAddress = null; // Default multicast IP
            int port = -1;              // Default port
            int bitWidth = 16;          // Default bit width
            int rate = 44100;           // Default sample rate
            int channels = 2;           // Default channels (stéréo)

            bool multicastIpSpecified = true;

            // Check if user requested help
            foreach (string arg in args)
            {
                var lowerArg = arg.ToLower();
                if (lowerArg == "--help" || lowerArg == "-h")
                {
                    ShowHelp();
                    Environment.Exit(0);
                    break;
                }
            }

            // Parse command line arguments
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLower();

                if (arg == "--ip")
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine("IP address must be specified after --ip");
                        Environment.Exit(1);
                    }
                    string ipAddressStr = args[i + 1];
                    if (!IPAddress.TryParse(ipAddressStr, out IPAddress parsedIpAddress))
                    {
                        Console.WriteLine("Specified IP address is invalid");
                        Environment.Exit(1);
                    }
                    ipAddress = parsedIpAddress;
                    i++;
                }
                else if (arg == "--port")
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine("Port must be specified after --port");
                        Environment.Exit(1);
                    }
                    if (!int.TryParse(args[i + 1], out int parsedPort))
                    {
                        Console.WriteLine("Port must be an integer");
                        Environment.Exit(1);
                    }
                    port = parsedPort;
                    i++;
                }
                else if (arg == "--unicast")
                {
                    multicastIpSpecified = false;
                }
                else if (arg == "--multicast")
                {
                    multicastIpSpecified = true;
                }
                else if (arg == "--bit-width")
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine("Bit depth must be specified after --bit-width");
                        Environment.Exit(1);
                    }
                    if (!int.TryParse(args[i + 1], out bitWidth))
                    {
                        Console.WriteLine("Bit depth must be an integer (e.g. 16, 24, 32)");
                        Environment.Exit(1);
                    }
                    i++;
                }
                else if (arg == "--rate")
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine("Sample rate must be specified after --rate");
                        Environment.Exit(1);
                    }
                    if (!int.TryParse(args[i + 1], out rate))
                    {
                        Console.WriteLine("Sample rate must be an integer (e.g. 44100, 48000)");
                        Environment.Exit(1);
                    }
                    i++;
                }
                else if (arg == "--channels")
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine("Number of channels must be specified after --channels");
                        Environment.Exit(1);
                    }
                    if (!int.TryParse(args[i + 1], out channels))
                    {
                        Console.WriteLine("Number of channels must be an integer (e.g. 1, 2, 6)");
                        Environment.Exit(1);
                    }
                    i++;
                }
            }

            // Specify default port if not specified
            if (port == -1)
            {
                if (multicastIpSpecified)
                {
                    port = 4010;
                }
                else
                {
                    port = 4011;
                }
            }

            // Specify default IP if not specified
            if (ipAddress == null)
            {
                if (multicastIpSpecified)
                {
                    ipAddress = IPAddress.Parse("239.255.77.77");
                }
                else
                {
                    ipAddress = IPAddress.Any;
                }
            }

            // Launch the application
            startScreamReader(bitWidth, rate, channels, port, ipAddress, multicastIpSpecified);
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("\t--ip <IP_address>    : Specify the IP address (unicast or multicast) to listen on (default 239.255.77.77)");
            Console.WriteLine("\t--port <port_number> : Specify the port to listen on (default 4010)");
            Console.WriteLine("\t--unicast            : Use IP address in unicast");
            Console.WriteLine("\t--multicast          : Use IP address in multicast (default)");
            Console.WriteLine("\t--bit-width <val>    : Specify bit depth (e.g., 16, 24, 32) (default 16)");
            Console.WriteLine("\t--rate <val>         : Specify sample rate (e.g., 44100, 48000) (default 44100)");
            Console.WriteLine("\t--channels <val>     : Specify number of channels (1 = mono, 2 = stereo, etc.) (default 2)");
            Console.WriteLine("\t--help or -h         : Show this help");
        }

        [STAThread]
        static void startScreamReader(int bitWidth, int rate, int channels, int port, IPAddress ipAddress, bool multicast)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ScreamReaderTray(bitWidth, rate, channels, port, ipAddress, multicast));
        }
    }


    public class ScreamReaderTray : Form
    {
        protected internal NotifyIcon trayIcon;
        protected ContextMenu trayMenu;
        internal UdpWaveStreamPlayer udpPlayer;
        private MainForm mainForm;

        public ScreamReaderTray(int bitWidth, int rate, int channels, int port, IPAddress ipAddress, bool multicast)
        {
            if (multicast)
            {
                this.udpPlayer = new MulticastUdpWaveStreamPlayer(bitWidth, rate, channels, port, ipAddress);
            }
            else
            {
                this.udpPlayer = new UnicastUdpWaveStreamPlayer(bitWidth, rate, channels, port, ipAddress);
            }

            this.udpPlayer.Start();
            this.mainForm = new MainForm(this);
            this.trayMenu = new ContextMenu();

            this.trayIcon = new NotifyIcon();
            this.trayIcon.Text = "ScreamReader";
            this.trayIcon.Icon = Properties.Resources.speaker_ico;

            // Add menu to tray icon and show it.
            this.trayIcon.ContextMenu = trayMenu;
            this.trayIcon.Visible = true;
            this.trayIcon.DoubleClick += (object sender, EventArgs e) =>
            {
                if (this.mainForm.Visible)
                {
                    this.mainForm.Focus();
                    return;
                }
                this.mainForm.ShowDialog(this);
            };

            trayMenu.MenuItems.Add("Exit", this.OnExit);
        }

        private void OnExit(object sender, EventArgs e)
        {
            this.udpPlayer.Dispose();
            trayIcon.Visible = false;
            Environment.Exit(0);
        }

        protected override void OnLoad(EventArgs e)
        {
            this.Visible = false;
            this.ShowInTaskbar = false;
            base.OnLoad(e);
        }
    }
}
