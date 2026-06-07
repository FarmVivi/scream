namespace ScreamReader
{
    partial class AudioReceiverWindow
    {
        /// <summary>Required designer variable.</summary>
        private System.ComponentModel.IContainer components = null;

        // Layout containers
        private System.Windows.Forms.TableLayoutPanel rootLayout;
        private System.Windows.Forms.TableLayoutPanel configLayout;
        private System.Windows.Forms.FlowLayoutPanel modePanel;
        private System.Windows.Forms.FlowLayoutPanel bufferPanel;
        private System.Windows.Forms.FlowLayoutPanel wasapiPanel;
        private System.Windows.Forms.TableLayoutPanel statusLayout;
        private System.Windows.Forms.TableLayoutPanel logsLayout;
        private System.Windows.Forms.FlowLayoutPanel logsTopBar;

        // Configuration
        private System.Windows.Forms.GroupBox grpConfig;
        private System.Windows.Forms.Label lblIp;
        private System.Windows.Forms.TextBox txtIp;
        private System.Windows.Forms.Label lblPort;
        private System.Windows.Forms.NumericUpDown numPort;
        private System.Windows.Forms.RadioButton radioMulticast;
        private System.Windows.Forms.RadioButton radioUnicast;
        private System.Windows.Forms.Label lblFormatHeader;
        private System.Windows.Forms.CheckBox chkAutoFormat;
        private System.Windows.Forms.Label lblBits;
        private System.Windows.Forms.NumericUpDown numBits;
        private System.Windows.Forms.Label lblRate;
        private System.Windows.Forms.NumericUpDown numRate;
        private System.Windows.Forms.Label lblChannels;
        private System.Windows.Forms.NumericUpDown numChannels;
        private System.Windows.Forms.Label lblBufferingHeader;
        private System.Windows.Forms.CheckBox chkAutoBuffer;
        private System.Windows.Forms.NumericUpDown numBuffer;
        private System.Windows.Forms.Label lblBufferMs;
        private System.Windows.Forms.CheckBox chkAutoWasapi;
        private System.Windows.Forms.NumericUpDown numWasapi;
        private System.Windows.Forms.Label lblWasapiMs;
        private System.Windows.Forms.CheckBox chkExclusive;
        private System.Windows.Forms.Button btnPlayStop;

        // Status
        private System.Windows.Forms.GroupBox grpStatus;
        private System.Windows.Forms.Label lblCapStatus;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label lblCapEndpoint;
        private System.Windows.Forms.Label lblEndpoint;
        private System.Windows.Forms.Label lblCapFormat;
        private System.Windows.Forms.Label lblFormat;
        private System.Windows.Forms.Label lblCapPackets;
        private System.Windows.Forms.Label lblPackets;
        private System.Windows.Forms.Label lblCapBitrate;
        private System.Windows.Forms.Label lblBitrate;
        private System.Windows.Forms.Label lblCapLatency;
        private System.Windows.Forms.Label lblLatency;
        private System.Windows.Forms.Label lblCapUnderruns;
        private System.Windows.Forms.Label lblUnderruns;
        private System.Windows.Forms.Label lblCapBuffer;
        private System.Windows.Forms.ProgressBar progressBuffer;
        private System.Windows.Forms.Label lblBufferText;

        // Logs
        private System.Windows.Forms.GroupBox grpLogs;
        private System.Windows.Forms.Label lblLogLevel;
        private System.Windows.Forms.ComboBox cmbLogLevel;
        private System.Windows.Forms.Button btnClearLogs;
        private System.Windows.Forms.RichTextBox txtLogs;
        private System.Windows.Forms.ToolTip toolTip;

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
            this.rootLayout = new System.Windows.Forms.TableLayoutPanel();
            this.grpConfig = new System.Windows.Forms.GroupBox();
            this.configLayout = new System.Windows.Forms.TableLayoutPanel();
            this.lblIp = new System.Windows.Forms.Label();
            this.txtIp = new System.Windows.Forms.TextBox();
            this.lblPort = new System.Windows.Forms.Label();
            this.numPort = new System.Windows.Forms.NumericUpDown();
            this.modePanel = new System.Windows.Forms.FlowLayoutPanel();
            this.radioMulticast = new System.Windows.Forms.RadioButton();
            this.radioUnicast = new System.Windows.Forms.RadioButton();
            this.lblFormatHeader = new System.Windows.Forms.Label();
            this.chkAutoFormat = new System.Windows.Forms.CheckBox();
            this.lblBits = new System.Windows.Forms.Label();
            this.numBits = new System.Windows.Forms.NumericUpDown();
            this.lblRate = new System.Windows.Forms.Label();
            this.numRate = new System.Windows.Forms.NumericUpDown();
            this.lblChannels = new System.Windows.Forms.Label();
            this.numChannels = new System.Windows.Forms.NumericUpDown();
            this.lblBufferingHeader = new System.Windows.Forms.Label();
            this.chkAutoBuffer = new System.Windows.Forms.CheckBox();
            this.bufferPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.numBuffer = new System.Windows.Forms.NumericUpDown();
            this.lblBufferMs = new System.Windows.Forms.Label();
            this.chkAutoWasapi = new System.Windows.Forms.CheckBox();
            this.wasapiPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.numWasapi = new System.Windows.Forms.NumericUpDown();
            this.lblWasapiMs = new System.Windows.Forms.Label();
            this.chkExclusive = new System.Windows.Forms.CheckBox();
            this.btnPlayStop = new System.Windows.Forms.Button();
            this.grpStatus = new System.Windows.Forms.GroupBox();
            this.statusLayout = new System.Windows.Forms.TableLayoutPanel();
            this.lblCapStatus = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lblCapEndpoint = new System.Windows.Forms.Label();
            this.lblEndpoint = new System.Windows.Forms.Label();
            this.lblCapFormat = new System.Windows.Forms.Label();
            this.lblFormat = new System.Windows.Forms.Label();
            this.lblCapPackets = new System.Windows.Forms.Label();
            this.lblPackets = new System.Windows.Forms.Label();
            this.lblCapBitrate = new System.Windows.Forms.Label();
            this.lblBitrate = new System.Windows.Forms.Label();
            this.lblCapLatency = new System.Windows.Forms.Label();
            this.lblLatency = new System.Windows.Forms.Label();
            this.lblCapUnderruns = new System.Windows.Forms.Label();
            this.lblUnderruns = new System.Windows.Forms.Label();
            this.lblCapBuffer = new System.Windows.Forms.Label();
            this.progressBuffer = new System.Windows.Forms.ProgressBar();
            this.lblBufferText = new System.Windows.Forms.Label();
            this.grpLogs = new System.Windows.Forms.GroupBox();
            this.logsLayout = new System.Windows.Forms.TableLayoutPanel();
            this.logsTopBar = new System.Windows.Forms.FlowLayoutPanel();
            this.lblLogLevel = new System.Windows.Forms.Label();
            this.cmbLogLevel = new System.Windows.Forms.ComboBox();
            this.btnClearLogs = new System.Windows.Forms.Button();
            this.txtLogs = new System.Windows.Forms.RichTextBox();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.numPort)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numBits)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numRate)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numChannels)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numBuffer)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numWasapi)).BeginInit();
            this.rootLayout.SuspendLayout();
            this.grpConfig.SuspendLayout();
            this.configLayout.SuspendLayout();
            this.modePanel.SuspendLayout();
            this.bufferPanel.SuspendLayout();
            this.wasapiPanel.SuspendLayout();
            this.grpStatus.SuspendLayout();
            this.statusLayout.SuspendLayout();
            this.grpLogs.SuspendLayout();
            this.logsLayout.SuspendLayout();
            this.logsTopBar.SuspendLayout();
            this.SuspendLayout();
            //
            // rootLayout — 2 columns (config | right), config spans both rows
            //
            this.rootLayout.ColumnCount = 2;
            this.rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 340F));
            this.rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.rootLayout.RowCount = 2;
            this.rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 250F));
            this.rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.rootLayout.Controls.Add(this.grpConfig, 0, 0);
            this.rootLayout.Controls.Add(this.grpStatus, 1, 0);
            this.rootLayout.Controls.Add(this.grpLogs, 1, 1);
            this.rootLayout.SetRowSpan(this.grpConfig, 2);
            this.rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rootLayout.Padding = new System.Windows.Forms.Padding(8);
            this.rootLayout.Name = "rootLayout";
            //
            // grpConfig
            //
            this.grpConfig.Controls.Add(this.configLayout);
            this.grpConfig.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpConfig.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            this.grpConfig.Name = "grpConfig";
            this.grpConfig.Text = "Configuration";
            //
            // configLayout
            //
            this.configLayout.ColumnCount = 2;
            this.configLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this.configLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.configLayout.RowCount = 14;
            this.configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize)); // 0 ip
            this.configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize)); // 1 port
            this.configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize)); // 2 mode
            this.configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize)); // 3 format header
            this.configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize)); // 4 auto format
            this.configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize)); // 5 bits
            this.configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize)); // 6 rate
            this.configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize)); // 7 channels
            this.configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize)); // 8 buffering header
            this.configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize)); // 9 buffer
            this.configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize)); // 10 wasapi
            this.configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize)); // 11 exclusive
            this.configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F)); // 12 spacer
            this.configLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize)); // 13 play button
            this.configLayout.Controls.Add(this.lblIp, 0, 0);
            this.configLayout.Controls.Add(this.txtIp, 1, 0);
            this.configLayout.Controls.Add(this.lblPort, 0, 1);
            this.configLayout.Controls.Add(this.numPort, 1, 1);
            this.configLayout.Controls.Add(this.modePanel, 0, 2);
            this.configLayout.Controls.Add(this.lblFormatHeader, 0, 3);
            this.configLayout.Controls.Add(this.chkAutoFormat, 0, 4);
            this.configLayout.Controls.Add(this.lblBits, 0, 5);
            this.configLayout.Controls.Add(this.numBits, 1, 5);
            this.configLayout.Controls.Add(this.lblRate, 0, 6);
            this.configLayout.Controls.Add(this.numRate, 1, 6);
            this.configLayout.Controls.Add(this.lblChannels, 0, 7);
            this.configLayout.Controls.Add(this.numChannels, 1, 7);
            this.configLayout.Controls.Add(this.lblBufferingHeader, 0, 8);
            this.configLayout.Controls.Add(this.chkAutoBuffer, 0, 9);
            this.configLayout.Controls.Add(this.bufferPanel, 1, 9);
            this.configLayout.Controls.Add(this.chkAutoWasapi, 0, 10);
            this.configLayout.Controls.Add(this.wasapiPanel, 1, 10);
            this.configLayout.Controls.Add(this.chkExclusive, 0, 11);
            this.configLayout.Controls.Add(this.btnPlayStop, 0, 13);
            this.configLayout.SetColumnSpan(this.modePanel, 2);
            this.configLayout.SetColumnSpan(this.lblFormatHeader, 2);
            this.configLayout.SetColumnSpan(this.chkAutoFormat, 2);
            this.configLayout.SetColumnSpan(this.lblBufferingHeader, 2);
            this.configLayout.SetColumnSpan(this.chkExclusive, 2);
            this.configLayout.SetColumnSpan(this.btnPlayStop, 2);
            this.configLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.configLayout.Padding = new System.Windows.Forms.Padding(8, 6, 8, 8);
            this.configLayout.Name = "configLayout";
            //
            // lblIp
            //
            this.lblIp.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblIp.AutoSize = true;
            this.lblIp.Text = "IP address";
            //
            // txtIp
            //
            this.txtIp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.txtIp.Margin = new System.Windows.Forms.Padding(3, 4, 0, 4);
            this.txtIp.Name = "txtIp";
            //
            // lblPort
            //
            this.lblPort.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblPort.AutoSize = true;
            this.lblPort.Text = "Port";
            //
            // numPort
            //
            this.numPort.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.numPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            this.numPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numPort.Name = "numPort";
            this.numPort.Size = new System.Drawing.Size(90, 23);
            this.numPort.Value = new decimal(new int[] { 4010, 0, 0, 0 });
            //
            // modePanel
            //
            this.modePanel.AutoSize = true;
            this.modePanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.modePanel.Controls.Add(this.radioMulticast);
            this.modePanel.Controls.Add(this.radioUnicast);
            this.modePanel.Margin = new System.Windows.Forms.Padding(0, 4, 0, 4);
            this.modePanel.Name = "modePanel";
            this.modePanel.WrapContents = false;
            //
            // radioMulticast
            //
            this.radioMulticast.AutoSize = true;
            this.radioMulticast.Text = "Multicast";
            //
            // radioUnicast
            //
            this.radioUnicast.AutoSize = true;
            this.radioUnicast.Margin = new System.Windows.Forms.Padding(12, 3, 3, 3);
            this.radioUnicast.Text = "Unicast";
            //
            // lblFormatHeader
            //
            this.lblFormatHeader.AutoSize = true;
            this.lblFormatHeader.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblFormatHeader.Margin = new System.Windows.Forms.Padding(3, 10, 3, 2);
            this.lblFormatHeader.Text = "Audio format";
            //
            // chkAutoFormat
            //
            this.chkAutoFormat.AutoSize = true;
            this.chkAutoFormat.Text = "Auto-detect from stream";
            //
            // lblBits
            //
            this.lblBits.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblBits.AutoSize = true;
            this.lblBits.Margin = new System.Windows.Forms.Padding(18, 0, 3, 0);
            this.lblBits.Text = "Bit depth";
            //
            // numBits
            //
            this.numBits.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.numBits.Maximum = new decimal(new int[] { 32, 0, 0, 0 });
            this.numBits.Minimum = new decimal(new int[] { 8, 0, 0, 0 });
            this.numBits.Name = "numBits";
            this.numBits.Size = new System.Drawing.Size(70, 23);
            this.numBits.Value = new decimal(new int[] { 16, 0, 0, 0 });
            //
            // lblRate
            //
            this.lblRate.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblRate.AutoSize = true;
            this.lblRate.Margin = new System.Windows.Forms.Padding(18, 0, 3, 0);
            this.lblRate.Text = "Sample rate";
            //
            // numRate
            //
            this.numRate.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.numRate.Increment = new decimal(new int[] { 1000, 0, 0, 0 });
            this.numRate.Maximum = new decimal(new int[] { 192000, 0, 0, 0 });
            this.numRate.Minimum = new decimal(new int[] { 8000, 0, 0, 0 });
            this.numRate.Name = "numRate";
            this.numRate.Size = new System.Drawing.Size(90, 23);
            this.numRate.Value = new decimal(new int[] { 48000, 0, 0, 0 });
            //
            // lblChannels
            //
            this.lblChannels.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblChannels.AutoSize = true;
            this.lblChannels.Margin = new System.Windows.Forms.Padding(18, 0, 3, 0);
            this.lblChannels.Text = "Channels";
            //
            // numChannels
            //
            this.numChannels.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.numChannels.Maximum = new decimal(new int[] { 8, 0, 0, 0 });
            this.numChannels.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numChannels.Name = "numChannels";
            this.numChannels.Size = new System.Drawing.Size(70, 23);
            this.numChannels.Value = new decimal(new int[] { 2, 0, 0, 0 });
            //
            // lblBufferingHeader
            //
            this.lblBufferingHeader.AutoSize = true;
            this.lblBufferingHeader.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblBufferingHeader.Margin = new System.Windows.Forms.Padding(3, 10, 3, 2);
            this.lblBufferingHeader.Text = "Buffering";
            //
            // chkAutoBuffer
            //
            this.chkAutoBuffer.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.chkAutoBuffer.AutoSize = true;
            this.chkAutoBuffer.Text = "Auto buffer";
            //
            // bufferPanel
            //
            this.bufferPanel.AutoSize = true;
            this.bufferPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.bufferPanel.Controls.Add(this.numBuffer);
            this.bufferPanel.Controls.Add(this.lblBufferMs);
            this.bufferPanel.Margin = new System.Windows.Forms.Padding(0, 1, 0, 1);
            this.bufferPanel.Name = "bufferPanel";
            this.bufferPanel.WrapContents = false;
            //
            // numBuffer
            //
            this.numBuffer.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            this.numBuffer.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numBuffer.Name = "numBuffer";
            this.numBuffer.Size = new System.Drawing.Size(70, 23);
            this.numBuffer.Value = new decimal(new int[] { 30, 0, 0, 0 });
            //
            // lblBufferMs
            //
            this.lblBufferMs.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblBufferMs.AutoSize = true;
            this.lblBufferMs.Margin = new System.Windows.Forms.Padding(6, 6, 3, 0);
            this.lblBufferMs.Text = "ms";
            //
            // chkAutoWasapi
            //
            this.chkAutoWasapi.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.chkAutoWasapi.AutoSize = true;
            this.chkAutoWasapi.Text = "Auto WASAPI latency";
            //
            // wasapiPanel
            //
            this.wasapiPanel.AutoSize = true;
            this.wasapiPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.wasapiPanel.Controls.Add(this.numWasapi);
            this.wasapiPanel.Controls.Add(this.lblWasapiMs);
            this.wasapiPanel.Margin = new System.Windows.Forms.Padding(0, 1, 0, 1);
            this.wasapiPanel.Name = "wasapiPanel";
            this.wasapiPanel.WrapContents = false;
            //
            // numWasapi
            //
            this.numWasapi.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            this.numWasapi.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numWasapi.Name = "numWasapi";
            this.numWasapi.Size = new System.Drawing.Size(70, 23);
            this.numWasapi.Value = new decimal(new int[] { 20, 0, 0, 0 });
            //
            // lblWasapiMs
            //
            this.lblWasapiMs.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblWasapiMs.AutoSize = true;
            this.lblWasapiMs.Margin = new System.Windows.Forms.Padding(6, 6, 3, 0);
            this.lblWasapiMs.Text = "ms";
            //
            // chkExclusive
            //
            this.chkExclusive.AutoSize = true;
            this.chkExclusive.Margin = new System.Windows.Forms.Padding(3, 6, 3, 3);
            this.chkExclusive.Text = "Exclusive mode";
            //
            // btnPlayStop
            //
            this.btnPlayStop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnPlayStop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnPlayStop.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.btnPlayStop.ForeColor = System.Drawing.Color.White;
            this.btnPlayStop.Margin = new System.Windows.Forms.Padding(3, 8, 3, 3);
            this.btnPlayStop.MinimumSize = new System.Drawing.Size(0, 46);
            this.btnPlayStop.Name = "btnPlayStop";
            this.btnPlayStop.Text = "Start";
            this.btnPlayStop.UseVisualStyleBackColor = true;
            //
            // grpStatus
            //
            this.grpStatus.Controls.Add(this.statusLayout);
            this.grpStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpStatus.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this.grpStatus.Name = "grpStatus";
            this.grpStatus.Text = "Status";
            //
            // statusLayout
            //
            this.statusLayout.ColumnCount = 2;
            this.statusLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
            this.statusLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.statusLayout.RowCount = 9;
            this.statusLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.statusLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.statusLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.statusLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.statusLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.statusLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.statusLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.statusLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.statusLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.statusLayout.Controls.Add(this.lblCapStatus, 0, 0);
            this.statusLayout.Controls.Add(this.lblStatus, 1, 0);
            this.statusLayout.Controls.Add(this.lblCapEndpoint, 0, 1);
            this.statusLayout.Controls.Add(this.lblEndpoint, 1, 1);
            this.statusLayout.Controls.Add(this.lblCapFormat, 0, 2);
            this.statusLayout.Controls.Add(this.lblFormat, 1, 2);
            this.statusLayout.Controls.Add(this.lblCapPackets, 0, 3);
            this.statusLayout.Controls.Add(this.lblPackets, 1, 3);
            this.statusLayout.Controls.Add(this.lblCapBitrate, 0, 4);
            this.statusLayout.Controls.Add(this.lblBitrate, 1, 4);
            this.statusLayout.Controls.Add(this.lblCapLatency, 0, 5);
            this.statusLayout.Controls.Add(this.lblLatency, 1, 5);
            this.statusLayout.Controls.Add(this.lblCapUnderruns, 0, 6);
            this.statusLayout.Controls.Add(this.lblUnderruns, 1, 6);
            this.statusLayout.Controls.Add(this.lblCapBuffer, 0, 7);
            this.statusLayout.Controls.Add(this.progressBuffer, 1, 7);
            this.statusLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.statusLayout.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);
            this.statusLayout.Name = "statusLayout";
            //
            // status captions and values
            //
            this.lblCapStatus.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblCapStatus.AutoSize = true;
            this.lblCapStatus.Text = "Connection";
            this.lblStatus.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblStatus.AutoSize = true;
            this.lblStatus.Text = "-";
            this.lblCapEndpoint.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblCapEndpoint.AutoSize = true;
            this.lblCapEndpoint.Text = "Source";
            this.lblEndpoint.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblEndpoint.AutoSize = true;
            this.lblEndpoint.Text = "-";
            this.lblCapFormat.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblCapFormat.AutoSize = true;
            this.lblCapFormat.Text = "Format";
            this.lblFormat.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblFormat.AutoSize = true;
            this.lblFormat.Text = "-";
            this.lblCapPackets.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblCapPackets.AutoSize = true;
            this.lblCapPackets.Text = "Packets";
            this.lblPackets.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblPackets.AutoSize = true;
            this.lblPackets.Text = "-";
            this.lblCapBitrate.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblCapBitrate.AutoSize = true;
            this.lblCapBitrate.Text = "Bitrate";
            this.lblBitrate.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblBitrate.AutoSize = true;
            this.lblBitrate.Text = "-";
            this.lblCapLatency.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblCapLatency.AutoSize = true;
            this.lblCapLatency.Text = "Latency";
            this.lblLatency.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblLatency.AutoSize = true;
            this.lblLatency.Text = "-";
            this.lblCapUnderruns.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblCapUnderruns.AutoSize = true;
            this.lblCapUnderruns.Text = "Glitches";
            this.lblUnderruns.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblUnderruns.AutoSize = true;
            this.lblUnderruns.Text = "0";
            this.lblCapBuffer.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblCapBuffer.AutoSize = true;
            this.lblCapBuffer.Text = "Net buffer";
            //
            // progressBuffer (+ overlaid text via lblBufferText to its right)
            //
            this.progressBuffer.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBuffer.Margin = new System.Windows.Forms.Padding(3, 4, 8, 2);
            this.progressBuffer.Name = "progressBuffer";
            this.progressBuffer.Height = 16;
            this.progressBuffer.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.lblBufferText.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblBufferText.AutoSize = true;
            this.lblBufferText.Margin = new System.Windows.Forms.Padding(3, 0, 3, 4);
            this.lblBufferText.Name = "lblBufferText";
            this.lblBufferText.Text = "-";
            this.statusLayout.Controls.Add(this.lblBufferText, 1, 8);
            //
            // grpLogs
            //
            this.grpLogs.Controls.Add(this.logsLayout);
            this.grpLogs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpLogs.Margin = new System.Windows.Forms.Padding(0);
            this.grpLogs.Name = "grpLogs";
            this.grpLogs.Text = "Logs";
            //
            // logsLayout
            //
            this.logsLayout.ColumnCount = 1;
            this.logsLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.logsLayout.RowCount = 2;
            this.logsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this.logsLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.logsLayout.Controls.Add(this.logsTopBar, 0, 0);
            this.logsLayout.Controls.Add(this.txtLogs, 0, 1);
            this.logsLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logsLayout.Padding = new System.Windows.Forms.Padding(8, 4, 8, 8);
            this.logsLayout.Name = "logsLayout";
            //
            // logsTopBar
            //
            this.logsTopBar.AutoSize = true;
            this.logsTopBar.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.logsTopBar.Controls.Add(this.lblLogLevel);
            this.logsTopBar.Controls.Add(this.cmbLogLevel);
            this.logsTopBar.Controls.Add(this.btnClearLogs);
            this.logsTopBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logsTopBar.Margin = new System.Windows.Forms.Padding(0, 0, 0, 4);
            this.logsTopBar.Name = "logsTopBar";
            this.logsTopBar.WrapContents = false;
            //
            // lblLogLevel
            //
            this.lblLogLevel.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblLogLevel.AutoSize = true;
            this.lblLogLevel.Margin = new System.Windows.Forms.Padding(0, 8, 6, 0);
            this.lblLogLevel.Text = "Log level";
            //
            // cmbLogLevel
            //
            this.cmbLogLevel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbLogLevel.Items.AddRange(new object[] { "Debug", "Info", "Warning", "Error" });
            this.cmbLogLevel.Margin = new System.Windows.Forms.Padding(0, 4, 12, 0);
            this.cmbLogLevel.Name = "cmbLogLevel";
            this.cmbLogLevel.Size = new System.Drawing.Size(120, 23);
            //
            // btnClearLogs
            //
            this.btnClearLogs.AutoSize = true;
            this.btnClearLogs.Margin = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this.btnClearLogs.Name = "btnClearLogs";
            this.btnClearLogs.Text = "Clear logs";
            this.btnClearLogs.UseVisualStyleBackColor = true;
            //
            // txtLogs
            //
            this.txtLogs.BackColor = System.Drawing.Color.Black;
            this.txtLogs.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtLogs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLogs.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtLogs.ForeColor = System.Drawing.Color.White;
            this.txtLogs.Margin = new System.Windows.Forms.Padding(0);
            this.txtLogs.Name = "txtLogs";
            this.txtLogs.ReadOnly = true;
            this.txtLogs.Text = "";
            this.txtLogs.WordWrap = false;
            //
            // AudioReceiverWindow
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(860, 560);
            this.Controls.Add(this.rootLayout);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.Icon = global::ScreamReader.Properties.Resources.speaker_ico;
            this.MinimumSize = new System.Drawing.Size(740, 540);
            this.Name = "AudioReceiverWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ScreamReader";
            ((System.ComponentModel.ISupportInitialize)(this.numPort)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numBits)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numRate)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numChannels)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numBuffer)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numWasapi)).EndInit();
            this.logsTopBar.ResumeLayout(false);
            this.logsTopBar.PerformLayout();
            this.logsLayout.ResumeLayout(false);
            this.logsLayout.PerformLayout();
            this.grpLogs.ResumeLayout(false);
            this.statusLayout.ResumeLayout(false);
            this.statusLayout.PerformLayout();
            this.grpStatus.ResumeLayout(false);
            this.wasapiPanel.ResumeLayout(false);
            this.wasapiPanel.PerformLayout();
            this.bufferPanel.ResumeLayout(false);
            this.bufferPanel.PerformLayout();
            this.modePanel.ResumeLayout(false);
            this.modePanel.PerformLayout();
            this.configLayout.ResumeLayout(false);
            this.configLayout.PerformLayout();
            this.grpConfig.ResumeLayout(false);
            this.rootLayout.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
