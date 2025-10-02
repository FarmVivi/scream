using System;
using System.Windows.Forms;

namespace ScreamReader
{
    /// <summary>
    /// ScreamReader - Modern Low-Latency Network Audio Receiver
    /// Version 2.0 - Interface moderne avec gestion adaptative des buffers
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
            
            LogManager.Log("ScreamReader - Modern Interface v2.0");
            LogManager.LogInfo("Gestion adaptative des buffers pour latence optimale");
            
            using (var mainWindow = new AudioReceiverWindow())
            {
                // Create system tray icon
                var trayIcon = new NotifyIcon
                {
                    Icon = Properties.Resources.speaker_ico,
                    Text = "ScreamReader",
                    Visible = true
                };
                
                // Tray context menu
                var trayMenu = new ContextMenuStrip();
                trayMenu.Items.Add("Afficher", null, (s, e) =>
                {
                    mainWindow.Show();
                    mainWindow.WindowState = FormWindowState.Normal;
                    mainWindow.BringToFront();
                });
                trayMenu.Items.Add("Masquer", null, (s, e) => mainWindow.Hide());
                trayMenu.Items.Add("-");
                trayMenu.Items.Add("Quitter", null, (s, e) =>
                {
                    trayIcon.Visible = false;
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
                
                // Run application
                Application.Run(mainWindow);
                
                // Cleanup
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
        }
    }
}
