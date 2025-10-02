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

        // Volume
        private System.Windows.Forms.Label lblVolumeLabel;
        private System.Windows.Forms.TrackBar trackBarVolume;
        private System.Windows.Forms.Label lblVolume;

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
            
            // Form properties
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;  // Font-based scaling (standard)
            this.ClientSize = new System.Drawing.Size(1400, 950);  // Augmenté de 900 à 950 pour plus d'espace
            this.MinimumSize = new System.Drawing.Size(1200, 750);  // Augmenté de 700 à 750
            this.Name = "AudioReceiverWindow";
            this.Text = "ScreamReader";
            this.Icon = Properties.Resources.speaker_ico;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.BackColor = System.Drawing.Color.FromArgb(240, 240, 240);
            
            this.Resize += AudioReceiverWindow_Resize;  // Gestion du redimensionnement

            InitializeControls();
            LayoutControls();

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void InitializeControls()
        {
            // Play/Stop Button
            btnPlayStop = new System.Windows.Forms.Button();
            btnPlayStop.Size = new System.Drawing.Size(150, 50);
            btnPlayStop.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold);
            btnPlayStop.Text = "▶ Start";
            btnPlayStop.BackColor = System.Drawing.Color.LightGreen;
            btnPlayStop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnPlayStop.Cursor = System.Windows.Forms.Cursors.Hand;
            btnPlayStop.Click += btnPlayStop_Click;

            // Configuration Group
            grpConfig = new System.Windows.Forms.GroupBox();
            grpConfig.Text = "⚙ Configuration";
            grpConfig.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);

            lblIpAddress = new System.Windows.Forms.Label { Text = "Adresse IP:", AutoSize = true };
            txtIpAddress = new System.Windows.Forms.TextBox { Width = 150 };
            txtIpAddress.TextChanged += OnConfigChanged;

            lblPort = new System.Windows.Forms.Label { Text = "Port:", AutoSize = true };
            numPort = new System.Windows.Forms.NumericUpDown();
            numPort.Minimum = 1;
            numPort.Maximum = 65535;
            numPort.Width = 80;
            numPort.ValueChanged += OnConfigChanged;

            radioMulticast = new System.Windows.Forms.RadioButton { Text = "Multicast", AutoSize = true };
            radioMulticast.CheckedChanged += OnConfigChanged;
            radioUnicast = new System.Windows.Forms.RadioButton { Text = "Unicast", AutoSize = true };
            radioUnicast.CheckedChanged += OnConfigChanged;

            chkAutoDetectFormat = new System.Windows.Forms.CheckBox { Text = "Auto-detect format", AutoSize = true, Checked = true };
            chkAutoDetectFormat.CheckedChanged += chkAutoDetectFormat_CheckedChanged;
            
            lblDetectedFormat = new System.Windows.Forms.Label { Text = "Format détecté: --", AutoSize = true, ForeColor = System.Drawing.Color.DarkGreen };
            lblDetectedFormat.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic);

            lblBitWidth = new System.Windows.Forms.Label { Text = "Bits:", AutoSize = true };
            numBitWidth = new System.Windows.Forms.NumericUpDown();
            numBitWidth.Minimum = 16;
            numBitWidth.Maximum = 32;
            numBitWidth.Increment = 8;
            numBitWidth.Width = 60;
            numBitWidth.ValueChanged += OnConfigChanged;

            lblSampleRate = new System.Windows.Forms.Label { Text = "Freq (Hz):", AutoSize = true };
            numSampleRate = new System.Windows.Forms.NumericUpDown();
            numSampleRate.Minimum = 8000;
            numSampleRate.Maximum = 192000;
            numSampleRate.Increment = 100;
            numSampleRate.Width = 80;
            numSampleRate.ValueChanged += OnConfigChanged;

            lblChannels = new System.Windows.Forms.Label { Text = "Canaux:", AutoSize = true };
            numChannels = new System.Windows.Forms.NumericUpDown();
            numChannels.Minimum = 1;
            numChannels.Maximum = 8;
            numChannels.Width = 60;
            numChannels.ValueChanged += OnConfigChanged;

            chkAutoBuffer = new System.Windows.Forms.CheckBox { Text = "Buffer Auto", AutoSize = true, Checked = true };
            chkAutoBuffer.CheckedChanged += chkAutoBuffer_CheckedChanged;
            numBufferDuration = new System.Windows.Forms.NumericUpDown();
            numBufferDuration.Minimum = 1;
            numBufferDuration.Maximum = 1000;
            numBufferDuration.Width = 60;
            numBufferDuration.ValueChanged += OnConfigChanged;

            chkAutoWasapi = new System.Windows.Forms.CheckBox { Text = "WASAPI Auto", AutoSize = true, Checked = true };
            chkAutoWasapi.CheckedChanged += chkAutoWasapi_CheckedChanged;
            numWasapiLatency = new System.Windows.Forms.NumericUpDown();
            numWasapiLatency.Minimum = 1;
            numWasapiLatency.Maximum = 1000;
            numWasapiLatency.Width = 60;
            numWasapiLatency.ValueChanged += OnConfigChanged;

            chkExclusiveMode = new System.Windows.Forms.CheckBox { Text = "Mode Exclusif", AutoSize = true };
            chkExclusiveMode.CheckedChanged += OnConfigChanged;

            // Volume
            lblVolumeLabel = new System.Windows.Forms.Label { Text = "🔊 Volume:", AutoSize = true };
            trackBarVolume = new System.Windows.Forms.TrackBar();
            trackBarVolume.Minimum = 0;
            trackBarVolume.Maximum = 100;
            trackBarVolume.Value = 50;
            trackBarVolume.TickFrequency = 10;
            trackBarVolume.Width = 200;
            trackBarVolume.Scroll += trackBarVolume_Scroll;
            lblVolume = new System.Windows.Forms.Label { Text = "50%", AutoSize = true };

            // Stats Group
            grpStats = new System.Windows.Forms.GroupBox();
            grpStats.Text = "📊 Statistiques en temps réel";
            grpStats.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);

            lblConnectionStatusLabel = new System.Windows.Forms.Label { Text = "État:", AutoSize = true };
            lblConnectionStatus = new System.Windows.Forms.Label { Text = "✗ Déconnecté", AutoSize = true, ForeColor = System.Drawing.Color.Gray };

            lblRemoteEndpointLabel = new System.Windows.Forms.Label { Text = "Source:", AutoSize = true };
            lblRemoteEndpoint = new System.Windows.Forms.Label { Text = "-", AutoSize = true };

            lblAudioFormatLabel = new System.Windows.Forms.Label { Text = "Format:", AutoSize = true };
            lblAudioFormat = new System.Windows.Forms.Label { Text = "-", AutoSize = true };

            lblPacketsReceivedLabel = new System.Windows.Forms.Label { Text = "Paquets:", AutoSize = true };
            lblPacketsReceived = new System.Windows.Forms.Label { Text = "-", AutoSize = true };

            lblBitrateLabel = new System.Windows.Forms.Label { Text = "Débit:", AutoSize = true };
            lblBitrate = new System.Windows.Forms.Label { Text = "-", AutoSize = true };

            lblTotalLatencyLabel = new System.Windows.Forms.Label { Text = "Latence:", AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold) };
            lblTotalLatency = new System.Windows.Forms.Label { Text = "-", AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold) };

            // Network Buffer
            grpNetworkBuffer = new System.Windows.Forms.GroupBox { Text = "📦 Buffer Réseau" };
            progressNetworkBuffer = new System.Windows.Forms.ProgressBar { Width = 300, Height = 25 };
            lblNetworkBuffer = new System.Windows.Forms.Label { Text = "-", AutoSize = true };
            lblNetworkBufferPercent = new System.Windows.Forms.Label { Text = "-", AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold) };
            lblNetworkBufferStatus = new System.Windows.Forms.Label { Text = "-", AutoSize = true };

            // WASAPI Buffer
            grpWasapiBuffer = new System.Windows.Forms.GroupBox { Text = "🎵 Buffer WASAPI" };
            progressWasapiBuffer = new System.Windows.Forms.ProgressBar { Width = 300, Height = 25 };
            lblWasapiBuffer = new System.Windows.Forms.Label { Text = "-", AutoSize = true };
            lblWasapiBufferPercent = new System.Windows.Forms.Label { Text = "-", AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold) };
            lblWasapiBufferStatus = new System.Windows.Forms.Label { Text = "-", AutoSize = true };

            // Performance
            lblUnderrunsLabel = new System.Windows.Forms.Label { Text = "Sous-alimentations:", AutoSize = true };
            lblUnderruns = new System.Windows.Forms.Label { Text = "0", AutoSize = true, ForeColor = System.Drawing.Color.Green };
            lblLastUnderrunLabel = new System.Windows.Forms.Label { Text = "Dernière:", AutoSize = true };
            lblLastUnderrun = new System.Windows.Forms.Label { Text = "-", AutoSize = true };

            // Logs
            grpLogs = new System.Windows.Forms.GroupBox();
            grpLogs.Text = "📋 Journaux";
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
            cmbLogLevel.SelectedIndex = 1; // Info by default
            cmbLogLevel.SelectedIndexChanged += cmbLogLevel_SelectedIndexChanged;

            btnClearLogs = new System.Windows.Forms.Button { Text = "Effacer", Width = 80 };
            btnClearLogs.Click += btnClearLogs_Click;
        }

        private void LayoutControls()
        {
            int margin = 10;
            int x = margin, y = margin;

            // Play button top center
            btnPlayStop.Location = new System.Drawing.Point((this.ClientSize.Width - btnPlayStop.Width) / 2, y);
            btnPlayStop.Anchor = System.Windows.Forms.AnchorStyles.Top;  // Reste centré lors du resize
            this.Controls.Add(btnPlayStop);
            y += btnPlayStop.Height + margin;

            // Configuration group (left side)
            grpConfig.Location = new System.Drawing.Point(margin, y);
            grpConfig.Size = new System.Drawing.Size(350, 380);  // Augmenté de 350 à 380 pour plus d'espace
            grpConfig.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;  // Fixe en haut à gauche
            
            int gy = 25;
            int lineHeight = (int)(30 * (this.AutoScaleDimensions.Height / 16F));  // Ajuste en fonction du DPI
            
            lblIpAddress.Location = new System.Drawing.Point(10, gy);
            txtIpAddress.Location = new System.Drawing.Point(100, gy);
            gy += lineHeight;
            
            lblPort.Location = new System.Drawing.Point(10, gy);
            numPort.Location = new System.Drawing.Point(100, gy);
            radioMulticast.Location = new System.Drawing.Point(190, gy);
            radioUnicast.Location = new System.Drawing.Point(270, gy);
            gy += lineHeight + 5;

            chkAutoDetectFormat.Location = new System.Drawing.Point(10, gy);
            lblDetectedFormat.Location = new System.Drawing.Point(160, gy + 2);
            gy += lineHeight;

            lblBitWidth.Location = new System.Drawing.Point(10, gy);
            numBitWidth.Location = new System.Drawing.Point(100, gy);
            lblSampleRate.Location = new System.Drawing.Point(170, gy);
            numSampleRate.Location = new System.Drawing.Point(250, gy);
            gy += lineHeight;

            lblChannels.Location = new System.Drawing.Point(10, gy);
            numChannels.Location = new System.Drawing.Point(100, gy);
            gy += lineHeight + 10;  // Plus d'espace avant la section buffer

            chkAutoBuffer.Location = new System.Drawing.Point(10, gy);
            numBufferDuration.Location = new System.Drawing.Point(120, gy);
            gy += lineHeight;

            chkAutoWasapi.Location = new System.Drawing.Point(10, gy);
            numWasapiLatency.Location = new System.Drawing.Point(120, gy);
            gy += lineHeight;

            chkExclusiveMode.Location = new System.Drawing.Point(10, gy);
            gy += lineHeight + 5;

            lblVolumeLabel.Location = new System.Drawing.Point(10, gy);
            trackBarVolume.Location = new System.Drawing.Point(80, gy - 5);
            lblVolume.Location = new System.Drawing.Point(290, gy);

            grpConfig.Controls.AddRange(new System.Windows.Forms.Control[] {
                lblIpAddress, txtIpAddress, lblPort, numPort, radioMulticast, radioUnicast,
                chkAutoDetectFormat, lblDetectedFormat,
                lblBitWidth, numBitWidth, lblSampleRate, numSampleRate, lblChannels, numChannels,
                chkAutoBuffer, numBufferDuration, chkAutoWasapi, numWasapiLatency, chkExclusiveMode,
                lblVolumeLabel, trackBarVolume, lblVolume
            });
            this.Controls.Add(grpConfig);

            // Stats group (right side)
            grpStats.Location = new System.Drawing.Point(370, y);
            grpStats.Size = new System.Drawing.Size(this.ClientSize.Width - 370 - margin, 380);  // Augmenté de 350 à 380
            grpStats.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;  // S'étend horizontalement

            int sy = 25;
            lblConnectionStatusLabel.Location = new System.Drawing.Point(10, sy);
            lblConnectionStatus.Location = new System.Drawing.Point(120, sy);
            sy += 25;

            lblRemoteEndpointLabel.Location = new System.Drawing.Point(10, sy);
            lblRemoteEndpoint.Location = new System.Drawing.Point(120, sy);
            sy += 25;

            lblAudioFormatLabel.Location = new System.Drawing.Point(10, sy);
            lblAudioFormat.Location = new System.Drawing.Point(120, sy);
            sy += 25;

            lblPacketsReceivedLabel.Location = new System.Drawing.Point(10, sy);
            lblPacketsReceived.Location = new System.Drawing.Point(120, sy);
            sy += 25;

            lblBitrateLabel.Location = new System.Drawing.Point(10, sy);
            lblBitrate.Location = new System.Drawing.Point(120, sy);
            sy += 25;

            lblTotalLatencyLabel.Location = new System.Drawing.Point(10, sy);
            lblTotalLatency.Location = new System.Drawing.Point(120, sy);
            sy += 35;

            // Network buffer
            grpNetworkBuffer.Location = new System.Drawing.Point(10, sy);
            grpNetworkBuffer.Size = new System.Drawing.Size((grpStats.Width - 30) / 2, 80);  // Largeur dynamique (50% de grpStats)
            grpNetworkBuffer.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            progressNetworkBuffer.Location = new System.Drawing.Point(10, 20);
            progressNetworkBuffer.Width = grpNetworkBuffer.Width - 20;
            progressNetworkBuffer.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            lblNetworkBuffer.Location = new System.Drawing.Point(10, 50);
            lblNetworkBufferPercent.Location = new System.Drawing.Point(120, 50);
            lblNetworkBufferStatus.Location = new System.Drawing.Point(180, 50);
            grpNetworkBuffer.Controls.AddRange(new System.Windows.Forms.Control[] { 
                progressNetworkBuffer, lblNetworkBuffer, lblNetworkBufferPercent, lblNetworkBufferStatus 
            });

            // WASAPI buffer
            grpWasapiBuffer.Location = new System.Drawing.Point(grpNetworkBuffer.Right + 10, sy);
            grpWasapiBuffer.Size = new System.Drawing.Size((grpStats.Width - 30) / 2, 80);  // Largeur dynamique (50% de grpStats)
            grpWasapiBuffer.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            progressWasapiBuffer.Location = new System.Drawing.Point(10, 20);
            progressWasapiBuffer.Width = grpWasapiBuffer.Width - 20;
            progressWasapiBuffer.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            lblWasapiBuffer.Location = new System.Drawing.Point(10, 50);
            lblWasapiBufferPercent.Location = new System.Drawing.Point(120, 50);
            lblWasapiBufferStatus.Location = new System.Drawing.Point(180, 50);
            grpWasapiBuffer.Controls.AddRange(new System.Windows.Forms.Control[] { 
                progressWasapiBuffer, lblWasapiBuffer, lblWasapiBufferPercent, lblWasapiBufferStatus 
            });

            sy += 90;

            lblUnderrunsLabel.Location = new System.Drawing.Point(10, sy);
            lblUnderruns.Location = new System.Drawing.Point(150, sy);
            lblLastUnderrunLabel.Location = new System.Drawing.Point(250, sy);
            lblLastUnderrun.Location = new System.Drawing.Point(320, sy);

            grpStats.Controls.AddRange(new System.Windows.Forms.Control[] {
                lblConnectionStatusLabel, lblConnectionStatus, lblRemoteEndpointLabel, lblRemoteEndpoint,
                lblAudioFormatLabel, lblAudioFormat, lblPacketsReceivedLabel, lblPacketsReceived,
                lblBitrateLabel, lblBitrate, lblTotalLatencyLabel, lblTotalLatency,
                grpNetworkBuffer, grpWasapiBuffer,
                lblUnderrunsLabel, lblUnderruns, lblLastUnderrunLabel, lblLastUnderrun
            });
            this.Controls.Add(grpStats);

            // Logs group (bottom, full width)
            y += 390;  // Augmenté de 360 à 390 pour correspondre à la nouvelle hauteur des groupes
            grpLogs.Location = new System.Drawing.Point(margin, y);
            grpLogs.Size = new System.Drawing.Size(this.ClientSize.Width - margin * 2, this.ClientSize.Height - y - margin);
            grpLogs.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right | 
                            System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom;

            cmbLogLevel.Location = new System.Drawing.Point(10, 25);
            cmbLogLevel.Width = 100;
            btnClearLogs.Location = new System.Drawing.Point(120, 25);

            txtLogs.Location = new System.Drawing.Point(10, 60);
            txtLogs.Size = new System.Drawing.Size(grpLogs.Width - 20, grpLogs.Height - 70);
            txtLogs.Anchor = System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right | 
                            System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom;

            grpLogs.Controls.AddRange(new System.Windows.Forms.Control[] { cmbLogLevel, btnClearLogs, txtLogs });
            this.Controls.Add(grpLogs);
        }
    }
}
