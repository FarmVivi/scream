using System;

namespace ScreamReader
{
    /// <summary>
    /// Represents real-time audio stream statistics
    /// </summary>
    public class StreamStats
    {
        // Connection info
        public bool IsConnected { get; set; }
        public string RemoteEndpoint { get; set; }
        public DateTime? ConnectionTime { get; set; }

        // Audio format (auto-detected from stream)
        public int SampleRate { get; set; }
        public int BitDepth { get; set; }
        public int Channels { get; set; }
        public string FormatDescription => $"{SampleRate}Hz, {BitDepth}bit, {Channels}ch";

        // Network stats
        public long TotalPacketsReceived { get; set; }
        public double PacketsPerSecond { get; set; }
        public long TotalBytesReceived { get; set; }
        public double BytesPerSecond { get; set; }
        public double Bitrate => BytesPerSecond * 8 / 1000.0; // kbps

        // Buffer stats
        public BufferStats NetworkBuffer { get; set; }
        public BufferStats WasapiBuffer { get; set; }

        // Latency
        public double TotalLatencyMs => (NetworkBuffer?.CurrentMs ?? 0) + (WasapiBuffer?.CurrentMs ?? 0);

        // Performance
        public int UnderrunCount { get; set; }
        public DateTime? LastUnderrun { get; set; }

        public StreamStats()
        {
            NetworkBuffer = new BufferStats { Name = "Network" };
            WasapiBuffer = new BufferStats { Name = "WASAPI" };
        }

        /// <summary>
        /// Creates a clone of the current stats for thread-safe UI updates
        /// </summary>
        public StreamStats Clone()
        {
            return new StreamStats
            {
                IsConnected = this.IsConnected,
                RemoteEndpoint = this.RemoteEndpoint,
                ConnectionTime = this.ConnectionTime,
                SampleRate = this.SampleRate,
                BitDepth = this.BitDepth,
                Channels = this.Channels,
                TotalPacketsReceived = this.TotalPacketsReceived,
                PacketsPerSecond = this.PacketsPerSecond,
                TotalBytesReceived = this.TotalBytesReceived,
                BytesPerSecond = this.BytesPerSecond,
                NetworkBuffer = this.NetworkBuffer?.Clone(),
                WasapiBuffer = this.WasapiBuffer?.Clone(),
                UnderrunCount = this.UnderrunCount,
                LastUnderrun = this.LastUnderrun
            };
        }
    }

    /// <summary>
    /// Represents buffer statistics
    /// </summary>
    public class BufferStats
    {
        public string Name { get; set; }
        
        // Current state
        public double CurrentMs { get; set; }
        public double CapacityMs { get; set; }
        public double FillPercentage => CapacityMs > 0 ? (CurrentMs / CapacityMs) * 100.0 : 0;

        // Historical
        public double MinMs { get; set; } = double.MaxValue;
        public double MaxMs { get; set; }
        public double AvgMs { get; set; }

        // Health
        public BufferHealth Health
        {
            get
            {
                if (FillPercentage < 15) return BufferHealth.Critical;
                if (FillPercentage < 25) return BufferHealth.Low;
                if (FillPercentage > 80) return BufferHealth.High;
                return BufferHealth.Optimal;
            }
        }

        public string HealthDescription
        {
            get
            {
                switch (Health)
                {
                    case BufferHealth.Critical: return "CRITIQUE";
                    case BufferHealth.Low: return "Bas";
                    case BufferHealth.Optimal: return "Optimal";
                    case BufferHealth.High: return "Élevé";
                    default: return "Inconnu";
                }
            }
        }

        /// <summary>
        /// Updates statistics with a new measurement
        /// </summary>
        public void UpdateMeasurement(double currentMs, double capacityMs)
        {
            CurrentMs = currentMs;
            CapacityMs = capacityMs;

            if (currentMs < MinMs) MinMs = currentMs;
            if (currentMs > MaxMs) MaxMs = currentMs;

            // Simple running average (can be improved with weighted average)
            AvgMs = (AvgMs + currentMs) / 2.0;
        }

        /// <summary>
        /// Resets min/max/avg statistics
        /// </summary>
        public void ResetStatistics()
        {
            MinMs = CurrentMs;
            MaxMs = CurrentMs;
            AvgMs = CurrentMs;
        }

        /// <summary>
        /// Creates a clone for thread-safe access
        /// </summary>
        public BufferStats Clone()
        {
            return new BufferStats
            {
                Name = this.Name,
                CurrentMs = this.CurrentMs,
                CapacityMs = this.CapacityMs,
                MinMs = this.MinMs,
                MaxMs = this.MaxMs,
                AvgMs = this.AvgMs
            };
        }
    }

    /// <summary>
    /// Buffer health status
    /// </summary>
    public enum BufferHealth
    {
        Critical,   // < 15%
        Low,        // 15-25%
        Optimal,    // 25-80%
        High        // > 80%
    }
}
