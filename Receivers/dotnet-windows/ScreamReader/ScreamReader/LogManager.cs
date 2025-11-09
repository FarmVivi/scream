using System.Drawing;

namespace ScreamReader
{
    /// <summary>
    /// Log severity levels
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    /// <summary>
    /// Represents a single log entry
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        public Color Color
        {
            get
            {
                switch (Level)
                {
                    case LogLevel.Debug: return Color.FromArgb(150, 150, 150);  // Light gray (readable on black)
                    case LogLevel.Info: return Color.White;                     // White (readable on black)
                    case LogLevel.Warning: return Color.DarkOrange;             // Orange
                    case LogLevel.Error: return Color.Red;                      // Red
                    default: return Color.White;                                // Default white
                }
            }
        }

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss.fff}] [{Level.ToString().ToUpper()}] {Message}";
        }
    }

    /// <summary>
    /// Manages logging for ScreamReader with multi-level support and GUI integration
    /// </summary>
    public static class LogManager
    {
        private static readonly List<LogEntry> logs = new List<LogEntry>();
        private static readonly object lockObject = new object();
        private static LogLevel minimumLevel = LogLevel.Info;
        
        // Event for real-time log updates
        public static event EventHandler<LogEntry> LogAdded;

        // Maximum logs to keep in memory
        private const int MaxLogEntries = 2000;

        /// <summary>
        /// Sets the minimum log level to display
        /// </summary>
        public static void SetMinimumLevel(LogLevel level)
        {
            lock (lockObject)
            {
                minimumLevel = level;
            }
        }

        /// <summary>
        /// Gets the current minimum log level
        /// </summary>
        public static LogLevel GetMinimumLevel()
        {
            lock (lockObject)
            {
                return minimumLevel;
            }
        }

        /// <summary>
        /// Adds a log entry
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

                // Keep only last MaxLogEntries to prevent memory issues
                if (logs.Count > MaxLogEntries)
                {
                    logs.RemoveAt(0);
                }

                // Raise event for UI updates (only if level is visible)
                if (level >= minimumLevel)
                {
                    LogAdded?.Invoke(null, entry);
                }
            }
        }

        /// <summary>
        /// Logs a debug message
        /// </summary>
        public static void LogDebug(string message)
        {
            AddLog(LogLevel.Debug, message);
        }

        /// <summary>
        /// Logs an info message
        /// </summary>
        public static void Log(string message)
        {
            AddLog(LogLevel.Info, message);
        }

        /// <summary>
        /// Logs an info message (alias for Log)
        /// </summary>
        public static void LogInfo(string message)
        {
            AddLog(LogLevel.Info, message);
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        public static void LogWarning(string message)
        {
            AddLog(LogLevel.Warning, message);
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        public static void LogError(string message)
        {
            AddLog(LogLevel.Error, message);
        }

        /// <summary>
        /// Gets all logs filtered by minimum level
        /// </summary>
        public static List<LogEntry> GetLogs(LogLevel? filterLevel = null)
        {
            lock (lockObject)
            {
                var level = filterLevel ?? minimumLevel;
                return logs.Where(log => log.Level >= level).ToList();
            }
        }

        /// <summary>
        /// Gets all logs as formatted strings
        /// </summary>
        public static string GetAllLogs(LogLevel? filterLevel = null)
        {
            lock (lockObject)
            {
                var filteredLogs = GetLogs(filterLevel);
                return string.Join(Environment.NewLine, filteredLogs.Select(l => l.ToString()));
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
            }
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

        /// <summary>
        /// Gets the count of logs at or above a certain level
        /// </summary>
        public static int GetLogCount(LogLevel level)
        {
            lock (lockObject)
            {
                return logs.Count(log => log.Level >= level);
            }
        }
    }
}