using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace ScreamReader
{
    /// <summary>
    /// Receives a Scream audio stream over UDP (multicast or unicast) and plays it through WASAPI.
    ///
    /// A single background thread owns the socket and the output device and performs all the work;
    /// the UI only reads a lock-free <see cref="StreamStats"/> snapshot. The hot path allocates
    /// nothing and never calls <c>DateTime.Now</c>. When the stream stops, the output is paused so
    /// the audio device (and CPU) can idle — the main reason the previous design drained the battery.
    /// </summary>
    internal sealed class AudioReceiver : IDisposable, IMMNotificationClient
    {
        // Low-latency defaults used when buffer/latency are left on "auto". Kept minimal on purpose
        // (good on a wired LAN); the UI suggests higher values for Wi-Fi. 20 ms is the practical floor
        // for WASAPI shared mode (10 ms engine period + 10 ms driver), so requesting less has no effect.
        private const int DefaultBufferMs = 30;
        private const int DefaultWasapiMs = 20;

        // How long Receive() blocks before we treat the stream as stalled and pause the device.
        private const int SocketReceiveTimeoutMs = 1000;

        // How often the stats snapshot is rebuilt for the UI.
        private const int PublishIntervalMs = 250;

        // Overflows above this rate (per 10 s health window) escalate from a benign note to a warning.
        private const int OverflowWarnRatePer10s = 10;

        private readonly StreamConfiguration config;
        private readonly int bufferMs;
        private readonly int wasapiMs;
        private readonly AudioClientShareMode shareMode;

        private readonly MMDeviceEnumerator deviceEnumerator;
        private readonly object sync = new object();

        private Thread worker;
        private CancellationTokenSource cts;

        // Cross-thread fields. The COM notification thread sets deviceChangePending; the UI reads stats.
        private volatile bool deviceChangePending;
        private volatile StreamStats stats = StreamStats.Empty;

        // Owned by the receive thread, except the socket which Stop() closes to unblock Receive().
        private UdpClient udp;
        private WasapiOut output;
        private BufferedWaveProvider buffer;
        private int actualWasapiMs;

        public AudioReceiver(StreamConfiguration config)
        {
            this.config = config.Clone();
            this.bufferMs = (config.IsAutoBuffer || config.BufferDuration <= 0) ? DefaultBufferMs : config.BufferDuration;
            this.wasapiMs = (config.IsAutoWasapi || config.WasapiLatency <= 0) ? DefaultWasapiMs : config.WasapiLatency;
            this.shareMode = config.UseExclusiveMode ? AudioClientShareMode.Exclusive : AudioClientShareMode.Shared;

            try
            {
                deviceEnumerator = new MMDeviceEnumerator();
                deviceEnumerator.RegisterEndpointNotificationCallback(this);
            }
            catch (Exception ex)
            {
                LogManager.LogWarning($"[AudioReceiver] Default-device notifications unavailable: {ex.Message}");
            }
        }

        /// <summary>Latest state snapshot for the UI. Always non-null.</summary>
        public StreamStats Stats => stats;

        public bool IsRunning { get; private set; }

        public void Start()
        {
            lock (sync)
            {
                if (IsRunning) return;
                cts = new CancellationTokenSource();
                worker = new Thread(() => ReceiveLoop(cts.Token))
                {
                    Name = "ScreamReader.Receive",
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal
                };
                IsRunning = true;
                worker.Start();
            }
            LogManager.LogInfo($"[AudioReceiver] Started — {config} (buffer {bufferMs} ms, WASAPI {wasapiMs} ms)");
        }

        public void Stop()
        {
            Thread toJoin;
            lock (sync)
            {
                if (!IsRunning) return;
                IsRunning = false;
                toJoin = worker;
                worker = null;
                cts.Cancel();
                try { udp?.Close(); } catch { /* unblocks the blocking Receive */ }
            }

            toJoin?.Join(2000);
            cts.Dispose();
            cts = null;
            stats = StreamStats.Empty;
            LogManager.LogInfo("[AudioReceiver] Stopped");
        }

        public void Dispose()
        {
            Stop();
            try { deviceEnumerator?.UnregisterEndpointNotificationCallback(this); } catch { /* ignore */ }
            deviceEnumerator?.Dispose();
        }

        private void ReceiveLoop(CancellationToken token)
        {
            IntPtr mmcssHandle = TryJoinProAudioMmcss();
            try
            {
                OpenSocket();

                // Allocated once and reused for every datagram (no per-packet allocation). Sized to the
                // maximum possible UDP payload so it can never be too small for any packet.
                var receiveBuffer = new byte[65536];
                EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                var clock = Stopwatch.StartNew();
                int prebufferMs = Math.Max(4, Math.Min(bufferMs / 2, 30));

                ScreamHeader currentHeader = default;
                bool hasFormat = false;
                bool playing = false;
                string remote = null;
                long packets = 0, bytes = 0;
                int underruns = 0, overflows = 0;
                const double underrunFloorMs = 2.0;   // the buffer is "starved" at or below this

                // Rolling baseline for packets/sec and bytes/sec, also used as the publish throttle.
                long prevPackets = 0, prevBytes = 0, prevMs = 0;
                // Rate-limit the underrun warning and the periodic health line.
                long lastUnderrunLogMs = -10000, lastHealthLogMs = 0;
                int lastOverflowCount = 0;   // overflow total at the previous health tick (for the rate)

                void Publish(bool connected)
                {
                    long nowMs = clock.ElapsedMilliseconds;
                    double dt = (nowMs - prevMs) / 1000.0;
                    double pps = dt > 0 ? (packets - prevPackets) / dt : 0;
                    double bps = dt > 0 ? (bytes - prevBytes) / dt : 0;
                    prevPackets = packets;
                    prevBytes = bytes;
                    prevMs = nowMs;

                    stats = new StreamStats(
                        isConnected: connected,
                        remoteEndpoint: remote ?? "-",
                        format: hasFormat ? currentHeader.ToString() : "-",
                        sampleRate: hasFormat ? currentHeader.SampleRate : 0,
                        bitDepth: hasFormat ? currentHeader.BitDepth : 0,
                        channels: hasFormat ? currentHeader.Channels : 0,
                        totalPackets: packets,
                        packetsPerSecond: connected ? pps : 0,
                        totalBytes: bytes,
                        bytesPerSecond: connected ? bps : 0,
                        networkBufferedMs: buffer != null ? buffer.BufferedDuration.TotalMilliseconds : 0,
                        networkCapacityMs: bufferMs,
                        wasapiLatencyMs: actualWasapiMs,
                        underrunCount: underruns,
                        overflowCount: overflows,
                        isPlaying: playing);
                }

                void DrainSocketBacklog()
                {
                    // A (re)init stalls the receive loop for as long as WASAPI takes to start (often
                    // 100-300 ms), during which the kernel queues packets. Discarding that backlog
                    // resyncs playback to live audio and avoids a startup overflow burst (clicks).
                    try
                    {
                        while (udp != null && udp.Available > 0)
                            udp.Client.ReceiveFrom(receiveBuffer, ref sender);
                    }
                    catch { /* socket closing */ }
                }

                bool ApplyDeviceChangeIfPending()
                {
                    if (!deviceChangePending) return false;
                    deviceChangePending = false;
                    if (!hasFormat) return false;

                    LogManager.LogInfo("[AudioReceiver] Default playback device changed — re-initializing output");
                    RebuildOutput(currentHeader);
                    playing = false;
                    DrainSocketBacklog();
                    return true;
                }

                while (!token.IsCancellationRequested)
                {
                    int length;
                    try
                    {
                        length = udp.Client.ReceiveFrom(receiveBuffer, ref sender);
                    }
                    catch (SocketException ex)
                    {
                        if (token.IsCancellationRequested) break;
                        if (ex.SocketErrorCode == SocketError.TimedOut)
                        {
                            // No data for a while: the sender stopped. Pause so the device can sleep.
                            if (playing) { PauseOutput(); playing = false; }
                            ApplyDeviceChangeIfPending();
                            Publish(connected: false);
                            continue;
                        }
                        LogManager.LogWarning($"[AudioReceiver] Socket error: {ex.SocketErrorCode}");
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break; // socket closed by Stop()
                    }

                    if (ApplyDeviceChangeIfPending()) continue;

                    if (length < ScreamHeader.Size) continue;

                    packets++;
                    bytes += length;

                    if (remote == null)
                    {
                        remote = sender.ToString();
                        LogManager.LogInfo($"[AudioReceiver] Receiving from {remote}");
                    }

                    var header = ScreamHeader.Parse(receiveBuffer);
                    if (!hasFormat || header.FormatDiffers(currentHeader))
                    {
                        currentHeader = header;
                        hasFormat = true;
                        LogManager.LogInfo($"[AudioReceiver] Format: {header}");
                        RebuildOutput(header);
                        playing = false;
                        DrainSocketBacklog();   // skip packets queued during init → clean, live start
                        continue;
                    }

                    int audioLength = length - ScreamHeader.Size;
                    if (audioLength > 0 && buffer != null)
                    {
                        // DiscardOnBufferOverflow drops samples silently; count them for the stats.
                        // Occasional overflows are normal (sender/DAC clock drift + jitter bursts) and
                        // harmless — so we note it once rather than warning repeatedly. The running
                        // count is shown in the UI ("Glitches") and the periodic health line.
                        if (buffer.BufferedBytes + audioLength > buffer.BufferLength)
                        {
                            overflows++;
                            if (overflows == 1)
                            {
                                LogManager.LogInfo("[AudioReceiver] Buffer overflow (clock drift / jitter burst) — a few ms dropped. Occasional overflows are normal; the running count is in the stats.");
                            }
                        }
                        buffer.AddSamples(receiveBuffer, ScreamHeader.Size, audioLength);
                    }

                    double bufferedMs = buffer != null ? buffer.BufferedDuration.TotalMilliseconds : 0;
                    long nowMs = clock.ElapsedMilliseconds;

                    if (output != null && buffer != null)
                    {
                        if (!playing)
                        {
                            // (Re)build a small jitter cushion before (re)starting playback.
                            if (bufferedMs >= prebufferMs)
                            {
                                try { output.Play(); playing = true; }
                                catch (Exception ex) { LogManager.LogWarning($"[AudioReceiver] Play failed: {ex.Message}"); }
                            }
                        }
                        else if (bufferedMs <= underrunFloorMs)
                        {
                            // Starved: pause and re-buffer rather than let WASAPI play silence (clicks).
                            PauseOutput();
                            playing = false;
                            underruns++;
                            if (nowMs - lastUnderrunLogMs >= 2000)
                            {
                                lastUnderrunLogMs = nowMs;
                                LogManager.LogWarning($"[AudioReceiver] Buffer underrun — audio starved; raise the network buffer (especially on Wi-Fi). Total: {underruns}");
                            }
                        }
                    }

                    if (nowMs - lastHealthLogMs >= 10000)
                    {
                        lastHealthLogMs = nowMs;
                        int fillPct = bufferMs > 0 ? (int)(bufferedMs / bufferMs * 100) : 0;
                        int overflowDelta = overflows - lastOverflowCount;
                        lastOverflowCount = overflows;

                        LogManager.LogDebug($"[AudioReceiver] Buffer health: {bufferedMs:F0}/{bufferMs} ms ({fillPct}%), underruns {underruns}, overflows {overflows}");

                        // Many overflows in a short window is no longer "occasional" — flag it.
                        if (overflowDelta >= OverflowWarnRatePer10s)
                            LogManager.LogWarning($"[AudioReceiver] Frequent buffer overflows ({overflowDelta} in 10 s) — the sender is outpacing playback; increase the network buffer (uncheck Auto buffer) if you hear drops.");
                    }

                    if (nowMs - prevMs >= PublishIntervalMs)
                    {
                        Publish(connected: true);
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[AudioReceiver] Receive loop failed: {ex.Message}");
            }
            finally
            {
                if (mmcssHandle != IntPtr.Zero)
                {
                    try { AvRevertMmThreadCharacteristics(mmcssHandle); } catch { /* ignore */ }
                }
                DisposeOutput();
                CloseSocket();
                // Reflect that the loop has ended (e.g. on a socket error) so Start() can run again.
                lock (sync) { IsRunning = false; }
            }
        }

        private void OpenSocket()
        {
            udp = new UdpClient { ExclusiveAddressUse = false };
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.ReceiveTimeout = SocketReceiveTimeoutMs;
            // Generous kernel receive buffer so bursts/scheduling hiccups don't drop packets.
            udp.Client.ReceiveBufferSize = 1 << 20; // 1 MB

            if (config.IsMulticast)
            {
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, config.Port));
                udp.JoinMulticastGroup(config.IpAddress);
                LogManager.LogInfo($"[AudioReceiver] Joined multicast {config.IpAddress}:{config.Port}");
            }
            else
            {
                udp.Client.Bind(new IPEndPoint(config.IpAddress, config.Port));
                LogManager.LogInfo($"[AudioReceiver] Listening (unicast) on {config.IpAddress}:{config.Port}");
            }
        }

        private void CloseSocket()
        {
            try { udp?.Close(); } catch { /* ignore */ }
            udp = null;
        }

        /// <summary>(Re)creates the buffer and WASAPI output for the given format on the current default device.</summary>
        private void RebuildOutput(ScreamHeader header)
        {
            DisposeOutput();

            var newBuffer = new BufferedWaveProvider(header.ToWaveFormat())
            {
                BufferDuration = TimeSpan.FromMilliseconds(bufferMs),
                DiscardOnBufferOverflow = true
            };

            using (var enumerator = new MMDeviceEnumerator())
            {
                var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                // Prefer event-driven mode (the device wakes us only when it needs data — far less CPU
                // than polling). Fall back to higher latencies, then to polling, if the device refuses.
                var attempts = new[]
                {
                    (eventSync: true,  latency: wasapiMs),
                    (eventSync: true,  latency: wasapiMs + 10),
                    (eventSync: true,  latency: wasapiMs + 20),
                    (eventSync: false, latency: wasapiMs),
                    (eventSync: false, latency: 50),
                    (eventSync: false, latency: 100)
                };

                foreach (var attempt in attempts)
                {
                    WasapiOut candidate = null;
                    try
                    {
                        candidate = new WasapiOut(device, shareMode, attempt.eventSync, attempt.latency);
                        candidate.Init(newBuffer);
                        output = candidate;
                        buffer = newBuffer;
                        actualWasapiMs = attempt.latency;
                        LogManager.LogInfo($"[AudioReceiver] Output ready ({shareMode}, {attempt.latency} ms, {(attempt.eventSync ? "event-driven" : "polling")})");
                        return;
                    }
                    catch (Exception ex)
                    {
                        LogManager.LogDebug($"[AudioReceiver] WASAPI {attempt.latency} ms ({(attempt.eventSync ? "event" : "polling")}) failed: {ex.Message}");
                        candidate?.Dispose();
                    }
                }
            }

            throw new InvalidOperationException($"Unable to initialize WASAPI output ({shareMode} mode).");
        }

        private void DisposeOutput()
        {
            if (output != null)
            {
                try { output.Stop(); } catch { /* ignore */ }
                try { output.Dispose(); } catch { /* ignore */ }
                output = null;
            }
            buffer = null;
        }

        private void PauseOutput()
        {
            try { output?.Pause(); } catch { /* ignore */ }
        }

        // The receive thread feeds the audio buffer in near-real time. Joining the MMCSS "Pro Audio"
        // class gives it the scheduling guarantees recommended for low-latency audio, on top of the
        // AboveNormal priority set in Start(). (NAudio's own WASAPI render thread is not MMCSS-managed.)
        private static IntPtr TryJoinProAudioMmcss()
        {
            try
            {
                uint taskIndex = 0;
                return AvSetMmThreadCharacteristics("Pro Audio", ref taskIndex);
            }
            catch (Exception ex)
            {
                LogManager.LogDebug($"[AudioReceiver] MMCSS unavailable: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        [DllImport("avrt.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr AvSetMmThreadCharacteristics(string taskName, ref uint taskIndex);

        [DllImport("avrt.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AvRevertMmThreadCharacteristics(IntPtr avrtHandle);

        #region IMMNotificationClient

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render && role == Role.Multimedia)
            {
                deviceChangePending = true;
            }
        }

        public void OnDeviceAdded(string pwstrDeviceId) { }
        public void OnDeviceRemoved(string deviceId) { }
        public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }

        #endregion
    }
}
