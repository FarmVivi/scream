using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScreamReader
{
    public partial class LogWindow : Form
    {
        private TextBox logTextBox;
        private Button clearButton;
        private Button saveButton;
        private Button refreshButton;

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

            // Create buttons
            refreshButton = new Button
            {
                Text = "Refresh",
                Size = new Size(80, 30),
                Margin = new Padding(5)
            };
            refreshButton.Click += RefreshButton_Click;

            clearButton = new Button
            {
                Text = "Clear",
                Size = new Size(80, 30),
                Margin = new Padding(5)
            };
            clearButton.Click += ClearButton_Click;

            saveButton = new Button
            {
                Text = "Save Logs",
                Size = new Size(100, 30),
                Margin = new Padding(5)
            };
            saveButton.Click += SaveButton_Click;

            buttonPanel.Controls.Add(refreshButton);
            buttonPanel.Controls.Add(clearButton);
            buttonPanel.Controls.Add(saveButton);

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
            };
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            LoadLogs();
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            LogManager.ClearLogs();
            LoadLogs();
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"ScreamReader_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    System.IO.File.WriteAllText(saveDialog.FileName, logTextBox.Text);
                    MessageBox.Show($"Logs saved to: {saveDialog.FileName}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save logs: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public void LoadLogs()
        {
            logTextBox.Text = LogManager.GetAllLogs();
            logTextBox.SelectionStart = logTextBox.Text.Length;
            logTextBox.ScrollToCaret();
        }

        public void AddLog(string logEntry)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AddLog), logEntry);
                return;
            }

            logTextBox.AppendText(logEntry + Environment.NewLine);
            logTextBox.SelectionStart = logTextBox.Text.Length;
            logTextBox.ScrollToCaret();
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