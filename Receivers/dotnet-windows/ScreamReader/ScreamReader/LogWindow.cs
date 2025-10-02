using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScreamReader
{
    public partial class LogWindow : Form
    {
        private TextBox logTextBox;
        private Button playPauseButton;
        private Timer autoRefreshTimer;
        private bool isAutoRefreshEnabled = true;

        public LogWindow()
        {
            InitializeComponent();
            SetupControls();
            LoadLogs();
        }

        private void SetupControls()
        {
            this.Text = "ScreamReader Logs";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(400, 300);

            // Create main panel
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };

            // Create button panel
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Height = 40
            };

            // Create play/pause button
            playPauseButton = new Button
            {
                Text = "Pause",
                Size = new Size(80, 30),
                Margin = new Padding(5)
            };
            playPauseButton.Click += PlayPauseButton_Click;

            buttonPanel.Controls.Add(playPauseButton);

            // Create auto-refresh timer
            autoRefreshTimer = new Timer
            {
                Interval = 1000 // Refresh every second
            };
            autoRefreshTimer.Tick += AutoRefreshTimer_Tick;
            autoRefreshTimer.Start();

            // Create log text box
            logTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                BackColor = Color.Black,
                ForeColor = Color.LimeGreen,
                WordWrap = false
            };

            // Add controls to main panel
            mainPanel.Controls.Add(buttonPanel, 0, 0);
            mainPanel.Controls.Add(logTextBox, 0, 1);

            // Set row styles
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            this.Controls.Add(mainPanel);

            // Handle form closing
            this.FormClosing += (sender, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    this.Hide();
                }
                else
                {
                    // Stop timer when form is actually closing
                    autoRefreshTimer?.Stop();
                    autoRefreshTimer?.Dispose();
                }
            };
        }

        private void PlayPauseButton_Click(object sender, EventArgs e)
        {
            isAutoRefreshEnabled = !isAutoRefreshEnabled;
            
            if (isAutoRefreshEnabled)
            {
                playPauseButton.Text = "Pause";
                autoRefreshTimer.Start();
                // Refresh immediately when resuming
                LoadLogs();
            }
            else
            {
                playPauseButton.Text = "Play";
                autoRefreshTimer.Stop();
            }
        }

        private void AutoRefreshTimer_Tick(object sender, EventArgs e)
        {
            if (isAutoRefreshEnabled)
            {
                LoadLogs();
            }
        }

        public void LoadLogs()
        {
            string newLogs = LogManager.GetAllLogs();
            if (logTextBox.Text != newLogs)
            {
                logTextBox.Text = newLogs;
                logTextBox.SelectionStart = logTextBox.Text.Length;
                logTextBox.ScrollToCaret();
            }
        }

        public void AddLog(string logEntry)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AddLog), logEntry);
                return;
            }

            // Only add to text box if auto-refresh is enabled
            if (isAutoRefreshEnabled)
            {
                logTextBox.AppendText(logEntry + Environment.NewLine);
                logTextBox.SelectionStart = logTextBox.Text.Length;
                logTextBox.ScrollToCaret();
            }
        }

        public void ClearLogs()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(ClearLogs));
                return;
            }

            logTextBox.Clear();
        }
    }
}