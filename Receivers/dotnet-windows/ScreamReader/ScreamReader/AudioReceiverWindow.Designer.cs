namespace ScreamReader
{
    partial class AudioReceiverWindow
    {
        private System.ComponentModel.IContainer components = null;

        // Play/Stop
        private System.Windows.Forms.Button btnPlayStop;

        // Configuration GroupBox
        private System.Windows.Forms.GroupBox grpConfig;
        private System.Windows.Forms.Label lblIpAddress;
        private System.Windows.Forms.TextBox txtIpAddress;
        private System.Windows.Forms.Label lblPort;
        private System.Windows.Forms.NumericUpDown numPort;
        private System.Windows.Forms.RadioButton radioMulticast;
        private System.Windows.Forms.RadioButton radioUnicast;
        private System.Windows.Forms.CheckBox chkAutoDetectFormat;
        private System.Windows.Forms.Label lblDetectedFormat;
        private System.Windows.Forms.Label lblBitWidth;
        private System.Windows.Forms.NumericUpDown numBitWidth;
        private System.Windows.Forms.Label lblSampleRate;
        private System.Windows.Forms.NumericUpDown numSampleRate;
        private System.Windows.Forms.Label lblChannels;
        private System.Windows.Forms.NumericUpDown numChannels;
        private System.Windows.Forms.CheckBox chkAutoBuffer;
        private System.Windows.Forms.NumericUpDown numBufferDuration;
        private System.Windows.Forms.CheckBox chkAutoWasapi;
        private System.Windows.Forms.NumericUpDown numWasapiLatency;
        private System.Windows.Forms.CheckBox chkExclusiveMode;

        // Stats GroupBox
        private System.Windows.Forms.GroupBox grpStats;
        private System.Windows.Forms.Label lblConnectionStatusLabel;
        private System.Windows.Forms.Label lblConnectionStatus;
        private System.Windows.Forms.Label lblRemoteEndpointLabel;
        private System.Windows.Forms.Label lblRemoteEndpoint;
        private System.Windows.Forms.Label lblAudioFormatLabel;
        private System.Windows.Forms.Label lblAudioFormat;
        private System.Windows.Forms.Label lblPacketsReceivedLabel;
        private System.Windows.Forms.Label lblPacketsReceived;
        private System.Windows.Forms.Label lblBitrateLabel;
        private System.Windows.Forms.Label lblBitrate;
        private System.Windows.Forms.Label lblTotalLatencyLabel;
        private System.Windows.Forms.Label lblTotalLatency;

        // Buffer stats
        private System.Windows.Forms.GroupBox grpNetworkBuffer;
        private System.Windows.Forms.ProgressBar progressNetworkBuffer;
        private System.Windows.Forms.Label lblNetworkBuffer;
        private System.Windows.Forms.Label lblNetworkBufferPercent;
        private System.Windows.Forms.Label lblNetworkBufferStatus;

        private System.Windows.Forms.GroupBox grpWasapiBuffer;
        private System.Windows.Forms.ProgressBar progressWasapiBuffer;
        private System.Windows.Forms.Label lblWasapiBuffer;
        private System.Windows.Forms.Label lblWasapiBufferPercent;
        private System.Windows.Forms.Label lblWasapiBufferStatus;

        // Performance
        private System.Windows.Forms.Label lblUnderrunsLabel;
        private System.Windows.Forms.Label lblUnderruns;
        private System.Windows.Forms.Label lblLastUnderrunLabel;
        private System.Windows.Forms.Label lblLastUnderrun;

        // Logs
        private System.Windows.Forms.GroupBox grpLogs;
        private System.Windows.Forms.RichTextBox txtLogs;
        private System.Windows.Forms.ComboBox cmbLogLevel;
        private System.Windows.Forms.Button btnClearLogs;

        // Layout containers
        private System.Windows.Forms.TableLayoutPanel layoutRoot;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.SuspendLayout();

            InitializeControls();
            LayoutControls();

            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1280, 860);
            this.MinimumSize = new System.Drawing.Size(1000, 720);
            this.Name = "AudioReceiverWindow";
            this.Text = "ScreamReader";
            this.Icon = Properties.Resources.speaker_ico;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.BackColor = System.Drawing.Color.FromArgb(246, 246, 246);

            this.ResumeLayout(false);
        }

        private void InitializeControls()
        {
            // Play/Stop Button
            btnPlayStop = new System.Windows.Forms.Button();
            btnPlayStop.AutoSize = true;
            btnPlayStop.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            btnPlayStop.Padding = new System.Windows.Forms.Padding(32, 12, 32, 12);
            btnPlayStop.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            btnPlayStop.Text = "▶ Démarrer";
            btnPlayStop.BackColor = System.Drawing.Color.FromArgb(0, 126, 249);
            btnPlayStop.ForeColor = System.Drawing.Color.White;
            btnPlayStop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnPlayStop.FlatAppearance.BorderSize = 0;
            btnPlayStop.Cursor = System.Windows.Forms.Cursors.Hand;
            btnPlayStop.UseMnemonic = false;
            btnPlayStop.UseVisualStyleBackColor = false;
            btnPlayStop.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(0, 102, 204);
            btnPlayStop.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(0, 92, 184);
            btnPlayStop.Click += btnPlayStop_Click;

            // Configuration Group
            grpConfig = new System.Windows.Forms.GroupBox();
            grpConfig.Text = "Configuration";
            grpConfig.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);

            lblIpAddress = new System.Windows.Forms.Label { Text = "Adresse IP :", AutoSize = true };
            txtIpAddress = new System.Windows.Forms.TextBox();
            txtIpAddress.TextChanged += OnConfigChanged;

            lblPort = new System.Windows.Forms.Label { Text = "Port :", AutoSize = true };
            numPort = new System.Windows.Forms.NumericUpDown();
            numPort.Minimum = 1;
            numPort.Maximum = 65535;
            numPort.Width = 100;
            numPort.ValueChanged += OnConfigChanged;

            radioMulticast = new System.Windows.Forms.RadioButton { Text = "Multicast", AutoSize = true };
            radioMulticast.CheckedChanged += OnConfigChanged;
            radioUnicast = new System.Windows.Forms.RadioButton { Text = "Unicast", AutoSize = true };
            radioUnicast.CheckedChanged += OnConfigChanged;

            chkAutoDetectFormat = new System.Windows.Forms.CheckBox { Text = "Détection auto du format", AutoSize = true, Checked = true };
            chkAutoDetectFormat.CheckedChanged += chkAutoDetectFormat_CheckedChanged;

            lblDetectedFormat = new System.Windows.Forms.Label { Text = "Format détecté : --", AutoSize = true, ForeColor = System.Drawing.Color.DarkGreen };
            lblDetectedFormat.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic);

            lblBitWidth = new System.Windows.Forms.Label { Text = "Bits :", AutoSize = true };
            numBitWidth = new System.Windows.Forms.NumericUpDown();
            numBitWidth.Minimum = 16;
            numBitWidth.Maximum = 32;
            numBitWidth.Increment = 8;
            numBitWidth.Width = 80;
            numBitWidth.ValueChanged += OnConfigChanged;

            lblSampleRate = new System.Windows.Forms.Label { Text = "Fréquence (Hz) :", AutoSize = true };
            numSampleRate = new System.Windows.Forms.NumericUpDown();
            numSampleRate.Minimum = 8000;
            numSampleRate.Maximum = 192000;
            numSampleRate.Increment = 100;
            numSampleRate.Width = 100;
            numSampleRate.ValueChanged += OnConfigChanged;

            lblChannels = new System.Windows.Forms.Label { Text = "Canaux :", AutoSize = true };
            numChannels = new System.Windows.Forms.NumericUpDown();
            numChannels.Minimum = 1;
            numChannels.Maximum = 8;
            numChannels.Width = 80;
            numChannels.ValueChanged += OnConfigChanged;

            chkAutoBuffer = new System.Windows.Forms.CheckBox { Text = "Buffer auto", AutoSize = true, Checked = true };
            chkAutoBuffer.CheckedChanged += chkAutoBuffer_CheckedChanged;
            numBufferDuration = new System.Windows.Forms.NumericUpDown();
            numBufferDuration.Minimum = 1;
            numBufferDuration.Maximum = 1000;
            numBufferDuration.Width = 80;
            numBufferDuration.ValueChanged += OnConfigChanged;

            chkAutoWasapi = new System.Windows.Forms.CheckBox { Text = "WASAPI auto", AutoSize = true, Checked = true };
            chkAutoWasapi.CheckedChanged += chkAutoWasapi_CheckedChanged;
            numWasapiLatency = new System.Windows.Forms.NumericUpDown();
            numWasapiLatency.Minimum = 1;
            numWasapiLatency.Maximum = 1000;
            numWasapiLatency.Width = 80;
            numWasapiLatency.ValueChanged += OnConfigChanged;

            chkExclusiveMode = new System.Windows.Forms.CheckBox { Text = "Mode exclusif", AutoSize = true };
            chkExclusiveMode.CheckedChanged += OnConfigChanged;

            // Stats Group
            grpStats = new System.Windows.Forms.GroupBox();
            grpStats.Text = "Statistiques temps réel";
            grpStats.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);

            lblConnectionStatusLabel = new System.Windows.Forms.Label { Text = "État :", AutoSize = true };
            lblConnectionStatus = new System.Windows.Forms.Label { Text = "Deconnecte", AutoSize = true, ForeColor = System.Drawing.Color.Gray };

            lblRemoteEndpointLabel = new System.Windows.Forms.Label { Text = "Source :", AutoSize = true };
            lblRemoteEndpoint = new System.Windows.Forms.Label { Text = "-", AutoSize = true };

            lblAudioFormatLabel = new System.Windows.Forms.Label { Text = "Format :", AutoSize = true };
            lblAudioFormat = new System.Windows.Forms.Label { Text = "-", AutoSize = true };

            lblPacketsReceivedLabel = new System.Windows.Forms.Label { Text = "Paquets :", AutoSize = true };
            lblPacketsReceived = new System.Windows.Forms.Label { Text = "-", AutoSize = true };

            lblBitrateLabel = new System.Windows.Forms.Label { Text = "Débit :", AutoSize = true };
            lblBitrate = new System.Windows.Forms.Label { Text = "-", AutoSize = true };

            lblTotalLatencyLabel = new System.Windows.Forms.Label { Text = "Latence :", AutoSize = true };
            lblTotalLatency = new System.Windows.Forms.Label { Text = "-", AutoSize = true };

            // Buffer stats
            grpNetworkBuffer = new System.Windows.Forms.GroupBox { Text = "Buffer réseau" };
            progressNetworkBuffer = new System.Windows.Forms.ProgressBar { Minimum = 0, Maximum = 100 };
            lblNetworkBuffer = new System.Windows.Forms.Label { Text = "-", AutoSize = true };
            lblNetworkBufferPercent = new System.Windows.Forms.Label { Text = "-", AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold) };
            lblNetworkBufferStatus = new System.Windows.Forms.Label { Text = "-", AutoSize = true };

            grpWasapiBuffer = new System.Windows.Forms.GroupBox { Text = "Buffer WASAPI" };
            progressWasapiBuffer = new System.Windows.Forms.ProgressBar { Minimum = 0, Maximum = 100 };
            lblWasapiBuffer = new System.Windows.Forms.Label { Text = "-", AutoSize = true };
            lblWasapiBufferPercent = new System.Windows.Forms.Label { Text = "-", AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold) };
            lblWasapiBufferStatus = new System.Windows.Forms.Label { Text = "-", AutoSize = true };

            // Performance
            lblUnderrunsLabel = new System.Windows.Forms.Label { Text = "Sous-alimentations :", AutoSize = true };
            lblUnderruns = new System.Windows.Forms.Label { Text = "0", AutoSize = true, ForeColor = System.Drawing.Color.Green };
            lblLastUnderrunLabel = new System.Windows.Forms.Label { Text = "Dernière :", AutoSize = true };
            lblLastUnderrun = new System.Windows.Forms.Label { Text = "-", AutoSize = true };

            // Logs
            grpLogs = new System.Windows.Forms.GroupBox();
            grpLogs.Text = "Journaux";
            grpLogs.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);

            txtLogs = new System.Windows.Forms.RichTextBox();
            txtLogs.ReadOnly = true;
            txtLogs.BackColor = System.Drawing.Color.Black;
            txtLogs.ForeColor = System.Drawing.Color.LightGreen;
            txtLogs.Font = new System.Drawing.Font("Consolas", 9F);
            txtLogs.WordWrap = false;

            cmbLogLevel = new System.Windows.Forms.ComboBox();
            cmbLogLevel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbLogLevel.Items.AddRange(new object[] { "Debug", "Info", "Warning", "Error" });
            cmbLogLevel.SelectedIndex = 1;
            cmbLogLevel.SelectedIndexChanged += cmbLogLevel_SelectedIndexChanged;

            btnClearLogs = new System.Windows.Forms.Button { Text = "Effacer" };
            btnClearLogs.Click += btnClearLogs_Click;
        }
        private void LayoutControls()
        {
            this.Controls.Clear();

            layoutRoot = new System.Windows.Forms.TableLayoutPanel();
            layoutRoot.ColumnCount = 1;
            layoutRoot.RowCount = 3;
            layoutRoot.Dock = System.Windows.Forms.DockStyle.Fill;
            layoutRoot.Padding = new System.Windows.Forms.Padding(16);
            layoutRoot.BackColor = System.Drawing.Color.Transparent;
            layoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            layoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 55F));
            layoutRoot.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 45F));
            this.Controls.Add(layoutRoot);

            var headerLayout = new System.Windows.Forms.TableLayoutPanel();
            headerLayout.ColumnCount = 3;
            headerLayout.RowCount = 1;
            headerLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            headerLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            headerLayout.Controls.Add(btnPlayStop, 1, 0);
            layoutRoot.Controls.Add(headerLayout, 0, 0);

            var contentLayout = new System.Windows.Forms.TableLayoutPanel();
            contentLayout.ColumnCount = 2;
            contentLayout.RowCount = 1;
            contentLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            contentLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 38F));
            contentLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 62F));
            contentLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            contentLayout.Padding = new System.Windows.Forms.Padding(0, 0, 0, 12);
            layoutRoot.Controls.Add(contentLayout, 0, 1);

            BuildConfigLayout();
            BuildStatsLayout();

            contentLayout.Controls.Add(grpConfig, 0, 0);
            contentLayout.Controls.Add(grpStats, 1, 0);

            BuildLogsLayout();
            layoutRoot.Controls.Add(grpLogs, 0, 2);
        }

        private void BuildConfigLayout()
        {
            grpConfig.SuspendLayout();
            grpConfig.Controls.Clear();
            grpConfig.Dock = System.Windows.Forms.DockStyle.Fill;
            grpConfig.Padding = new System.Windows.Forms.Padding(12, 16, 12, 12);

            var configLayout = new System.Windows.Forms.TableLayoutPanel();
            configLayout.ColumnCount = 2;
            configLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            configLayout.AutoSize = true;
            configLayout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            configLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            configLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));

            int row = 0;

            configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            lblIpAddress.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            txtIpAddress.Dock = System.Windows.Forms.DockStyle.Fill;
            txtIpAddress.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
            configLayout.Controls.Add(lblIpAddress, 0, row);
            configLayout.Controls.Add(txtIpAddress, 1, row);
            row++;

            configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            lblPort.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            numPort.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
            configLayout.Controls.Add(lblPort, 0, row);
            configLayout.Controls.Add(numPort, 1, row);
            row++;

            configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            var lblMode = new System.Windows.Forms.Label { Text = "Mode :", AutoSize = true, Margin = new System.Windows.Forms.Padding(0, 0, 8, 0) };
            var transportLayout = new System.Windows.Forms.FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink,
                Dock = System.Windows.Forms.DockStyle.Fill,
                FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new System.Windows.Forms.Padding(0, 0, 0, 6)
            };
            radioMulticast.Margin = new System.Windows.Forms.Padding(0, 0, 12, 0);
            radioUnicast.Margin = new System.Windows.Forms.Padding(0, 0, 12, 0);
            transportLayout.Controls.Add(radioMulticast);
            transportLayout.Controls.Add(radioUnicast);
            configLayout.Controls.Add(lblMode, 0, row);
            configLayout.Controls.Add(transportLayout, 1, row);
            row++;

            configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            chkAutoDetectFormat.Margin = new System.Windows.Forms.Padding(0, 12, 0, 0);
            configLayout.Controls.Add(chkAutoDetectFormat, 0, row);
            configLayout.SetColumnSpan(chkAutoDetectFormat, 2);
            row++;

            configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            lblDetectedFormat.Margin = new System.Windows.Forms.Padding(0, 4, 0, 8);
            configLayout.Controls.Add(lblDetectedFormat, 0, row);
            configLayout.SetColumnSpan(lblDetectedFormat, 2);
            row++;

            configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            lblBitWidth.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            numBitWidth.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
            configLayout.Controls.Add(lblBitWidth, 0, row);
            configLayout.Controls.Add(numBitWidth, 1, row);
            row++;

            configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            lblSampleRate.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            numSampleRate.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
            configLayout.Controls.Add(lblSampleRate, 0, row);
            configLayout.Controls.Add(numSampleRate, 1, row);
            row++;

            configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            lblChannels.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            numChannels.Margin = new System.Windows.Forms.Padding(0, 0, 0, 12);
            configLayout.Controls.Add(lblChannels, 0, row);
            configLayout.Controls.Add(numChannels, 1, row);
            row++;

            configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            var lblBuffer = new System.Windows.Forms.Label { Text = "Buffer UDP :", AutoSize = true, Margin = new System.Windows.Forms.Padding(0, 0, 8, 0) };
            var bufferLayout = new System.Windows.Forms.FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink,
                Dock = System.Windows.Forms.DockStyle.Fill,
                FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new System.Windows.Forms.Padding(0, 0, 0, 6)
            };
            chkAutoBuffer.Margin = new System.Windows.Forms.Padding(0, 0, 12, 0);
            bufferLayout.Controls.Add(chkAutoBuffer);
            bufferLayout.Controls.Add(numBufferDuration);
            configLayout.Controls.Add(lblBuffer, 0, row);
            configLayout.Controls.Add(bufferLayout, 1, row);
            row++;

            configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            var lblWasapi = new System.Windows.Forms.Label { Text = "Buffer WASAPI :", AutoSize = true, Margin = new System.Windows.Forms.Padding(0, 0, 8, 0) };
            var wasapiLayout = new System.Windows.Forms.FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink,
                Dock = System.Windows.Forms.DockStyle.Fill,
                FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new System.Windows.Forms.Padding(0, 0, 0, 6)
            };
            chkAutoWasapi.Margin = new System.Windows.Forms.Padding(0, 0, 12, 0);
            wasapiLayout.Controls.Add(chkAutoWasapi);
            wasapiLayout.Controls.Add(numWasapiLatency);
            configLayout.Controls.Add(lblWasapi, 0, row);
            configLayout.Controls.Add(wasapiLayout, 1, row);
            row++;

            configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            chkExclusiveMode.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            configLayout.Controls.Add(chkExclusiveMode, 0, row);
            configLayout.SetColumnSpan(chkExclusiveMode, 2);
            row++;

            configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            var filler = new System.Windows.Forms.Panel { Dock = System.Windows.Forms.DockStyle.Fill };
            configLayout.Controls.Add(filler, 0, row);
            configLayout.SetColumnSpan(filler, 2);

            grpConfig.Controls.Add(configLayout);
            grpConfig.ResumeLayout(false);
        }

        private void BuildStatsLayout()
        {
            grpStats.SuspendLayout();
            grpStats.Controls.Clear();
            grpStats.Dock = System.Windows.Forms.DockStyle.Fill;
            grpStats.Padding = new System.Windows.Forms.Padding(12, 16, 12, 12);

            var statsLayout = new System.Windows.Forms.TableLayoutPanel();
            statsLayout.ColumnCount = 2;
            statsLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            statsLayout.AutoSize = true;
            statsLayout.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            statsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            statsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));

            int row = 0;

            statsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            lblConnectionStatusLabel.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            lblConnectionStatus.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            statsLayout.Controls.Add(lblConnectionStatusLabel, 0, row);
            statsLayout.Controls.Add(lblConnectionStatus, 1, row);
            row++;

            statsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            lblRemoteEndpointLabel.Margin = new System.Windows.Forms.Padding(0, 4, 8, 0);
            lblRemoteEndpoint.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            statsLayout.Controls.Add(lblRemoteEndpointLabel, 0, row);
            statsLayout.Controls.Add(lblRemoteEndpoint, 1, row);
            row++;

            statsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            lblAudioFormatLabel.Margin = new System.Windows.Forms.Padding(0, 4, 8, 0);
            lblAudioFormat.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            statsLayout.Controls.Add(lblAudioFormatLabel, 0, row);
            statsLayout.Controls.Add(lblAudioFormat, 1, row);
            row++;

            statsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            lblPacketsReceivedLabel.Margin = new System.Windows.Forms.Padding(0, 4, 8, 0);
            lblPacketsReceived.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            statsLayout.Controls.Add(lblPacketsReceivedLabel, 0, row);
            statsLayout.Controls.Add(lblPacketsReceived, 1, row);
            row++;

            statsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            lblBitrateLabel.Margin = new System.Windows.Forms.Padding(0, 4, 8, 0);
            lblBitrate.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            statsLayout.Controls.Add(lblBitrateLabel, 0, row);
            statsLayout.Controls.Add(lblBitrate, 1, row);
            row++;

            statsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            lblTotalLatencyLabel.Margin = new System.Windows.Forms.Padding(0, 4, 8, 0);
            lblTotalLatency.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            statsLayout.Controls.Add(lblTotalLatencyLabel, 0, row);
            statsLayout.Controls.Add(lblTotalLatency, 1, row);
            row++;

            statsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            var bufferLayout = new System.Windows.Forms.TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = System.Windows.Forms.DockStyle.Fill,
                Margin = new System.Windows.Forms.Padding(0, 16, 0, 0)
            };
            bufferLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            bufferLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));

            BuildBufferGroup(grpNetworkBuffer, progressNetworkBuffer, lblNetworkBuffer, lblNetworkBufferPercent, lblNetworkBufferStatus);
            BuildBufferGroup(grpWasapiBuffer, progressWasapiBuffer, lblWasapiBuffer, lblWasapiBufferPercent, lblWasapiBufferStatus);

            bufferLayout.Controls.Add(grpNetworkBuffer, 0, 0);
            bufferLayout.Controls.Add(grpWasapiBuffer, 1, 0);
            statsLayout.Controls.Add(bufferLayout, 0, row);
            statsLayout.SetColumnSpan(bufferLayout, 2);
            row++;

            statsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            var underrunLayout = new System.Windows.Forms.FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink,
                Dock = System.Windows.Forms.DockStyle.Fill,
                FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new System.Windows.Forms.Padding(0, 16, 0, 0)
            };
            lblUnderrunsLabel.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            lblUnderruns.Margin = new System.Windows.Forms.Padding(0, 0, 16, 0);
            lblLastUnderrunLabel.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            lblLastUnderrun.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            underrunLayout.Controls.Add(lblUnderrunsLabel);
            underrunLayout.Controls.Add(lblUnderruns);
            underrunLayout.Controls.Add(lblLastUnderrunLabel);
            underrunLayout.Controls.Add(lblLastUnderrun);
            statsLayout.Controls.Add(underrunLayout, 0, row);
            statsLayout.SetColumnSpan(underrunLayout, 2);

            grpStats.Controls.Add(statsLayout);
            grpStats.ResumeLayout(false);
        }
        private void BuildBufferGroup(System.Windows.Forms.GroupBox groupBox, System.Windows.Forms.ProgressBar progressBar, System.Windows.Forms.Label valueLabel, System.Windows.Forms.Label percentLabel, System.Windows.Forms.Label statusLabel)
        {
            groupBox.SuspendLayout();
            groupBox.Controls.Clear();
            groupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            groupBox.Padding = new System.Windows.Forms.Padding(12);

            var layout = new System.Windows.Forms.TableLayoutPanel
            {
                ColumnCount = 2,
                Dock = System.Windows.Forms.DockStyle.Fill
            };
            layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));

            progressBar.Dock = System.Windows.Forms.DockStyle.Fill;
            progressBar.Height = 18;
            progressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            layout.Controls.Add(progressBar, 0, 0);
            layout.SetColumnSpan(progressBar, 2);

            valueLabel.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            percentLabel.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            layout.Controls.Add(valueLabel, 0, 1);
            layout.Controls.Add(percentLabel, 1, 1);

            statusLabel.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            layout.Controls.Add(statusLabel, 0, 2);
            layout.SetColumnSpan(statusLabel, 2);

            groupBox.Controls.Add(layout);
            groupBox.ResumeLayout(false);
        }

        private void BuildLogsLayout()
        {
            grpLogs.SuspendLayout();
            grpLogs.Controls.Clear();
            grpLogs.Dock = System.Windows.Forms.DockStyle.Fill;
            grpLogs.Padding = new System.Windows.Forms.Padding(12, 16, 12, 12);

            var logsLayout = new System.Windows.Forms.TableLayoutPanel();
            logsLayout.ColumnCount = 1;
            logsLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            logsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            logsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));

            var logToolbar = new System.Windows.Forms.FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink,
                Dock = System.Windows.Forms.DockStyle.Fill,
                FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight,
                WrapContents = false
            };

            var lblLogLevel = new System.Windows.Forms.Label
            {
                Text = "Niveau:",
                AutoSize = true,
                Margin = new System.Windows.Forms.Padding(0, 6, 8, 0)
            };

            cmbLogLevel.Width = 140;
            cmbLogLevel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            btnClearLogs.AutoSize = true;
            btnClearLogs.Margin = new System.Windows.Forms.Padding(12, 0, 0, 0);

            logToolbar.Controls.Add(lblLogLevel);
            logToolbar.Controls.Add(cmbLogLevel);
            logToolbar.Controls.Add(btnClearLogs);

            logsLayout.Controls.Add(logToolbar, 0, 0);

            txtLogs.Dock = System.Windows.Forms.DockStyle.Fill;
            txtLogs.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            logsLayout.Controls.Add(txtLogs, 0, 1);

            grpLogs.Controls.Add(logsLayout);
            grpLogs.ResumeLayout(false);
        }
    }
}
