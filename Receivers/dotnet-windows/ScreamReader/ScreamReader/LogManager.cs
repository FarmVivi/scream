using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace ScreamReader
{
    /// <summary>
    /// Manages logging for ScreamReader with GUI support
    /// </summary>
    public static class LogManager
    {
        private static readonly List<string> logs = new List<string>();
        private static readonly object lockObject = new object();
        private static LogWindow logWindow = null;

        /// <summary>
        /// Adds a log entry with timestamp
        /// </summary>
        public static void Log(string message)
        {
            lock (lockObject)
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string logEntry = $"[{timestamp}] {message}";
                logs.Add(logEntry);
                
                // Keep only last 1000 entries to prevent memory issues
                if (logs.Count > 1000)
                {
                    logs.RemoveAt(0);
                }
                
                // Update log window if it's open
                if (logWindow != null && !logWindow.IsDisposed)
                {
                    logWindow.Invoke(new Action(() => logWindow.AddLog(logEntry)));
                }
            }
        }

        /// <summary>
        /// Gets all logs as a formatted string
        /// </summary>
        public static string GetAllLogs()
        {
            lock (lockObject)
            {
                return string.Join(Environment.NewLine, logs);
            }
        }

        /// <summary>
        /// Clears all logs
        /// </summary>
        public static void ClearLogs()
        {
            lock (lockObject)
            {
                logs.Clear();
                if (logWindow != null && !logWindow.IsDisposed)
                {
                    logWindow.Invoke(new Action(() => logWindow.ClearLogs()));
                }
            }
        }

        /// <summary>
        /// Shows the log window
        /// </summary>
        public static void ShowLogWindow()
        {
            if (logWindow == null || logWindow.IsDisposed)
            {
                logWindow = new LogWindow();
            }
            
            logWindow.Show();
            logWindow.BringToFront();
        }

        /// <summary>
        /// Gets the current log count
        /// </summary>
        public static int GetLogCount()
        {
            lock (lockObject)
            {
                return logs.Count;
            }
        }
    }
}