using System;
using System.Collections.Generic;
using System.Linq;
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
        private static bool showDebugLogs = true;

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
        /// Adds a debug log entry with timestamp (only shown if debug logs are enabled)
        /// </summary>
        public static void LogDebug(string message)
        {
            if (!showDebugLogs) return;
            
            lock (lockObject)
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string logEntry = $"[{timestamp}] [DEBUG] {message}";
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
        /// Sets whether debug logs should be shown
        /// </summary>
        public static void SetShowDebugLogs(bool show)
        {
            lock (lockObject)
            {
                showDebugLogs = show;
                if (logWindow != null && !logWindow.IsDisposed)
                {
                    logWindow.Invoke(new Action(() => logWindow.RefreshLogs()));
                }
            }
        }

        /// <summary>
        /// Gets whether debug logs are currently shown
        /// </summary>
        public static bool GetShowDebugLogs()
        {
            lock (lockObject)
            {
                return showDebugLogs;
            }
        }

        /// <summary>
        /// Gets all logs as a formatted string, filtering debug logs if needed
        /// </summary>
        public static string GetAllLogs()
        {
            lock (lockObject)
            {
                if (showDebugLogs)
                {
                    return string.Join(Environment.NewLine, logs);
                }
                else
                {
                    // Filter out debug logs
                    var filteredLogs = logs.Where(log => !log.Contains("[DEBUG]")).ToList();
                    return string.Join(Environment.NewLine, filteredLogs);
                }
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