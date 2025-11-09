namespace ScreamReader
{
    /// <summary>
    /// ScreamReader - Modern Low-Latency Network Audio Receiver
    /// </summary>
    static class Program
    {
        /// <summary>
        /// Main entry point of the application
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            LogManager.Log($"ScreamReader v{version}");
            
            // Check if this is first use (no config in registry)
            bool isFirstRun = ConfigurationManager.IsFirstRun();
            
            var mainWindow = new AudioReceiverWindow();
            
            // Create system tray icon
            var trayIcon = new NotifyIcon
            {
                Icon = Properties.Resources.speaker_ico,
                Text = "ScreamReader",
                Visible = true
            };
            
            // Tray context menu
            var trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Show", null, (s, e) =>
            {
                mainWindow.Show();
                mainWindow.WindowState = FormWindowState.Normal;
                mainWindow.BringToFront();
            });
            trayMenu.Items.Add("Hide", null, (s, e) => mainWindow.Hide());
            trayMenu.Items.Add("-");
            trayMenu.Items.Add("Exit", null, (s, e) =>
            {
                LogManager.LogInfo("Application shutdown requested");
                trayIcon.Visible = false;
                trayIcon.Dispose();
                mainWindow.ForceClose(); // Complete shutdown with cleanup
                Application.Exit();
            });
            trayIcon.ContextMenuStrip = trayMenu;
            
            // Double-click to show/restore window
            trayIcon.DoubleClick += (s, e) =>
            {
                mainWindow.Show();
                mainWindow.WindowState = FormWindowState.Normal;
                mainWindow.BringToFront();
            };
            
            // Handle application exit
            Application.ApplicationExit += (s, e) =>
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                mainWindow.Dispose();
            };
            
            // Behavior on startup depending on config presence
            if (isFirstRun)
            {
                // First use: show window without starting
                LogManager.LogInfo("First use - Showing interface");
                mainWindow.Show();
            }
            else
            {
                // Existing config: start automatically in background
                LogManager.LogInfo("Configuration detected - Auto-starting in background");
                mainWindow.StartAudioAutomatically();
                // Do not call Show() - window stays hidden
            }
            
            // Run application without showing main window by default
            Application.Run();
        }
    }
}
