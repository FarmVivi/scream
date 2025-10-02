using System;
using System.Net;

namespace ScreamReader
{
    /// <summary>
    /// Configuration for the audio stream receiver
    /// </summary>
    public class StreamConfiguration
    {
        // Network settings
        public IPAddress IpAddress { get; set; }
        public int Port { get; set; }
        public bool IsMulticast { get; set; }

        // Audio settings (can be auto-detected from stream)
        public int BitWidth { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }

        // Buffer settings
        public int BufferDuration { get; set; } = -1;  // -1 = auto
        public int WasapiLatency { get; set; } = -1;   // -1 = auto
        public bool UseExclusiveMode { get; set; } = false;

        /// <summary>
        /// Creates a default configuration
        /// </summary>
        public static StreamConfiguration CreateDefault()
        {
            return new StreamConfiguration
            {
                IpAddress = IPAddress.Parse("239.255.77.77"),
                Port = 4010,
                IsMulticast = true,
                BitWidth = 16,
                SampleRate = 44100,
                Channels = 2,
                BufferDuration = -1,
                WasapiLatency = -1,
                UseExclusiveMode = false
            };
        }

        /// <summary>
        /// Creates a clone of the configuration
        /// </summary>
        public StreamConfiguration Clone()
        {
            return new StreamConfiguration
            {
                IpAddress = this.IpAddress,
                Port = this.Port,
                IsMulticast = this.IsMulticast,
                BitWidth = this.BitWidth,
                SampleRate = this.SampleRate,
                Channels = this.Channels,
                BufferDuration = this.BufferDuration,
                WasapiLatency = this.WasapiLatency,
                UseExclusiveMode = this.UseExclusiveMode
            };
        }

        /// <summary>
        /// Validates the configuration
        /// </summary>
        public bool IsValid(out string error)
        {
            if (IpAddress == null)
            {
                error = "Adresse IP invalide";
                return false;
            }

            if (Port < 1 || Port > 65535)
            {
                error = "Port invalide (doit être entre 1 et 65535)";
                return false;
            }

            if (BitWidth != 16 && BitWidth != 24 && BitWidth != 32)
            {
                error = "Profondeur de bits invalide (16, 24 ou 32)";
                return false;
            }

            if (SampleRate < 8000 || SampleRate > 192000)
            {
                error = "Fréquence d'échantillonnage invalide (8000-192000 Hz)";
                return false;
            }

            if (Channels < 1 || Channels > 8)
            {
                error = "Nombre de canaux invalide (1-8)";
                return false;
            }

            if (BufferDuration != -1 && (BufferDuration < 1 || BufferDuration > 1000))
            {
                error = "Durée de buffer invalide (1-1000 ms ou -1 pour auto)";
                return false;
            }

            if (WasapiLatency != -1 && (WasapiLatency < 1 || WasapiLatency > 1000))
            {
                error = "Latence WASAPI invalide (1-1000 ms ou -1 pour auto)";
                return false;
            }

            error = null;
            return true;
        }

        public override string ToString()
        {
            return $"{(IsMulticast ? "Multicast" : "Unicast")} {IpAddress}:{Port} | " +
                   $"{SampleRate}Hz {BitWidth}bit {Channels}ch | " +
                   $"Buffer:{(BufferDuration == -1 ? "Auto" : BufferDuration + "ms")} " +
                   $"WASAPI:{(WasapiLatency == -1 ? "Auto" : WasapiLatency + "ms")} " +
                   $"{(UseExclusiveMode ? "Exclusive" : "Shared")}";
        }
    }
}
