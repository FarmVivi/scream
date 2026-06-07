using System;
using System.Drawing;
using System.Net;
using System.Windows.Forms;

namespace ScreamReader
{
    /// <summary>
    /// Main window: stream configuration, live status and logs. To save power, the stats timer and
    /// log rendering only run while the window is actually visible — when minimized to the tray
    /// (the normal mode) the UI does no work at all.
    /// </summary>
    public partial class AudioReceiverWindow : Form
    {
        private static readonly Color PlayColor = Color.FromArgb(0, 126, 249);
        private static readonly Color StopColor = Color.FromArgb(224, 62, 54);

        private readonly Timer statsTimer;
        private StreamConfiguration currentConfig;
        private AudioReceiver audio;
        private bool isPlaying;
        private bool cleaned;

        public AudioReceiverWindow()
        {
            InitializeComponent();

            ConfigurationManager.Load();
            currentConfig = ConfigurationManager.GetStreamConfiguration();
            LogManager.SetMinimumLevel(ConfigurationManager.MinimumLogLevel);

            statsTimer = new Timer { Interval = 250 };
            statsTimer.Tick += (s, e) => RefreshStats();

            LoadConfigIntoUi();
            UpdatePlayButton();
            WireEvents();
            ConfigureHelp();

            LogManager.LogAdded += OnLogAdded;
        }

        /// <summary>Starts playback in the background without showing the window (called at startup).</summary>
        public void StartAudioAutomatically()
        {
            if (!isPlaying) StartAudio();
        }

        /// <summary>Fully shuts down the receiver (called from the tray "Exit" command).</summary>
        public void ForceClose()
        {
            Cleanup();
        }

        #region Lifecycle

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!IsDisposed && this.Visible)
            {
                ReloadLogs();
                RefreshStats();
            }
            UpdateStatsTimer();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateStatsTimer();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Closing via the window's X minimizes to the tray instead of quitting.
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                return;
            }

            Cleanup();
            base.OnFormClosing(e);
        }

        private void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;

            PersistConfig();

            statsTimer.Stop();
            statsTimer.Dispose();
            LogManager.LogAdded -= OnLogAdded;

            audio?.Stop();
            audio?.Dispose();
            audio = null;
        }

        #endregion

        #region Playback

        private void TogglePlayback()
        {
            if (isPlaying) StopAudio();
            else StartAudio();
        }

        private void StartAudio()
        {
            ReadConfigFromUi();
            if (!currentConfig.IsValid(out string error))
            {
                MessageBox.Show($"Invalid configuration: {error}", "ScreamReader",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            PersistConfig();

            audio?.Dispose();
            audio = new AudioReceiver(currentConfig);
            audio.Start();

            isPlaying = true;
            UpdatePlayButton();
            UpdateStatsTimer();
        }

        private void StopAudio()
        {
            audio?.Stop();
            isPlaying = false;
            UpdatePlayButton();
            UpdateStatsTimer();
            RefreshStats();
        }

        private void UpdatePlayButton()
        {
            btnPlayStop.Text = isPlaying ? "Stop" : "Start";
            btnPlayStop.BackColor = isPlaying ? StopColor : PlayColor;
            ApplyControlStates();
        }

        #endregion

        #region Stats / timer

        private void UpdateStatsTimer()
        {
            // OnResize fires from InitializeComponent (when ClientSize is set), before the timer exists.
            if (statsTimer == null) return;

            bool active = this.Visible && this.WindowState != FormWindowState.Minimized && isPlaying;
            if (active)
            {
                if (!statsTimer.Enabled)
                {
                    RefreshStats();
                    statsTimer.Start();
                }
            }
            else if (statsTimer.Enabled)
            {
                statsTimer.Stop();
            }
        }

        private void RefreshStats()
        {
            var s = audio != null ? audio.Stats : StreamStats.Empty;

            SetText(lblStatus, s.IsConnected ? "Connected" : (isPlaying ? "Waiting…" : "Stopped"));
            lblStatus.ForeColor = s.IsConnected ? Color.Green : (isPlaying ? Color.DarkOrange : Color.Gray);
            SetText(lblEndpoint, s.RemoteEndpoint);
            SetText(lblFormat, s.Format);
            SetText(lblPackets, $"{s.TotalPackets:N0}  ({s.PacketsPerSecond:F0}/s)");
            SetText(lblBitrate, $"{s.BitrateKbps:F0} kbps");
            SetText(lblLatency, $"{s.TotalLatencyMs:F0} ms");
            SetText(lblUnderruns, $"{s.UnderrunCount} underrun / {s.OverflowCount} overflow");

            int fill = (int)Math.Round(s.NetworkFillPercent);
            if (fill < 0) fill = 0; else if (fill > 100) fill = 100;
            if (progressBuffer.Value != fill) progressBuffer.Value = fill;
            SetText(lblBufferText, $"{s.NetworkBufferedMs:F0} / {s.NetworkCapacityMs:F0} ms");
        }

        private static void SetText(Label label, string text)
        {
            if (label.Text != text) label.Text = text;
        }

        #endregion

        #region Logs

        private void OnLogAdded(object sender, LogEntry entry)
        {
            if (IsDisposed) return;
            if (!this.Visible || this.WindowState == FormWindowState.Minimized) return;

            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<LogEntry>(AppendLog), entry); }
                catch { /* window closing */ }
                return;
            }

            AppendLog(entry);
        }

        private void AppendLog(LogEntry entry)
        {
            if (txtLogs.IsDisposed) return;

            const int maxLines = 400;
            if (txtLogs.Lines.Length > maxLines)
            {
                int cut = txtLogs.GetFirstCharIndexFromLine(100);
                if (cut > 0)
                {
                    txtLogs.Select(0, cut);
                    txtLogs.SelectedText = string.Empty;
                }
            }

            AppendColored(entry);
            txtLogs.SelectionStart = txtLogs.TextLength;
            txtLogs.ScrollToCaret();
        }

        private void ReloadLogs()
        {
            if (txtLogs.IsDisposed) return;

            var entries = LogManager.GetLogs();
            txtLogs.Clear();

            int start = Math.Max(0, entries.Count - 300);
            for (int i = start; i < entries.Count; i++)
                AppendColored(entries[i]);

            txtLogs.SelectionStart = txtLogs.TextLength;
            txtLogs.ScrollToCaret();
        }

        private void AppendColored(LogEntry entry)
        {
            txtLogs.SelectionStart = txtLogs.TextLength;
            txtLogs.SelectionLength = 0;
            txtLogs.SelectionColor = entry.Color;
            txtLogs.AppendText(entry.ToString() + Environment.NewLine);
            txtLogs.SelectionColor = txtLogs.ForeColor;
        }

        #endregion

        #region Configuration UI

        /// <summary>
        /// Per-row hover help: what each setting is, what it does, use cases, and special cases.
        /// </summary>
        private void ConfigureHelp()
        {
            toolTip.AutoPopDelay = 30000;
            toolTip.InitialDelay = 300;
            toolTip.ReshowDelay = 100;

            const string formatTip =
                "Audio format — sample rate, bit depth and channel count.\n\n" +
                "Auto-detect: read them from the incoming stream (recommended).\n" +
                "Manual: uncheck to force fixed values — only if detection is wrong.";
            SetRowTooltip(formatTip, lblFormatHeader, chkAutoFormat, lblBits, numBits, lblRate, numRate, lblChannels, numChannels);

            const string bufferTip =
                "Network buffer (jitter buffer).\n\n" +
                "Queues incoming audio before playback to absorb network jitter and\n" +
                "late packets. Larger = fewer dropouts but more delay; smaller =\n" +
                "lower latency but more stutter risk.\n\n" +
                "Auto = sensible low default (30 ms). Uncheck to set it yourself:\n" +
                "  • Wired LAN: 20–30 ms (very stable)\n" +
                "  • Wi-Fi: 50–80 ms\n" +
                "  • Congested / unstable Wi-Fi: 100–150 ms\n\n" +
                "Below ~15 ms it may stutter even on a wired LAN.";
            SetRowTooltip(bufferTip, lblBufferingHeader, chkAutoBuffer, numBuffer, lblBufferMs);

            const string wasapiTip =
                "WASAPI output latency.\n\n" +
                "Delay between handing audio to Windows and hearing it from the\n" +
                "speakers. Lower = more responsive (helps A/V sync); higher costs\n" +
                "almost nothing here.\n\n" +
                "Auto = 20 ms (recommended). Uncheck to set it yourself:\n" +
                "  • Shared mode: 20 ms is the floor (10 ms engine + 10 ms driver) —\n" +
                "    setting less has NO effect.\n" +
                "  • Exclusive mode: can reach ≈5–10 ms, but locks the device.";
            SetRowTooltip(wasapiTip, chkAutoWasapi, numWasapi, lblWasapiMs);

            toolTip.SetToolTip(chkExclusive,
                "Exclusive mode.\n\n" +
                "Gives ScreamReader sole use of the output device for slightly lower\n" +
                "latency. Downside: no other app can play sound while active, and the\n" +
                "device must support the stream format directly.");
        }

        private void SetRowTooltip(string text, params Control[] controls)
        {
            foreach (var c in controls) toolTip.SetToolTip(c, text);
        }

        private void WireEvents()
        {
            btnPlayStop.Click += (s, e) => TogglePlayback();
            btnClearLogs.Click += (s, e) => txtLogs.Clear();
            cmbLogLevel.SelectedIndexChanged += OnLogLevelChanged;

            chkAutoFormat.CheckedChanged += OnConfigToggleChanged;
            chkAutoBuffer.CheckedChanged += OnConfigToggleChanged;
            chkAutoWasapi.CheckedChanged += OnConfigToggleChanged;

            EventHandler persist = (s, e) => PersistConfig();
            txtIp.Leave += persist;
            numPort.ValueChanged += persist;
            radioMulticast.CheckedChanged += persist;
            radioUnicast.CheckedChanged += persist;
            numBits.ValueChanged += persist;
            numRate.ValueChanged += persist;
            numChannels.ValueChanged += persist;
            numBuffer.ValueChanged += persist;
            numWasapi.ValueChanged += persist;
            chkExclusive.CheckedChanged += persist;
        }

        private void OnConfigToggleChanged(object sender, EventArgs e)
        {
            ApplyControlStates();
            PersistConfig();
        }

        private void OnLogLevelChanged(object sender, EventArgs e)
        {
            var level = (LogLevel)cmbLogLevel.SelectedIndex;
            LogManager.SetMinimumLevel(level);
            ConfigurationManager.MinimumLogLevel = level;
            ConfigurationManager.Save();
        }

        private void LoadConfigIntoUi()
        {
            txtIp.Text = currentConfig.IpAddress.ToString();
            numPort.Value = Clamp(currentConfig.Port, numPort);
            radioMulticast.Checked = currentConfig.IsMulticast;
            radioUnicast.Checked = !currentConfig.IsMulticast;

            chkAutoFormat.Checked = currentConfig.IsAutoDetectFormat;
            numBits.Value = Clamp(currentConfig.BitWidth > 0 ? currentConfig.BitWidth : 16, numBits);
            numRate.Value = Clamp(currentConfig.SampleRate > 0 ? currentConfig.SampleRate : 48000, numRate);
            numChannels.Value = Clamp(currentConfig.Channels > 0 ? currentConfig.Channels : 2, numChannels);

            chkAutoBuffer.Checked = currentConfig.IsAutoBuffer;
            numBuffer.Value = Clamp(currentConfig.BufferDuration > 0 ? currentConfig.BufferDuration : 30, numBuffer);

            chkAutoWasapi.Checked = currentConfig.IsAutoWasapi;
            numWasapi.Value = Clamp(currentConfig.WasapiLatency > 0 ? currentConfig.WasapiLatency : 20, numWasapi);

            chkExclusive.Checked = currentConfig.UseExclusiveMode;
            cmbLogLevel.SelectedIndex = (int)ConfigurationManager.MinimumLogLevel;

            ApplyControlStates();
        }

        private void ReadConfigFromUi()
        {
            if (IPAddress.TryParse(txtIp.Text.Trim(), out IPAddress ip))
                currentConfig.IpAddress = ip;

            currentConfig.Port = (int)numPort.Value;
            currentConfig.IsMulticast = radioMulticast.Checked;
            currentConfig.BitWidth = (int)numBits.Value;
            currentConfig.SampleRate = (int)numRate.Value;
            currentConfig.Channels = (int)numChannels.Value;
            currentConfig.IsAutoDetectFormat = chkAutoFormat.Checked;
            currentConfig.IsAutoBuffer = chkAutoBuffer.Checked;
            currentConfig.IsAutoWasapi = chkAutoWasapi.Checked;
            currentConfig.BufferDuration = chkAutoBuffer.Checked ? -1 : (int)numBuffer.Value;
            currentConfig.WasapiLatency = chkAutoWasapi.Checked ? -1 : (int)numWasapi.Value;
            currentConfig.UseExclusiveMode = chkExclusive.Checked;
        }

        private void PersistConfig()
        {
            ReadConfigFromUi();
            ConfigurationManager.UpdateFromStreamConfiguration(currentConfig);
            ConfigurationManager.Save();
        }

        /// <summary>Enables/disables config inputs based on play state and the auto toggles.</summary>
        private void ApplyControlStates()
        {
            bool editable = !isPlaying;

            txtIp.Enabled = editable;
            numPort.Enabled = editable;
            radioMulticast.Enabled = editable;
            radioUnicast.Enabled = editable;
            chkAutoFormat.Enabled = editable;
            chkAutoBuffer.Enabled = editable;
            chkAutoWasapi.Enabled = editable;
            chkExclusive.Enabled = editable;

            numBits.Enabled = editable && !chkAutoFormat.Checked;
            numRate.Enabled = editable && !chkAutoFormat.Checked;
            numChannels.Enabled = editable && !chkAutoFormat.Checked;
            numBuffer.Enabled = editable && !chkAutoBuffer.Checked;
            numWasapi.Enabled = editable && !chkAutoWasapi.Checked;
        }

        private static decimal Clamp(int value, NumericUpDown num)
        {
            if (value < num.Minimum) return num.Minimum;
            if (value > num.Maximum) return num.Maximum;
            return value;
        }

        #endregion
    }
}
