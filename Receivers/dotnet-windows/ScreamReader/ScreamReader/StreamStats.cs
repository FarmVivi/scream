namespace ScreamReader
{
    /// <summary>
    /// Immutable snapshot of the receiver state, published by <see cref="AudioReceiver"/> a few
    /// times per second and read by the UI without locking (atomic reference swap).
    /// </summary>
    internal sealed class StreamStats
    {
        /// <summary>Idle snapshot shown when nothing is playing.</summary>
        public static readonly StreamStats Empty = new StreamStats();

        public bool IsConnected { get; }
        public string RemoteEndpoint { get; }
        public string Format { get; }
        public int SampleRate { get; }
        public int BitDepth { get; }
        public int Channels { get; }

        public long TotalPackets { get; }
        public double PacketsPerSecond { get; }
        public long TotalBytes { get; }
        public double BytesPerSecond { get; }
        public double BitrateKbps => BytesPerSecond * 8 / 1000.0;

        public double NetworkBufferedMs { get; }
        public double NetworkCapacityMs { get; }
        public double NetworkFillPercent => NetworkCapacityMs > 0 ? NetworkBufferedMs / NetworkCapacityMs * 100.0 : 0;

        public int WasapiLatencyMs { get; }
        public double TotalLatencyMs => NetworkBufferedMs + WasapiLatencyMs;

        public int UnderrunCount { get; }
        public int OverflowCount { get; }
        public bool IsPlaying { get; }

        private StreamStats()
        {
            RemoteEndpoint = "-";
            Format = "-";
        }

        public StreamStats(
            bool isConnected, string remoteEndpoint, string format,
            int sampleRate, int bitDepth, int channels,
            long totalPackets, double packetsPerSecond, long totalBytes, double bytesPerSecond,
            double networkBufferedMs, double networkCapacityMs,
            int wasapiLatencyMs, int underrunCount, int overflowCount, bool isPlaying)
        {
            IsConnected = isConnected;
            RemoteEndpoint = remoteEndpoint;
            Format = format;
            SampleRate = sampleRate;
            BitDepth = bitDepth;
            Channels = channels;
            TotalPackets = totalPackets;
            PacketsPerSecond = packetsPerSecond;
            TotalBytes = totalBytes;
            BytesPerSecond = bytesPerSecond;
            NetworkBufferedMs = networkBufferedMs;
            NetworkCapacityMs = networkCapacityMs;
            WasapiLatencyMs = wasapiLatencyMs;
            UnderrunCount = underrunCount;
            OverflowCount = overflowCount;
            IsPlaying = isPlaying;
        }
    }
}
