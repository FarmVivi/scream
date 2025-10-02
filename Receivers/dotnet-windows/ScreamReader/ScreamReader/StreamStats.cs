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

        // Buffer stats - d\u00e9sormais g\u00e9r\u00e9s directement par AdaptiveBufferManager
        // On garde juste les valeurs pour compatibilit\u00e9 UI
        public double NetworkBufferedMs { get; set; }
        public double NetworkBufferCapacityMs { get; set; }
        public double NetworkBufferFillPercentage => NetworkBufferCapacityMs > 0 ? (NetworkBufferedMs / NetworkBufferCapacityMs) * 100.0 : 0;
        
        public double WasapiBufferedMs { get; set; }
        public double WasapiBufferCapacityMs { get; set; }
        public double WasapiBufferFillPercentage => WasapiBufferCapacityMs > 0 ? (WasapiBufferedMs / WasapiBufferCapacityMs) * 100.0 : 0;

        // Latency
        public double TotalLatencyMs => NetworkBufferedMs + WasapiBufferedMs;

        // Performance
        public int UnderrunCount { get; set; }
        public DateTime? LastUnderrun { get; set; }

        public StreamStats()
        {
            // Les stats de buffer sont désormais gérées par AdaptiveBufferManager
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
                NetworkBufferedMs = this.NetworkBufferedMs,
                NetworkBufferCapacityMs = this.NetworkBufferCapacityMs,
                WasapiBufferedMs = this.WasapiBufferedMs,
                WasapiBufferCapacityMs = this.WasapiBufferCapacityMs,
                UnderrunCount = this.UnderrunCount,
                LastUnderrun = this.LastUnderrun
            };
        }
    }

    /// <summary>
    /// Represents buffer statistics
    /// </summary>

}
