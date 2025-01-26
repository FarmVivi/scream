using System;
using System.Net;
using System.Windows.Forms;

namespace ScreamReader
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            // Parse arguments to get IP and port
            IPAddress ipAddress = IPAddress.Parse("239.255.77.77"); // Default multicast IP
            int port = 4010; // Default port
            bool helpRequested = false;
            bool multicastIpSpecified = true;

            if (args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string arg = args[i].ToLower();
                    if (arg == "--ip")
                    {
                        if (i + 1 >= args.Length)
                        {
                            Console.WriteLine("L'IP doit être spécifié après --ip");
                            Environment.Exit(1);
                        }
                        string ipAddressStr = args[i + 1];
                        if (!IPAddress.TryParse(ipAddressStr, out IPAddress parsedIpAddress))
                        {
                            Console.WriteLine("L'IP spécifié n'est pas valide");
                            Environment.Exit(1);
                        }
                        ipAddress = parsedIpAddress;
                        i++; // Skip next argument
                    }
                    else if (arg == "--port")
                    {
                        if (i + 1 >= args.Length)
                        {
                            Console.WriteLine("Le port doit être spécifié après --port");
                            Environment.Exit(1);
                        }
                        if (!int.TryParse(args[i + 1], out int parsedPort))
                        {
                            Console.WriteLine("Le port doit être un nombre entier");
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
                    else if (arg == "--help" || arg == "-h")
                    {
                        helpRequested = true;
                        break;
                    }
                }
            }

            if (helpRequested)
            {
                Console.WriteLine("Utilisation :");
                Console.WriteLine("\t--ip <adresse_IP>  : Spécifier l'IP à écouter");
                Console.WriteLine("\t--port <num_port>  : Spécifier le port à écouter");
                Console.WriteLine("\t--unicast          : Utiliser l'adresse IP en unicast");
                Console.WriteLine("\t--multicast        : Utiliser l'adresse IP en multicast (par défaut)");
                Console.WriteLine("\t--help ou -h       : Afficher cette aide");
                Environment.Exit(0);
            }

            startScreamReader(ipAddress, port, multicastIpSpecified);
        }

        [STAThread]
        static void startScreamReader(IPAddress ipAddress, int port, bool multicast)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ScreamReaderTray(ipAddress, port, multicast));
        }
    }


    public class ScreamReaderTray : Form
    {
        protected internal NotifyIcon trayIcon;

        protected ContextMenu trayMenu;

        internal UdpWaveStreamPlayer udpPlayer;

        private MainForm mainForm;

        public ScreamReaderTray(IPAddress ipAddress, int port, bool multicast)
        {
            if (multicast)
            {
                this.udpPlayer = new MulticastUdpWaveStreamPlayer(ipAddress, port);
            }
            else
            {
                this.udpPlayer = new UnicastUdpWaveStreamPlayer(port);
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
