using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ScreamReader
{
    /// <summary>
    /// Log level enumeration
    /// </summary>
    public enum LogLevel
    {
        DEBUG = 0,
        INFO = 1,
        WARNING = 2,
        ERROR = 3
    }

    /// <summary>
    /// Represents a single log entry with level and timestamp
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        
        public override string ToString()
        {
            string levelStr = Level.ToString().PadRight(7);
            return $"[{Timestamp:HH:mm:ss.fff}] [{levelStr}] {Message}";
        }
    }

    /// <summary>
    /// Manages logging for ScreamReader with GUI support
    /// </summary>
    public static class LogManager
    {
        private static readonly List<LogEntry> logs = new List<LogEntry>();
        private static readonly object lockObject = new object();
        private static LogWindow logWindow = null;
        private static LogLevel minimumLogLevel = LogLevel.DEBUG;

        /// <summary>
        /// Adds a log entry with specified level
        /// </summary>
        private static void AddLog(LogLevel level, string message)
        {
            lock (lockObject)
            {
                var entry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = level,
                    Message = message
                };
                
                logs.Add(entry);
                
                // Keep only last 2000 entries to prevent memory issues
                if (logs.Count > 2000)
                {
                    logs.RemoveAt(0);
                }
                
                // Update log window if it's open
                if (logWindow != null && !logWindow.IsDisposed)
                {
                    try
                    {
                        logWindow.Invoke(new Action(() => logWindow.AddLog(entry)));
                    }
                    catch
                    {
                        // Ignore errors if window is closing
                    }
                }
            }
        }

        /// <summary>
        /// Adds an INFO level log entry
        /// </summary>
        public static void Log(string message)
        {
            AddLog(LogLevel.INFO, message);
        }

        /// <summary>
        /// Adds a DEBUG level log entry
        /// </summary>
        public static void LogDebug(string message)
        {
            AddLog(LogLevel.DEBUG, message);
        }

        /// <summary>
        /// Adds a WARNING level log entry
        /// </summary>
        public static void LogWarning(string message)
        {
            AddLog(LogLevel.WARNING, message);
        }

        /// <summary>
        /// Adds an ERROR level log entry
        /// </summary>
        public static void LogError(string message)
        {
            AddLog(LogLevel.ERROR, message);
        }

        /// <summary>
        /// Sets the minimum log level to display
        /// </summary>
        public static void SetMinimumLogLevel(LogLevel level)
        {
            lock (lockObject)
            {
                minimumLogLevel = level;
                if (logWindow != null && !logWindow.IsDisposed)
                {
                    try
                    {
                        logWindow.Invoke(new Action(() => logWindow.RefreshLogs()));
                    }
                    catch
                    {
                        // Ignore errors if window is closing
                    }
                }
            }
        }

        /// <summary>
        /// Gets the current minimum log level
        /// </summary>
        public static LogLevel GetMinimumLogLevel()
        {
            lock (lockObject)
            {
                return minimumLogLevel;
            }
        }

        /// <summary>
        /// Legacy method for compatibility - sets whether debug logs should be shown
        /// </summary>
        public static void SetShowDebugLogs(bool show)
        {
            SetMinimumLogLevel(show ? LogLevel.DEBUG : LogLevel.INFO);
        }

        /// <summary>
        /// Legacy method for compatibility - gets whether debug logs are currently shown
        /// </summary>
        public static bool GetShowDebugLogs()
        {
            return GetMinimumLogLevel() == LogLevel.DEBUG;
        }

        /// <summary>
        /// Gets all logs as a formatted string, filtering by minimum log level
        /// </summary>
        public static string GetAllLogs()
        {
            lock (lockObject)
            {
                var filteredLogs = logs.Where(log => log.Level >= minimumLogLevel).ToList();
                return string.Join(Environment.NewLine, filteredLogs.Select(l => l.ToString()));
            }
        }

        /// <summary>
        /// Gets all log entries (unformatted) for advanced processing
        /// </summary>
        public static List<LogEntry> GetLogEntries()
        {
            lock (lockObject)
            {
                return logs.Where(log => log.Level >= minimumLogLevel).ToList();
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