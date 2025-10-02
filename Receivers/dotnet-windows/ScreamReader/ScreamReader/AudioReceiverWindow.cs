using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net;

namespace ScreamReader
{
    /// <summary>
    /// Buffer health status for UI display
    /// </summary>
    public enum BufferHealth
    {
        Critical,   // < 15%
        Low,        // 15-25%
        Optimal,    // 25-80%
        High        // > 80%
    }

    /// <summary>
    /// Modern main window for ScreamReader with real-time stats and controls
    /// </summary>
    public partial class AudioReceiverWindow : Form
    {
        private UdpWaveStreamPlayer audioPlayer;
        private StreamConfiguration currentConfig;
        private System.Windows.Forms.Timer statsUpdateTimer;
        private bool isPlaying = false;
        private bool isAutoDetectFormat = true;

        public AudioReceiverWindow()
        {
            InitializeComponent();
            InitializeDefaults();
            SetupEventHandlers();
            SetupStatsTimer();
        }

        private void InitializeDefaults()
        {
            // Charger la configuration sauvegardée
            ConfigurationManager.Load();
            
            // Appliquer la configuration
            currentConfig = ConfigurationManager.GetStreamConfiguration();
            LogManager.LogDebug($"[AudioReceiverWindow] Config après GetStreamConfiguration: {currentConfig}");
            UpdateConfigUI();
            UpdatePlayButton();
            
            // Appliquer le niveau de log sauvegardé
            LogManager.SetMinimumLevel(ConfigurationManager.MinimumLogLevel);
            
            // Appliquer le volume sauvegardé
            trackBarVolume.Value = ConfigurationManager.Volume;
            
            // Configurer l'auto-détection de format (par défaut true)
            UpdateFormatControlsState();
        }

        private void SetupEventHandlers()
        {
            // Log events
            LogManager.LogAdded += OnLogAdded;

            // Form events
            this.FormClosing += OnFormClosing;
            this.Load += OnFormLoad;
        }

        private void SetupStatsTimer()
        {
            statsUpdateTimer = new System.Windows.Forms.Timer();
            statsUpdateTimer.Interval = 100; // Update every 100ms
            statsUpdateTimer.Tick += OnStatsTimerTick;
            statsUpdateTimer.Start();
        }

        private void OnFormLoad(object sender, EventArgs e)
        {
            // Restaurer la taille et position de la fenêtre
            if (ConfigurationManager.WindowSize.Width > 0 && ConfigurationManager.WindowSize.Height > 0)
            {
                this.Size = ConfigurationManager.WindowSize;
            }
            
            if (ConfigurationManager.WindowLocation != Point.Empty)
            {
                this.Location = ConfigurationManager.WindowLocation;
            }
            
            if (ConfigurationManager.WindowMaximized)
            {
                this.WindowState = FormWindowState.Maximized;
            }
            
            LogManager.Log("ScreamReader Interface chargée");
            LogManager.LogInfo($"Configuration chargée: {currentConfig}");
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                // Minimize to tray instead of closing
                e.Cancel = true;
                this.Hide();
                return;
            }

            // Sauvegarder la configuration avant de fermer
            SaveConfiguration();
            
            // Actual cleanup
            StopAudio();
            
            // Nettoyer le player à la fermeture de l'application
            if (audioPlayer != null)
            {
                audioPlayer.Dispose();
                audioPlayer = null;
            }
            
            statsUpdateTimer?.Stop();
            statsUpdateTimer?.Dispose();
            LogManager.LogAdded -= OnLogAdded;
        }
        
        private void SaveConfiguration()
        {
            // Sauvegarder la position et taille de la fenêtre
            if (this.WindowState == FormWindowState.Normal)
            {
                ConfigurationManager.WindowSize = this.Size;
                ConfigurationManager.WindowLocation = this.Location;
                ConfigurationManager.WindowMaximized = false;
            }
            else if (this.WindowState == FormWindowState.Maximized)
            {
                ConfigurationManager.WindowMaximized = true;
            }
            
            // Sauvegarder la configuration du stream
            ConfigurationManager.UpdateFromStreamConfiguration(currentConfig);
            
            // Sauvegarder le volume
            ConfigurationManager.Volume = trackBarVolume.Value;
            
            // Sauvegarder le niveau de log
            ConfigurationManager.MinimumLogLevel = LogManager.GetMinimumLevel();
            
            // Écrire dans le registre
            ConfigurationManager.Save();
        }

        private void OnLogAdded(object sender, LogEntry entry)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnLogAdded(sender, entry)));
                return;
            }

            AddLogToTextBox(entry);
        }

        private void AddLogToTextBox(LogEntry entry)
        {
            // Add log with color
            int start = txtLogs.TextLength;
            txtLogs.AppendText(entry.ToString() + Environment.NewLine);
            
            txtLogs.SelectionStart = start;
            txtLogs.SelectionLength = entry.ToString().Length;
            txtLogs.SelectionColor = entry.Color;
            txtLogs.SelectionLength = 0;
            txtLogs.SelectionStart = txtLogs.TextLength;
            
            // Auto-scroll
            txtLogs.ScrollToCaret();

            // Keep max 500 lines
            if (txtLogs.Lines.Length > 500)
            {
                var lines = txtLogs.Lines;
                txtLogs.Lines = new string[lines.Length - 100];
                Array.Copy(lines, 100, txtLogs.Lines, 0, lines.Length - 100);
            }
        }

        private void OnStatsTimerTick(object sender, EventArgs e)
        {
            if (audioPlayer == null || !isPlaying) return;

            try
            {
                var stats = audioPlayer.GetCurrentStats();
                if (stats != null)
                {
                    UpdateStatsUI(stats);
                }
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Erreur mise à jour stats: {ex.Message}");
            }
        }

        private void UpdateStatsUI(StreamStats stats)
        {
            // Connection status
            lblConnectionStatus.Text = stats.IsConnected ? "✓ Connecté" : "✗ Déconnecté";
            lblConnectionStatus.ForeColor = stats.IsConnected ? Color.Green : Color.Red;

            if (stats.IsConnected)
            {
                lblRemoteEndpoint.Text = stats.RemoteEndpoint ?? "N/A";
                lblAudioFormat.Text = stats.FormatDescription;
                
                // Mettre à jour le label de format détecté
                if (isAutoDetectFormat)
                {
                    lblDetectedFormat.Text = $"Détecté: {stats.FormatDescription}";
                    lblDetectedFormat.ForeColor = System.Drawing.Color.DarkGreen;
                }
                
                lblPacketsReceived.Text = $"{stats.TotalPacketsReceived:N0} ({stats.PacketsPerSecond:F1}/s)";
                lblBitrate.Text = $"{stats.Bitrate:F1} kbps";
                lblTotalLatency.Text = $"{stats.TotalLatencyMs:F1} ms";

                // Network buffer
                lblNetworkBuffer.Text = $"{stats.NetworkBufferedMs:F1}ms / {stats.NetworkBufferCapacityMs:F0}ms";
                lblNetworkBufferPercent.Text = $"{stats.NetworkBufferFillPercentage:F0}%";
                progressNetworkBuffer.Value = Math.Min(100, Math.Max(0, (int)stats.NetworkBufferFillPercentage));
                
                var networkHealth = GetBufferHealth(stats.NetworkBufferFillPercentage);
                lblNetworkBufferStatus.Text = GetHealthDescription(networkHealth);
                lblNetworkBufferStatus.ForeColor = GetHealthColor(networkHealth);
                progressNetworkBuffer.ForeColor = GetHealthColor(networkHealth);

                // WASAPI buffer
                lblWasapiBuffer.Text = $"{stats.WasapiBufferedMs:F1}ms / {stats.WasapiBufferCapacityMs:F0}ms";
                lblWasapiBufferPercent.Text = $"{stats.WasapiBufferFillPercentage:F0}%";
                progressWasapiBuffer.Value = Math.Min(100, Math.Max(0, (int)stats.WasapiBufferFillPercentage));
                
                var wasapiHealth = GetBufferHealth(stats.WasapiBufferFillPercentage);
                lblWasapiBufferStatus.Text = GetHealthDescription(wasapiHealth);
                lblWasapiBufferStatus.ForeColor = GetHealthColor(wasapiHealth);
                progressWasapiBuffer.ForeColor = GetHealthColor(wasapiHealth);

                // Performance
                lblUnderruns.Text = stats.UnderrunCount.ToString();
                if (stats.UnderrunCount > 0 && stats.LastUnderrun.HasValue)
                {
                    lblLastUnderrun.Text = stats.LastUnderrun.Value.ToString("HH:mm:ss");
                }
            }
        }

        private BufferHealth GetBufferHealth(double fillPercentage)
        {
            if (fillPercentage < 15) return BufferHealth.Critical;
            if (fillPercentage < 25) return BufferHealth.Low;
            if (fillPercentage > 80) return BufferHealth.High;
            return BufferHealth.Optimal;
        }

        private string GetHealthDescription(BufferHealth health)
        {
            switch (health)
            {
                case BufferHealth.Critical: return "CRITIQUE";
                case BufferHealth.Low: return "Bas";
                case BufferHealth.Optimal: return "Optimal";
                case BufferHealth.High: return "Élevé";
                default: return "Inconnu";
            }
        }

        private Color GetHealthColor(BufferHealth health)
        {
            switch (health)
            {
                case BufferHealth.Critical: return Color.Red;
                case BufferHealth.Low: return Color.Orange;
                case BufferHealth.Optimal: return Color.Green;
                case BufferHealth.High: return Color.Blue;
                default: return Color.Gray;
            }
        }

        private void btnPlayStop_Click(object sender, EventArgs e)
        {
            if (isPlaying)
            {
                StopAudio();
            }
            else
            {
                StartAudio();
            }
        }

        private void StartAudio()
        {
            try
            {
                // Lire la configuration depuis l'UI
                ReadConfigFromUI();
                
                // Validate configuration
                if (!currentConfig.IsValid(out string error))
                {
                    MessageBox.Show($"Configuration invalide: {error}", "Erreur", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                LogManager.Log($"Démarrage du flux audio: {currentConfig}");

                // Ne recréer le player que si nécessaire (première fois ou changement de mode multicast/unicast)
                bool needNewPlayer = audioPlayer == null || 
                    (currentConfig.IsMulticast && !(audioPlayer is MulticastUdpWaveStreamPlayer)) ||
                    (!currentConfig.IsMulticast && !(audioPlayer is UnicastUdpWaveStreamPlayer));

                if (needNewPlayer)
                {
                    // Create player based on configuration
                    if (currentConfig.IsMulticast)
                    {
                        audioPlayer = new MulticastUdpWaveStreamPlayer(
                            currentConfig.BitWidth,
                            currentConfig.SampleRate,
                            currentConfig.Channels,
                            currentConfig.Port,
                            currentConfig.IpAddress,
                            currentConfig.IsAutoBuffer ? -1 : currentConfig.BufferDuration,
                            currentConfig.IsAutoWasapi ? -1 : currentConfig.WasapiLatency,
                            currentConfig.UseExclusiveMode,
                            currentConfig.IsAutoDetectFormat
                        );
                    }
                    else
                    {
                        audioPlayer = new UnicastUdpWaveStreamPlayer(
                            currentConfig.BitWidth,
                            currentConfig.SampleRate,
                            currentConfig.Channels,
                            currentConfig.Port,
                            currentConfig.IpAddress,
                            currentConfig.IsAutoBuffer ? -1 : currentConfig.BufferDuration,
                            currentConfig.IsAutoWasapi ? -1 : currentConfig.WasapiLatency,
                            currentConfig.UseExclusiveMode,
                            currentConfig.IsAutoDetectFormat
                        );
                    }
                    LogManager.LogDebug($"[AudioReceiverWindow] Nouveau player créé ({(currentConfig.IsMulticast ? "Multicast" : "Unicast")})");
                }
                else
                {
                    // Réutilisation du player : appliquer les valeurs selon le mode (Auto/Manuel)
                    LogManager.LogDebug("[AudioReceiverWindow] Réutilisation du player existant");
                    
                    // Si mode MANUEL : toujours appliquer la valeur de l'UI
                    // Si mode AUTO : garder les adaptations (ne rien faire)
                    bool isManualMode = !currentConfig.IsAutoBuffer || !currentConfig.IsAutoWasapi;
                    
                    if (isManualMode)
                    {
                        // Mode manuel : appliquer les valeurs de l'UI à chaque Start
                        audioPlayer.UpdateManualSettings(
                            !currentConfig.IsAutoBuffer ? currentConfig.BufferDuration : -1,
                            !currentConfig.IsAutoWasapi ? currentConfig.WasapiLatency : -1
                        );
                        
                        LogManager.LogInfo($"[AudioReceiverWindow] Mode manuel - Valeurs appliquées: Buffer={(!currentConfig.IsAutoBuffer ? currentConfig.BufferDuration + "ms" : "Auto")}, WASAPI={(!currentConfig.IsAutoWasapi ? currentConfig.WasapiLatency + "ms" : "Auto")}");
                    }
                    else
                    {
                        // Mode auto : garder les adaptations existantes
                        LogManager.LogDebug("[AudioReceiverWindow] Mode auto - Adaptations préservées");
                    }
                }

                // Set volume
                audioPlayer.Volume = trackBarVolume.Value;

                // Start playback
                audioPlayer.Start();
                isPlaying = true;
                UpdatePlayButton();

                LogManager.LogInfo("✓ Lecture audio démarrée");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Erreur démarrage audio: {ex.Message}");
                MessageBox.Show($"Impossible de démarrer l'audio:\n{ex.Message}", "Erreur",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopAudio()
        {
            try
            {
                if (audioPlayer != null)
                {
                    LogManager.Log("Arrêt du flux audio...");
                    audioPlayer.Stop();
                    // NE PAS détruire le player pour préserver le bufferManager adaptatif
                    // audioPlayer.Dispose();
                    // audioPlayer = null;
                }

                isPlaying = false;
                UpdatePlayButton();
                ClearStats();

                LogManager.LogInfo("✓ Lecture audio arrêtée (player préservé pour adaptations)");
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Erreur arrêt audio: {ex.Message}");
            }
        }

        private void UpdatePlayButton()
        {
            if (isPlaying)
            {
                btnPlayStop.Text = "⏹ Stop";
                btnPlayStop.BackColor = Color.IndianRed;
                grpConfig.Enabled = false;
            }
            else
            {
                btnPlayStop.Text = "▶ Start";
                btnPlayStop.BackColor = Color.LightGreen;
                grpConfig.Enabled = true;
            }
        }

        private void ClearStats()
        {
            lblConnectionStatus.Text = "✗ Déconnecté";
            lblConnectionStatus.ForeColor = Color.Gray;
            lblRemoteEndpoint.Text = "-";
            lblAudioFormat.Text = "-";
            lblPacketsReceived.Text = "-";
            lblBitrate.Text = "-";
            lblTotalLatency.Text = "-";
            lblNetworkBuffer.Text = "-";
            lblNetworkBufferPercent.Text = "-";
            lblNetworkBufferStatus.Text = "-";
            lblWasapiBuffer.Text = "-";
            lblWasapiBufferPercent.Text = "-";
            lblWasapiBufferStatus.Text = "-";
            lblUnderruns.Text = "0";
            lblLastUnderrun.Text = "-";
            progressNetworkBuffer.Value = 0;
            progressWasapiBuffer.Value = 0;
        }

        private bool isUpdatingUI = false; // Flag pour éviter les événements circulaires

        private void UpdateConfigUI()
        {
            // Désactiver les événements pendant la mise à jour
            isUpdatingUI = true;
            
            try
            {
                txtIpAddress.Text = currentConfig.IpAddress.ToString();
                numPort.Value = currentConfig.Port;
                radioMulticast.Checked = currentConfig.IsMulticast;
                radioUnicast.Checked = !currentConfig.IsMulticast;
                
                // Gérer les valeurs -1 (auto-detect) pour les contrôles de format
                numBitWidth.Value = currentConfig.BitWidth > 0 ? currentConfig.BitWidth : 16;
                numSampleRate.Value = currentConfig.SampleRate > 0 ? currentConfig.SampleRate : 48000;
                numChannels.Value = currentConfig.Channels > 0 ? currentConfig.Channels : 2;
                
                // Auto-detection format
                chkAutoDetectFormat.Checked = currentConfig.IsAutoDetectFormat;
                isAutoDetectFormat = currentConfig.IsAutoDetectFormat;
                UpdateFormatControlsState();
                
                // Auto buffer
                if (currentConfig.BufferDuration == -1 || currentConfig.IsAutoBuffer)
                {
                    chkAutoBuffer.Checked = true;
                    numBufferDuration.Enabled = false;
                    numBufferDuration.Value = 30;
                }
                else
                {
                    chkAutoBuffer.Checked = false;
                    numBufferDuration.Enabled = true;
                    numBufferDuration.Value = currentConfig.BufferDuration;
                }

                // Auto WASAPI
                if (currentConfig.WasapiLatency == -1 || currentConfig.IsAutoWasapi)
                {
                    chkAutoWasapi.Checked = true;
                    numWasapiLatency.Enabled = false;
                    numWasapiLatency.Value = 20;
                }
                else
                {
                    chkAutoWasapi.Checked = false;
                    numWasapiLatency.Enabled = true;
                    numWasapiLatency.Value = currentConfig.WasapiLatency;
                }

                chkExclusiveMode.Checked = currentConfig.UseExclusiveMode;
            }
            finally
            {
                // Réactiver les événements
                isUpdatingUI = false;
            }
        }

        /// <summary>
        /// Lit la configuration depuis l'UI pour la mettre à jour dans currentConfig
        /// </summary>
        private void ReadConfigFromUI()
        {
            try
            {
                currentConfig.IpAddress = IPAddress.Parse(txtIpAddress.Text);
                currentConfig.Port = (int)numPort.Value;
                currentConfig.IsMulticast = radioMulticast.Checked;
                currentConfig.BitWidth = (int)numBitWidth.Value;
                currentConfig.SampleRate = (int)numSampleRate.Value;
                currentConfig.Channels = (int)numChannels.Value;
                currentConfig.IsAutoDetectFormat = chkAutoDetectFormat.Checked;
                currentConfig.IsAutoBuffer = chkAutoBuffer.Checked;
                currentConfig.IsAutoWasapi = chkAutoWasapi.Checked;
                currentConfig.BufferDuration = chkAutoBuffer.Checked ? -1 : (int)numBufferDuration.Value;
                currentConfig.WasapiLatency = chkAutoWasapi.Checked ? -1 : (int)numWasapiLatency.Value;
                currentConfig.UseExclusiveMode = chkExclusiveMode.Checked;
            }
            catch (Exception ex)
            {
                LogManager.LogError($"[AudioReceiverWindow] Erreur lecture config UI: {ex.Message}");
            }
        }

        private void SaveConfigFromUI()
        {
            try
            {
                ReadConfigFromUI();
                
                // Sauvegarder dans le registre
                ConfigurationManager.UpdateFromStreamConfiguration(currentConfig);
                ConfigurationManager.Save();
            }
            catch (Exception ex)
            {
                LogManager.LogError($"Erreur sauvegarde config: {ex.Message}");
            }
        }

        // Configuration change handlers
        private void OnConfigChanged(object sender, EventArgs e)
        {
            // Ignorer si on est en train de mettre à jour l'UI
            if (isUpdatingUI) return;
            
            if (!isPlaying)
            {
                SaveConfigFromUI();
            }
        }

        private void chkAutoBuffer_CheckedChanged(object sender, EventArgs e)
        {
            if (isUpdatingUI) return;
            
            numBufferDuration.Enabled = !chkAutoBuffer.Checked;
            if (chkAutoBuffer.Checked)
            {
                numBufferDuration.Value = 30; // Valeur par défaut
            }
            SaveConfigFromUI();
        }

        private void chkAutoWasapi_CheckedChanged(object sender, EventArgs e)
        {
            if (isUpdatingUI) return;
            
            numWasapiLatency.Enabled = !chkAutoWasapi.Checked;
            if (chkAutoWasapi.Checked)
            {
                numWasapiLatency.Value = 20; // Valeur par défaut
            }
            SaveConfigFromUI();
        }

        private void chkAutoDetectFormat_CheckedChanged(object sender, EventArgs e)
        {
            if (isUpdatingUI) return;
            
            isAutoDetectFormat = chkAutoDetectFormat.Checked;
            UpdateFormatControlsState();
            
            if (isAutoDetectFormat)
            {
                LogManager.LogInfo("Auto-détection de format activée");
                lblDetectedFormat.Text = "Format détecté: en attente...";
                lblDetectedFormat.ForeColor = System.Drawing.Color.Gray;
            }
            else
            {
                LogManager.LogInfo("Configuration manuelle du format");
                lblDetectedFormat.Text = "Mode manuel";
                lblDetectedFormat.ForeColor = System.Drawing.Color.DarkOrange;
            }
            
            // Sauvegarder dans tous les cas
            SaveConfigFromUI();
        }
        
        private void UpdateFormatControlsState()
        {
            // Activer/désactiver les contrôles de format selon le mode
            bool enableManual = !isAutoDetectFormat;
            
            lblBitWidth.Enabled = enableManual;
            numBitWidth.Enabled = enableManual;
            lblSampleRate.Enabled = enableManual;
            numSampleRate.Enabled = enableManual;
            lblChannels.Enabled = enableManual;
            numChannels.Enabled = enableManual;
            
            // Changer la couleur des labels pour indiquer l'état
            Color labelColor = enableManual ? System.Drawing.Color.Black : System.Drawing.Color.Gray;
            lblBitWidth.ForeColor = labelColor;
            lblSampleRate.ForeColor = labelColor;
            lblChannels.ForeColor = labelColor;
        }

        private void trackBarVolume_Scroll(object sender, EventArgs e)
        {
            lblVolume.Text = $"{trackBarVolume.Value}%";
            if (audioPlayer != null)
            {
                audioPlayer.Volume = trackBarVolume.Value;
            }
            
            // Sauvegarder le volume dans le registre
            ConfigurationManager.Volume = trackBarVolume.Value;
            ConfigurationManager.Save();
        }

        private void cmbLogLevel_SelectedIndexChanged(object sender, EventArgs e)
        {
            var level = (LogLevel)cmbLogLevel.SelectedIndex;
            LogManager.SetMinimumLevel(level);
            LogManager.LogInfo($"Niveau de log changé: {level}");
            
            // Sauvegarder le niveau de log dans le registre
            ConfigurationManager.MinimumLogLevel = level;
            ConfigurationManager.Save();
        }

        private void btnClearLogs_Click(object sender, EventArgs e)
        {
            txtLogs.Clear();
            LogManager.Log("Logs effacés");
        }
        
        /// <summary>
        /// Gestion du redimensionnement de la fenêtre pour ajuster les contrôles
        /// </summary>
        private void AudioReceiverWindow_Resize(object sender, EventArgs e)
        {
            // Recentrer le bouton Play/Stop
            if (btnPlayStop != null)
            {
                btnPlayStop.Left = (this.ClientSize.Width - btnPlayStop.Width) / 2;
            }
            
            // Ajuster la largeur de grpStats pour s'adapter à la largeur de la fenêtre
            if (grpStats != null && grpConfig != null)
            {
                int margin = 10;
                grpStats.Width = this.ClientSize.Width - grpConfig.Right - margin * 2;
            }
            
            // Ajuster les buffer boxes pour être côte à côte de manière égale
            if (grpNetworkBuffer != null && grpWasapiBuffer != null && grpStats != null)
            {
                int bufferWidth = (grpStats.Width - 30) / 2;
                grpNetworkBuffer.Width = bufferWidth;
                grpWasapiBuffer.Width = bufferWidth;
                grpWasapiBuffer.Left = grpNetworkBuffer.Right + 10;
                
                // Ajuster la largeur des progress bars
                if (progressNetworkBuffer != null)
                {
                    progressNetworkBuffer.Width = grpNetworkBuffer.Width - 20;
                }
                if (progressWasapiBuffer != null)
                {
                    progressWasapiBuffer.Width = grpWasapiBuffer.Width - 20;
                }
            }
        }
    }
}
