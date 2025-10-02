using System;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Windows.Forms;

namespace ScreamReader
{
    /// <summary>
    /// Modern control panel for ScreamReader with streaming controls and statistics
    /// </summary>
    public partial class ControlPanel : Form
    {
        private UdpWaveStreamPlayer udpPlayer;
        private ScreamReaderTray screamReaderTray;
        private Timer statsRefreshTimer;

        // Control elements
        private Button startStopButton;
        private Button pauseResumeButton;
        private TrackBar volumeSlider;
        private Label volumeLabel;
        private Label statusLabel;
        private Panel statsPanel;
        private Label bufferStatsLabel;
        private ProgressBar bufferFillBar;
        private Label networkStatsLabel;
        private Label audioStatsLabel;
        
        // Configuration controls
        private GroupBox configGroupBox;
        private TextBox ipAddressTextBox;
        private TextBox portTextBox;
        private ComboBox modeComboBox;
        private ComboBox bitWidthComboBox;
        private ComboBox sampleRateComboBox;
        private ComboBox channelsComboBox;
        private Button applyConfigButton;
        
        private bool isPlaying = true;
        private bool isPaused = false;

        public ControlPanel(ScreamReaderTray tray)
        {
            this.screamReaderTray = tray;
            this.udpPlayer = tray.udpPlayer;
            
            InitializeComponent();
            SetupControls();
            SetupStatsTimer();
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Text = "ScreamReader Control Panel";
            this.Size = new Size(650, 550);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(240, 240, 245);
        }

        private void SetupControls()
        {
            // Main layout
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10),
                BackColor = Color.FromArgb(240, 240, 245)
            };

            // Menu strip
            var menuStrip = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add("View Logs", null, (s, e) => LogManager.ShowLogWindow());
            fileMenu.DropDownItems.Add("-");
            fileMenu.DropDownItems.Add("Exit", null, (s, e) => { this.screamReaderTray.Close(); Application.Exit(); });
            
            var helpMenu = new ToolStripMenuItem("Help");
            helpMenu.DropDownItems.Add("About", null, (s, e) => { new About().ShowDialog(this); });
            
            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(helpMenu);

            // Control panel
            var controlPanel = CreateControlPanel();
            
            // Statistics panel
            statsPanel = CreateStatsPanel();
            
            // Configuration panel
            configGroupBox = CreateConfigPanel();

            // Add all sections
            mainPanel.Controls.Add(menuStrip, 0, 0);
            mainPanel.Controls.Add(controlPanel, 0, 1);
            mainPanel.Controls.Add(statsPanel, 0, 2);
            mainPanel.Controls.Add(configGroupBox, 0, 3);

            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            this.Controls.Add(mainPanel);

            // Form closing event
            this.FormClosing += (sender, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    this.Hide();
                }
            };
        }

        private Panel CreateControlPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            // Status label
            statusLabel = new Label
            {
                Text = "● Streaming",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 200, 0),
                AutoSize = true,
                Location = new Point(10, 10)
            };

            // Start/Stop button
            startStopButton = new Button
            {
                Text = "Stop",
                Size = new Size(100, 35),
                Location = new Point(10, 45),
                BackColor = Color.FromArgb(220, 50, 50),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            startStopButton.FlatAppearance.BorderSize = 0;
            startStopButton.Click += StartStopButton_Click;

            // Pause/Resume button
            pauseResumeButton = new Button
            {
                Text = "Pause",
                Size = new Size(100, 35),
                Location = new Point(120, 45),
                BackColor = Color.FromArgb(50, 120, 200),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            pauseResumeButton.FlatAppearance.BorderSize = 0;
            pauseResumeButton.Click += PauseResumeButton_Click;

            // Volume label
            var volumeTitleLabel = new Label
            {
                Text = "Volume:",
                AutoSize = true,
                Location = new Point(10, 95),
                Font = new Font("Segoe UI", 10)
            };

            // Volume slider
            volumeSlider = new TrackBar
            {
                Location = new Point(80, 90),
                Size = new Size(450, 45),
                Minimum = 0,
                Maximum = 100,
                TickFrequency = 10,
                Value = udpPlayer.Volume
            };
            volumeSlider.ValueChanged += VolumeSlider_ValueChanged;

            // Volume percentage label
            volumeLabel = new Label
            {
                Text = $"{udpPlayer.Volume}%",
                Location = new Point(540, 95),
                Size = new Size(60, 20),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight
            };

            panel.Controls.Add(statusLabel);
            panel.Controls.Add(startStopButton);
            panel.Controls.Add(pauseResumeButton);
            panel.Controls.Add(volumeTitleLabel);
            panel.Controls.Add(volumeSlider);
            panel.Controls.Add(volumeLabel);

            return panel;
        }

        private Panel CreateStatsPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            var titleLabel = new Label
            {
                Text = "Buffer Statistics",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(10, 10)
            };

            bufferFillBar = new ProgressBar
            {
                Location = new Point(10, 40),
                Size = new Size(590, 25),
                Minimum = 0,
                Maximum = 100,
                Style = ProgressBarStyle.Continuous
            };

            bufferStatsLabel = new Label
            {
                Text = "Initializing...",
                Location = new Point(10, 70),
                Size = new Size(590, 20),
                Font = new Font("Consolas", 9)
            };

            networkStatsLabel = new Label
            {
                Text = "Network: Waiting for data...",
                Location = new Point(10, 95),
                Size = new Size(590, 20),
                Font = new Font("Consolas", 9)
            };

            audioStatsLabel = new Label
            {
                Text = "Audio: Not configured",
                Location = new Point(10, 120),
                Size = new Size(590, 20),
                Font = new Font("Consolas", 9)
            };

            var autoDetectLabel = new Label
            {
                Text = "📡 Auto-detection: Active",
                Location = new Point(10, 145),
                Size = new Size(590, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(0, 120, 200)
            };

            panel.Controls.Add(titleLabel);
            panel.Controls.Add(bufferFillBar);
            panel.Controls.Add(bufferStatsLabel);
            panel.Controls.Add(networkStatsLabel);
            panel.Controls.Add(audioStatsLabel);
            panel.Controls.Add(autoDetectLabel);

            return panel;
        }

        private GroupBox CreateConfigPanel()
        {
            var groupBox = new GroupBox
            {
                Text = "Stream Configuration",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Padding = new Padding(10)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 3,
                Padding = new Padding(5)
            };

            // IP Address
            layout.Controls.Add(new Label { Text = "IP Address:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            ipAddressTextBox = new TextBox { Width = 120, Text = "239.255.77.77" };
            layout.Controls.Add(ipAddressTextBox, 1, 0);

            // Port
            layout.Controls.Add(new Label { Text = "Port:", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
            portTextBox = new TextBox { Width = 80, Text = "4010" };
            layout.Controls.Add(portTextBox, 3, 0);

            // Mode
            layout.Controls.Add(new Label { Text = "Mode:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            modeComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
            modeComboBox.Items.AddRange(new object[] { "Multicast", "Unicast" });
            modeComboBox.SelectedIndex = 0;
            layout.Controls.Add(modeComboBox, 1, 1);

            // Bit Width
            layout.Controls.Add(new Label { Text = "Bit Width:", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 1);
            bitWidthComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
            bitWidthComboBox.Items.AddRange(new object[] { "16", "24", "32" });
            bitWidthComboBox.SelectedIndex = 0;
            layout.Controls.Add(bitWidthComboBox, 3, 1);

            // Sample Rate
            layout.Controls.Add(new Label { Text = "Sample Rate:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            sampleRateComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
            sampleRateComboBox.Items.AddRange(new object[] { "44100", "48000", "96000", "192000" });
            sampleRateComboBox.SelectedIndex = 0;
            layout.Controls.Add(sampleRateComboBox, 1, 2);

            // Channels
            layout.Controls.Add(new Label { Text = "Channels:", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 2);
            channelsComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
            channelsComboBox.Items.AddRange(new object[] { "1 (Mono)", "2 (Stereo)", "6 (5.1)", "8 (7.1)" });
            channelsComboBox.SelectedIndex = 1;
            layout.Controls.Add(channelsComboBox, 3, 2);

            // Apply button
            applyConfigButton = new Button
            {
                Text = "Apply & Restart",
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(0, 120, 200),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Enabled = false,
                Cursor = Cursors.Hand
            };
            applyConfigButton.FlatAppearance.BorderSize = 0;
            applyConfigButton.Click += ApplyConfigButton_Click;

            var infoLabel = new Label
            {
                Text = "⚠️ Configuration changes require restart. Most parameters are auto-detected from stream.",
                AutoSize = false,
                Size = new Size(450, 30),
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.Gray
            };

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.LeftToRight,
                Height = 40,
                WrapContents = false
            };
            buttonPanel.Controls.Add(applyConfigButton);
            buttonPanel.Controls.Add(infoLabel);

            groupBox.Controls.Add(layout);
            groupBox.Controls.Add(buttonPanel);

            return groupBox;
        }

        private void SetupStatsTimer()
        {
            statsRefreshTimer = new Timer
            {
                Interval = 500 // Update every 500ms
            };
            statsRefreshTimer.Tick += StatsRefreshTimer_Tick;
            statsRefreshTimer.Start();
        }

        private void StartStopButton_Click(object sender, EventArgs e)
        {
            if (isPlaying)
            {
                // Stop streaming
                udpPlayer.Stop();
                isPlaying = false;
                isPaused = false;
                startStopButton.Text = "Start";
                startStopButton.BackColor = Color.FromArgb(0, 150, 0);
                pauseResumeButton.Enabled = false;
                statusLabel.Text = "● Stopped";
                statusLabel.ForeColor = Color.Red;
                LogManager.Log("[ControlPanel] Streaming stopped by user");
            }
            else
            {
                // Start streaming
                udpPlayer.Start();
                isPlaying = true;
                isPaused = false;
                startStopButton.Text = "Stop";
                startStopButton.BackColor = Color.FromArgb(220, 50, 50);
                pauseResumeButton.Enabled = true;
                pauseResumeButton.Text = "Pause";
                statusLabel.Text = "● Streaming";
                statusLabel.ForeColor = Color.FromArgb(0, 200, 0);
                LogManager.Log("[ControlPanel] Streaming started by user");
            }
        }

        private void PauseResumeButton_Click(object sender, EventArgs e)
        {
            if (isPaused)
            {
                // Resume streaming
                udpPlayer.Start();
                isPaused = false;
                pauseResumeButton.Text = "Pause";
                pauseResumeButton.BackColor = Color.FromArgb(50, 120, 200);
                statusLabel.Text = "● Streaming";
                statusLabel.ForeColor = Color.FromArgb(0, 200, 0);
                LogManager.Log("[ControlPanel] Streaming resumed by user");
            }
            else
            {
                // Pause streaming
                udpPlayer.Stop();
                isPaused = true;
                pauseResumeButton.Text = "Resume";
                pauseResumeButton.BackColor = Color.FromArgb(0, 150, 0);
                statusLabel.Text = "● Paused";
                statusLabel.ForeColor = Color.Orange;
                LogManager.Log("[ControlPanel] Streaming paused by user");
            }
        }

        private void VolumeSlider_ValueChanged(object sender, EventArgs e)
        {
            udpPlayer.Volume = volumeSlider.Value;
            volumeLabel.Text = $"{volumeSlider.Value}%";
        }

        private void ApplyConfigButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Configuration changes will be implemented in a future update.\n\nCurrently, parameters are auto-detected from the stream.\n\nTo change network settings, restart the application with command-line parameters.", 
                "Feature In Progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void StatsRefreshTimer_Tick(object sender, EventArgs e)
        {
            if (udpPlayer == null) return;

            try
            {
                // Update buffer statistics
                var bufferStats = udpPlayer.GetBufferStatistics();
                if (bufferStats != null)
                {
                    bufferStatsLabel.Text = bufferStats;
                    
                    // Parse fill percentage for progress bar
                    if (bufferStats.Contains("Remplissage:"))
                    {
                        var fillStr = bufferStats.Substring(bufferStats.IndexOf("Remplissage:") + 12);
                        fillStr = fillStr.Substring(0, fillStr.IndexOf("%"));
                        if (int.TryParse(fillStr.Trim(), out int fillPercent))
                        {
                            bufferFillBar.Value = Math.Min(100, Math.Max(0, fillPercent));
                            
                            // Change color based on fill level
                            if (fillPercent < 25)
                                bufferStatsLabel.ForeColor = Color.Red;
                            else if (fillPercent < 50)
                                bufferStatsLabel.ForeColor = Color.Orange;
                            else if (fillPercent > 80)
                                bufferStatsLabel.ForeColor = Color.Purple;
                            else
                                bufferStatsLabel.ForeColor = Color.Green;
                        }
                    }
                }

                // Update network stats
                networkStatsLabel.Text = $"Network: {udpPlayer.GetNetworkInfo()}";
                
                // Update audio stats
                audioStatsLabel.Text = $"Audio: {udpPlayer.GetAudioInfo()}";
            }
            catch
            {
                // Ignore errors during refresh
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                statsRefreshTimer?.Stop();
                statsRefreshTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        private System.ComponentModel.IContainer components = null;
    }
}
