using Microsoft.Win32;
using System.Drawing;
using System.Net;
using System.Runtime.Versioning;

namespace ScreamReader
{
    /// <summary>
    /// Persistent configuration manager for Windows registry
    /// Saves and restores all user settings
    /// </summary>
    public static class ConfigurationManager
    {
        private const string REGISTRY_KEY_PATH = @"Software\ScreamReader";

        #region Configuration Properties

        // Network Settings
        public static string IpAddress { get; set; } = "239.255.77.77";
        public static int Port { get; set; } = 4010;
        public static bool IsMulticast { get; set; } = true;

        // Audio Format Settings (-1 = auto-detect)
        public static int BitWidth { get; set; } = 16;
        public static int SampleRate { get; set; } = 48000;
        public static int Channels { get; set; } = 2;

        // Auto-Detection Flags
        public static bool IsAutoDetectFormat { get; set; } = true;  // true = auto-detect format from stream
        public static bool IsAutoBuffer { get; set; } = false;       // false = use manual values
        public static bool IsAutoWasapi { get; set; } = false;       // false = use manual values

        // Buffer Settings (-1 = auto-adapt)
        public static int BufferDuration { get; set; } = 48;
        public static int WasapiLatency { get; set; } = 2;
        public static bool UseExclusiveMode { get; set; } = false;

        // Application Settings
        public static LogLevel MinimumLogLevel { get; set; } = LogLevel.Info;

        // Window Settings
        public static Size WindowSize { get; set; } = new Size(1400, 900);
        public static Point WindowLocation { get; set; } = Point.Empty;
        public static bool WindowMaximized { get; set; } = false;

        #endregion

        /// <summary>
        /// Checks if this is the first run (no config in registry)
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static bool IsFirstRun()
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH, false);
                return key == null; // First run if key doesn't exist
            }
            catch
            {
                return true; // In case of error, consider it as first run
            }
        }

        /// <summary>
        /// Loads configuration from registry
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static void Load()
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH, false);
                if (key == null)
                {
                    LogManager.LogInfo("[Config] First run - Using default values");
                    return;
                }

                // Network Settings
                IpAddress = key.GetValue("IpAddress", IpAddress) as string;
                Port = (int)key.GetValue("Port", Port);
                IsMulticast = Convert.ToBoolean(key.GetValue("IsMulticast", IsMulticast));

                LogManager.LogDebug($"[Config] Loaded from registry: IP={IpAddress}, Port={Port}, Multicast={IsMulticast}");

                // Audio Format Settings
                BitWidth = (int)key.GetValue("BitWidth", BitWidth);
                SampleRate = (int)key.GetValue("SampleRate", SampleRate);
                Channels = (int)key.GetValue("Channels", Channels);

                // Auto-Detection Flags
                IsAutoDetectFormat = Convert.ToBoolean(key.GetValue("IsAutoDetectFormat", IsAutoDetectFormat));
                IsAutoBuffer = Convert.ToBoolean(key.GetValue("IsAutoBuffer", IsAutoBuffer));
                IsAutoWasapi = Convert.ToBoolean(key.GetValue("IsAutoWasapi", IsAutoWasapi));

                // Buffer Settings
                BufferDuration = (int)key.GetValue("BufferDuration", BufferDuration);
                WasapiLatency = (int)key.GetValue("WasapiLatency", WasapiLatency);
                UseExclusiveMode = Convert.ToBoolean(key.GetValue("UseExclusiveMode", UseExclusiveMode));

                // Application Settings
                MinimumLogLevel = (LogLevel)Enum.Parse(typeof(LogLevel),
                    key.GetValue("MinimumLogLevel", MinimumLogLevel.ToString()) as string);

                // Window Settings
                int width = (int)key.GetValue("WindowWidth", WindowSize.Width);
                int height = (int)key.GetValue("WindowHeight", WindowSize.Height);
                WindowSize = new Size(width, height);

                int x = (int)key.GetValue("WindowX", -1);
                int y = (int)key.GetValue("WindowY", -1);
                if (x >= 0 && y >= 0)
                {
                    WindowLocation = new Point(x, y);
                }

                WindowMaximized = Convert.ToBoolean(key.GetValue("WindowMaximized", WindowMaximized));

                LogManager.LogInfo("[Config] Configuration loaded from registry");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[Config] Error loading configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves configuration to registry
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static void Save()
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY_PATH, true);
                if (key == null)
                {
                    LogManager.LogError("[Config] Unable to create registry key");
                    return;
                }

                // Network Settings
                key.SetValue("IpAddress", IpAddress, RegistryValueKind.String);
                key.SetValue("Port", Port, RegistryValueKind.DWord);
                key.SetValue("IsMulticast", IsMulticast, RegistryValueKind.DWord);

                // Audio Format Settings
                key.SetValue("BitWidth", BitWidth, RegistryValueKind.DWord);
                key.SetValue("SampleRate", SampleRate, RegistryValueKind.DWord);
                key.SetValue("Channels", Channels, RegistryValueKind.DWord);

                // Auto-Detection Flags
                key.SetValue("IsAutoDetectFormat", IsAutoDetectFormat, RegistryValueKind.DWord);
                key.SetValue("IsAutoBuffer", IsAutoBuffer, RegistryValueKind.DWord);
                key.SetValue("IsAutoWasapi", IsAutoWasapi, RegistryValueKind.DWord);

                // Buffer Settings
                key.SetValue("BufferDuration", BufferDuration, RegistryValueKind.DWord);
                key.SetValue("WasapiLatency", WasapiLatency, RegistryValueKind.DWord);
                key.SetValue("UseExclusiveMode", UseExclusiveMode, RegistryValueKind.DWord);

                // Application Settings
                key.SetValue("MinimumLogLevel", MinimumLogLevel.ToString(), RegistryValueKind.String);

                // Window Settings
                key.SetValue("WindowWidth", WindowSize.Width, RegistryValueKind.DWord);
                key.SetValue("WindowHeight", WindowSize.Height, RegistryValueKind.DWord);
                key.SetValue("WindowX", WindowLocation.X, RegistryValueKind.DWord);
                key.SetValue("WindowY", WindowLocation.Y, RegistryValueKind.DWord);
                key.SetValue("WindowMaximized", WindowMaximized, RegistryValueKind.DWord);

                LogManager.LogDebug("[Config] Configuration saved to registry");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[Config] Error saving configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Resets configuration to default values
        /// </summary>
        [SupportedOSPlatform("windows")]
        public static void Reset()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKey(REGISTRY_KEY_PATH, false);
                LogManager.LogInfo("[Config] Configuration reset to default values");

                // Restore default values
                IpAddress = "239.255.77.77";
                Port = 4010;
                IsMulticast = true;
                BitWidth = -1;
                SampleRate = -1;
                Channels = -1;
                IsAutoDetectFormat = true;
                IsAutoBuffer = true;
                IsAutoWasapi = true;
                BufferDuration = -1;
                WasapiLatency = -1;
                UseExclusiveMode = false;
                MinimumLogLevel = LogLevel.Info;
                WindowSize = new Size(1400, 900);
                WindowLocation = Point.Empty;
                WindowMaximized = false;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[Config] Error resetting configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a StreamConfiguration from current settings
        /// </summary>
        public static StreamConfiguration GetStreamConfiguration()
        {
            var config = new StreamConfiguration
            {
                IpAddress = IPAddress.Parse(IpAddress),
                Port = Port,
                IsMulticast = IsMulticast,
                BitWidth = BitWidth,
                SampleRate = SampleRate,
                Channels = Channels,
                IsAutoDetectFormat = IsAutoDetectFormat,
                IsAutoBuffer = IsAutoBuffer,
                IsAutoWasapi = IsAutoWasapi,
                BufferDuration = BufferDuration,
                WasapiLatency = WasapiLatency,
                UseExclusiveMode = UseExclusiveMode
            };

            return config;
        }

        /// <summary>
        /// Updates settings from a StreamConfiguration
        /// </summary>
        public static void UpdateFromStreamConfiguration(StreamConfiguration config)
        {
            IpAddress = config.IpAddress.ToString();
            Port = config.Port;
            IsMulticast = config.IsMulticast;
            BitWidth = config.BitWidth;
            SampleRate = config.SampleRate;
            Channels = config.Channels;
            IsAutoDetectFormat = config.IsAutoDetectFormat;
            IsAutoBuffer = config.IsAutoBuffer;
            IsAutoWasapi = config.IsAutoWasapi;
            BufferDuration = config.BufferDuration;
            WasapiLatency = config.WasapiLatency;
            UseExclusiveMode = config.UseExclusiveMode;
        }
    }
}
