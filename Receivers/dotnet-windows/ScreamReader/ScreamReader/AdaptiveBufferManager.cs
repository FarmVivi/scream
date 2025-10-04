using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ScreamReader
{
    /// <summary>
    /// Gère dynamiquement les tailles de buffer pour optimiser la latence tout en préservant la qualité audio.
    /// Utilise des heuristiques adaptatives pour détecter et corriger les craquellements.
    /// </summary>
    internal class AdaptiveBufferManager
    {
        #region Configuration Constants
        // Limites absolues de sécurité
        private const int MIN_BUFFER_MS = 20;           // Minimum absolu
        private const int MAX_BUFFER_MS = 300;          // Maximum absolu
        private const int MIN_WASAPI_MS = 10;           // Minimum absolu WASAPI
        private const int MAX_WASAPI_MS = 500;          // Maximum absolu WASAPI

        // Cibles optimales par défaut (mode équilibré)
        private const int TARGET_BUFFER_MS = 30;        // Cible pour le buffer réseau (réduit de 150ms)
        private const int TARGET_WASAPI_MS = 20;        // Cible pour WASAPI (réduit de 200ms)

        // Seuils de détection
        private const double CRITICAL_BUFFER_THRESHOLD = 0.15;  // 15% du buffer = critique
        private const double LOW_BUFFER_THRESHOLD = 0.25;       // 25% du buffer = bas
        private const double HIGH_BUFFER_THRESHOLD = 0.80;      // 80% du buffer = trop haut
        
        // Ajustements adaptatifs
        private const int ADJUSTMENT_STEP_MS = 5;       // Pas d'ajustement en millisecondes
        private const int PACKETS_BEFORE_DECREASE = 100;// Attendre N paquets stables avant de réduire
        private const int MAX_LOW_BUFFER_WARNINGS = 3;  // Nombre d'alertes avant augmentation
        #endregion

        #region State Variables
        private int currentBufferDurationMs;
        private int currentWasapiLatencyMs;             // Valeur cible (utilisée lors des recreations)
        private int actualWasapiLatencyMs;              // Valeur réelle actuellement utilisée (fixe)
        private int consecutiveLowBufferWarnings;
        private int consecutiveStablePackets;
        private DateTime lastAdjustmentTime;
        private readonly bool isUserSpecified;          // L'utilisateur a-t-il spécifié des valeurs manuelles ?
        private readonly Queue<BufferMeasurement> recentMeasurements;
        private const int MEASUREMENT_HISTORY_SIZE = 20;
        
        // Statistiques long terme pour analyse intelligente
        private readonly List<double> longTermBufferFills;         // Historique des % de remplissage
        private readonly List<double> longTermBufferedMs;          // Historique des ms bufferisées
        private int recommendedWasapiLatency;                      // Latence WASAPI recommandée après analyse
        private const int LONG_TERM_HISTORY_SIZE = 1000;           // ~100s à 10Hz
        private DateTime sessionStartTime;
        private bool hasLongTermAnalysis;
        #endregion

        #region Properties
        public int CurrentBufferDurationMs => currentBufferDurationMs;
        public int CurrentWasapiLatencyMs => currentWasapiLatencyMs;     // Valeur cible
        public int ActualWasapiLatencyMs => actualWasapiLatencyMs;       // Valeur réelle
        public double TotalLatencyMs => currentBufferDurationMs + actualWasapiLatencyMs;
        public bool IsAdaptive => !isUserSpecified;
        public int RecommendedWasapiLatency => recommendedWasapiLatency; // Suggestion après analyse
        public bool HasLongTermAnalysis => hasLongTermAnalysis;
        
        // Stats actuelles pour l'UI
        private double currentNetworkBufferedMs;
        private double currentNetworkBufferCapacityMs;
        public double NetworkBufferedMs => currentNetworkBufferedMs;
        public double NetworkBufferCapacityMs => currentNetworkBufferCapacityMs;
        public double NetworkBufferFillPercentage => currentNetworkBufferCapacityMs > 0 ? (currentNetworkBufferedMs / currentNetworkBufferCapacityMs) * 100.0 : 0;
        
        public double WasapiBufferedMs => actualWasapiLatencyMs; // WASAPI est toujours "plein"
        public double WasapiBufferCapacityMs => actualWasapiLatencyMs;
        public double WasapiBufferFillPercentage => 100.0; // WASAPI est toujours considéré plein
        #endregion

        public AdaptiveBufferManager(int userBufferDuration, int userWasapiLatency, bool useExclusiveMode, int bitWidth, int sampleRate)
        {
            this.recentMeasurements = new Queue<BufferMeasurement>(MEASUREMENT_HISTORY_SIZE);
            this.lastAdjustmentTime = DateTime.Now;
            this.isUserSpecified = (userBufferDuration > 0 || userWasapiLatency > 0);
            
            // Initialiser les statistiques long terme
            this.longTermBufferFills = new List<double>(LONG_TERM_HISTORY_SIZE);
            this.longTermBufferedMs = new List<double>(LONG_TERM_HISTORY_SIZE);
            this.sessionStartTime = DateTime.Now;
            this.hasLongTermAnalysis = false;

            if (isUserSpecified)
            {
                // L'utilisateur a spécifié des valeurs, les utiliser directement
                this.currentBufferDurationMs = userBufferDuration > 0 ? userBufferDuration : TARGET_BUFFER_MS;
                this.currentWasapiLatencyMs = userWasapiLatency > 0 ? userWasapiLatency : TARGET_WASAPI_MS;
                this.actualWasapiLatencyMs = this.currentWasapiLatencyMs;
                this.recommendedWasapiLatency = this.currentWasapiLatencyMs;
                LogManager.Log($"[AdaptiveBuffer] Mode manuel: Buffer={currentBufferDurationMs}ms, WASAPI={currentWasapiLatencyMs}ms");
            }
            else
            {
                // Mode adaptatif intelligent basé sur les caractéristiques audio
                InitializeAdaptiveBuffers(useExclusiveMode, bitWidth, sampleRate);
                this.recommendedWasapiLatency = this.currentWasapiLatencyMs;
            }
        }

        /// <summary>
        /// Initialise les buffers de manière intelligente basée sur les caractéristiques système et audio
        /// </summary>
        private void InitializeAdaptiveBuffers(bool useExclusiveMode, int bitWidth, int sampleRate)
        {
            // Commencer avec des valeurs optimistes pour une faible latence
            currentBufferDurationMs = TARGET_BUFFER_MS;
            currentWasapiLatencyMs = TARGET_WASAPI_MS;

            // Ajustements basés sur le mode exclusif/partagé
            if (useExclusiveMode)
            {
                // Mode exclusif : peut aller encore plus bas
                currentBufferDurationMs = Math.Max(MIN_BUFFER_MS, TARGET_BUFFER_MS - 10);
                currentWasapiLatencyMs = Math.Max(MIN_WASAPI_MS, TARGET_WASAPI_MS - 10);
                LogManager.Log($"[AdaptiveBuffer] Mode Exclusif détecté: démarrage avec latence ultra-basse");
            }
            else
            {
                // Mode partagé : un peu plus conservateur mais toujours optimiste
                currentBufferDurationMs = TARGET_BUFFER_MS;
                currentWasapiLatencyMs = TARGET_WASAPI_MS;
                LogManager.Log($"[AdaptiveBuffer] Mode Partagé détecté: démarrage avec latence basse");
            }

            // Ajustements basés sur la qualité audio
            if (bitWidth >= 24 || sampleRate >= 96000)
            {
                // Audio haute résolution : besoin d'un peu plus de buffer
                currentBufferDurationMs += 10;
                currentWasapiLatencyMs += 5;
                LogManager.Log($"[AdaptiveBuffer] Audio haute résolution détecté ({bitWidth}bit/{sampleRate}Hz): ajustement +10ms");
            }

            // Initialiser la latence WASAPI réelle
            this.actualWasapiLatencyMs = this.currentWasapiLatencyMs;
            
            LogManager.Log($"[AdaptiveBuffer] Mode adaptatif initialisé: Buffer={currentBufferDurationMs}ms, WASAPI={currentWasapiLatencyMs}ms");
            LogManager.Log($"[AdaptiveBuffer] Latence totale estimée: {TotalLatencyMs}ms");
        }

        /// <summary>
        /// Enregistre une mesure de buffer et ajuste si nécessaire
        /// </summary>
        public void RecordMeasurement(double bufferedMs, int bufferCapacityMs, int packetCount)
        {
            // Stocker les valeurs actuelles pour l'UI
            currentNetworkBufferedMs = bufferedMs;
            currentNetworkBufferCapacityMs = Math.Max(bufferCapacityMs, 0);

            var measurement = CreateMeasurement(bufferedMs, bufferCapacityMs);

            StoreMeasurement(measurement);

            // Enregistrer dans l'historique long terme (même si capacité invalide, on garde la trace du niveau)
            RecordLongTermStats(measurement.BufferedMs, measurement.FillPercentage);

            // Ne pas ajuster si l'utilisateur a spécifié des valeurs
            if (!IsAdaptive)
                return;

            // Analyser et ajuster
            if (measurement.BufferCapacityMs > 0)
            {
                AnalyzeAndAdjust(measurement, packetCount);
            }
        }

        private BufferMeasurement CreateMeasurement(double bufferedMs, int bufferCapacityMs)
        {
            return new BufferMeasurement
            {
                Timestamp = DateTime.Now,
                BufferedMs = bufferedMs,
                BufferCapacityMs = bufferCapacityMs,
                FillPercentage = bufferCapacityMs > 0 ? bufferedMs / bufferCapacityMs : 0d
            };
        }

        private void StoreMeasurement(BufferMeasurement measurement)
        {
            recentMeasurements.Enqueue(measurement);
            if (recentMeasurements.Count > MEASUREMENT_HISTORY_SIZE)
            {
                recentMeasurements.Dequeue();
            }
        }
        
        /// <summary>
        /// Enregistre les statistiques dans l'historique long terme
        /// </summary>
        private void RecordLongTermStats(double bufferedMs, double fillPercentage)
        {
            longTermBufferedMs.Add(bufferedMs);
            longTermBufferFills.Add(fillPercentage);
            
            // Limiter la taille pour éviter la croissance infinie
            if (longTermBufferedMs.Count > LONG_TERM_HISTORY_SIZE)
            {
                longTermBufferedMs.RemoveAt(0);
                longTermBufferFills.RemoveAt(0);
            }
        }

        /// <summary>
        /// Analyse les mesures récentes et ajuste les buffers si nécessaire
        /// </summary>
        private void AnalyzeAndAdjust(BufferMeasurement current, int packetCount)
        {
            if (current.BufferCapacityMs <= 0)
            {
                return;
            }

            // Attendre au moins 2 secondes entre les ajustements pour laisser le système se stabiliser
            if ((DateTime.Now - lastAdjustmentTime).TotalSeconds < 2.0)
                return;

            var fillPercentage = current.FillPercentage;
            var bufferedMs = current.BufferedMs;

            // Situation CRITIQUE : buffer très bas (risque de craquellement)
            if (fillPercentage < CRITICAL_BUFFER_THRESHOLD)
            {
                consecutiveLowBufferWarnings++;
                consecutiveStablePackets = 0;

                LogManager.Log($"[AdaptiveBuffer] ⚠️ Buffer CRITIQUE: {bufferedMs:F1}ms ({fillPercentage:P0}) - Warning #{consecutiveLowBufferWarnings}");

                // Augmenter immédiatement si on a plusieurs alertes consécutives
                if (consecutiveLowBufferWarnings >= MAX_LOW_BUFFER_WARNINGS)
                {
                    IncreaseBuffers("Buffer critique détecté à plusieurs reprises");
                    consecutiveLowBufferWarnings = 0;
                }
            }
            // Situation BASSE : buffer bas mais pas critique
            else if (fillPercentage < LOW_BUFFER_THRESHOLD)
            {
                consecutiveLowBufferWarnings++;
                consecutiveStablePackets = 0;

                if (consecutiveLowBufferWarnings >= MAX_LOW_BUFFER_WARNINGS * 2)
                {
                    IncreaseBuffers("Buffer constamment bas");
                    consecutiveLowBufferWarnings = 0;
                }
            }
            // Buffer TROP HAUT : opportunité de réduire la latence
            else if (fillPercentage > HIGH_BUFFER_THRESHOLD)
            {
                consecutiveLowBufferWarnings = 0;
                consecutiveStablePackets++;

                // Si le buffer est constamment trop haut, on peut réduire pour gagner en latence
                if (consecutiveStablePackets >= PACKETS_BEFORE_DECREASE)
                {
                    DecreaseBuffers("Buffer constamment élevé, réduction de la latence possible");
                    consecutiveStablePackets = 0;
                }
            }
            // Buffer OPTIMAL : tout va bien
            else
            {
                consecutiveLowBufferWarnings = 0;
                consecutiveStablePackets++;

                // Stats déjà affichées dans l'UI, log debug uniquement
                if (packetCount % 500 == 0)
                {
                    LogManager.LogDebug($"[AdaptiveBuffer] ✓ Stable: {bufferedMs:F1}ms ({fillPercentage:P0}), Latence totale: {TotalLatencyMs}ms");
                }
            }
        }

        /// <summary>
        /// Augmente les buffers pour améliorer la stabilité
        /// </summary>
        private void IncreaseBuffers(string reason)
        {
            int oldBufferMs = currentBufferDurationMs;
            int oldWasapiMs = currentWasapiLatencyMs;

            // Augmenter le buffer réseau en priorité (plus efficace contre les interruptions réseau)
            if (currentBufferDurationMs < MAX_BUFFER_MS)
            {
                currentBufferDurationMs = Math.Min(MAX_BUFFER_MS, currentBufferDurationMs + ADJUSTMENT_STEP_MS * 2);
            }

            // Note: WASAPI ne peut pas être ajusté en temps réel (nécessite recréation WasapiOut)
            // La valeur currentWasapiLatencyMs sera utilisée lors du prochain changement de format

            lastAdjustmentTime = DateTime.Now;
            
            LogManager.Log($"[AdaptiveBuffer] 📈 Augmentation de la latence: {reason}");
            LogManager.Log($"[AdaptiveBuffer]    Buffer: {oldBufferMs}ms → {currentBufferDurationMs}ms (+{currentBufferDurationMs - oldBufferMs}ms)");
            LogManager.Log($"[AdaptiveBuffer]    WASAPI: {oldWasapiMs}ms → {currentWasapiLatencyMs}ms (+{currentWasapiLatencyMs - oldWasapiMs}ms)");
            LogManager.Log($"[AdaptiveBuffer]    Latence totale: {TotalLatencyMs}ms");
        }

        /// <summary>
        /// Réduit les buffers pour diminuer la latence
        /// </summary>
        private void DecreaseBuffers(string reason)
        {
            int oldBufferMs = currentBufferDurationMs;
            int oldWasapiMs = currentWasapiLatencyMs;

            // Réduire prudemment, le buffer réseau en priorité
            if (currentBufferDurationMs > MIN_BUFFER_MS)
            {
                currentBufferDurationMs = Math.Max(MIN_BUFFER_MS, currentBufferDurationMs - ADJUSTMENT_STEP_MS);
            }

            // Note: WASAPI ne peut pas être ajusté en temps réel (nécessite recréation WasapiOut)
            // La valeur currentWasapiLatencyMs sera utilisée lors du prochain changement de format

            lastAdjustmentTime = DateTime.Now;

            LogManager.Log($"[AdaptiveBuffer] 📉 Réduction de la latence: {reason}");
            LogManager.Log($"[AdaptiveBuffer]    Buffer: {oldBufferMs}ms → {currentBufferDurationMs}ms ({currentBufferDurationMs - oldBufferMs}ms)");
            LogManager.Log($"[AdaptiveBuffer]    WASAPI: {oldWasapiMs}ms → {currentWasapiLatencyMs}ms ({currentWasapiLatencyMs - oldWasapiMs}ms)");
            LogManager.Log($"[AdaptiveBuffer]    Latence totale: {TotalLatencyMs}ms");
        }

        /// <summary>
        /// Met à jour la latence WASAPI réelle après création/recréation de WasapiOut
        /// </summary>
        public void SetActualWasapiLatency(int actualLatencyMs)
        {
            this.actualWasapiLatencyMs = actualLatencyMs;
            LogManager.LogDebug($"[AdaptiveBuffer] WASAPI réel initialisé: {actualLatencyMs}ms");
        }

        /// <summary>
        /// Obtient les statistiques récentes pour le diagnostic
        /// </summary>
        public string GetStatistics()
        {
            if (recentMeasurements.Count == 0)
                return "Aucune mesure disponible";

            var measurements = recentMeasurements.ToList();
            var avgFill = measurements.Average(m => m.FillPercentage);
            var minFill = measurements.Min(m => m.FillPercentage);
            var maxFill = measurements.Max(m => m.FillPercentage);
            var avgBufferedMs = measurements.Average(m => m.BufferedMs);

            return $"Buffer moyen: {avgBufferedMs:F1}ms, Remplissage: {avgFill:P0} (min: {minFill:P0}, max: {maxFill:P0}), Latence totale: {TotalLatencyMs}ms";
        }
        
        /// <summary>
        /// Analyse les statistiques long terme et détermine une latence WASAPI optimale.
        /// Appelé lors du Stop() pour préparer les paramètres du prochain Start().
        /// </summary>
        public void AnalyzeLongTermPerformance()
        {
            // Nécessite au moins 30 secondes de données pour une analyse fiable
            var sessionDuration = (DateTime.Now - sessionStartTime).TotalSeconds;
            if (sessionDuration < 30 || longTermBufferFills.Count < 100)
            {
                LogManager.LogDebug($"[AdaptiveBuffer] Analyse long terme: données insuffisantes ({sessionDuration:F0}s, {longTermBufferFills.Count} mesures)");
                return;
            }
            
            // Calculer statistiques
            double avgFill = longTermBufferFills.Average();
            double stdDevFill = CalculateStandardDeviation(longTermBufferFills);
            double minFill = longTermBufferFills.Min();
            double maxFill = longTermBufferFills.Max();
            
            double avgBufferedMs = longTermBufferedMs.Average();
            double stdDevBufferedMs = CalculateStandardDeviation(longTermBufferedMs);
            
            // Compter les incidents critiques
            int criticalEvents = longTermBufferFills.Count(f => f < CRITICAL_BUFFER_THRESHOLD);
            int lowEvents = longTermBufferFills.Count(f => f < LOW_BUFFER_THRESHOLD);
            int highEvents = longTermBufferFills.Count(f => f > HIGH_BUFFER_THRESHOLD);
            
            double criticalRate = (double)criticalEvents / longTermBufferFills.Count;
            double lowRate = (double)lowEvents / longTermBufferFills.Count;
            double highRate = (double)highEvents / longTermBufferFills.Count;
            
            LogManager.Log($"[AdaptiveBuffer] 📊 Analyse long terme ({sessionDuration:F0}s, {longTermBufferFills.Count} mesures):");
            LogManager.Log($"[AdaptiveBuffer]    Remplissage moyen: {avgFill:P1} ± {stdDevFill:P1} (min: {minFill:P1}, max: {maxFill:P1})");
            LogManager.Log($"[AdaptiveBuffer]    Buffer moyen: {avgBufferedMs:F1}ms ± {stdDevBufferedMs:F1}ms");
            LogManager.Log($"[AdaptiveBuffer]    Incidents: {criticalRate:P1} critiques, {lowRate:P1} bas, {highRate:P1} élevés");
            LogManager.Log($"[AdaptiveBuffer]    WASAPI actuel: {actualWasapiLatencyMs}ms");
            
            // Décision d'ajustement WASAPI
            int suggestedWasapiMs = actualWasapiLatencyMs;
            string recommendation = "";
            
            // Cas 1: Trop d'incidents critiques ou bas (>5%) => augmenter WASAPI
            if (criticalRate > 0.05 || lowRate > 0.10)
            {
                int increaseAmount = 5;
                if (criticalRate > 0.10) increaseAmount = 10; // Beaucoup de critiques => +10ms
                
                suggestedWasapiMs = Math.Min(MAX_WASAPI_MS, actualWasapiLatencyMs + increaseAmount);
                recommendation = $"Augmentation recommandée: trop d'incidents ({criticalRate:P1} critiques, {lowRate:P1} bas)";
            }
            // Cas 2: Buffer constamment très haut (>70% du temps au-dessus de 80%) et stable => réduire WASAPI
            else if (highRate > 0.70 && stdDevFill < 0.15)
            {
                int decreaseAmount = 3; // Réduction conservatrice
                suggestedWasapiMs = Math.Max(MIN_WASAPI_MS, actualWasapiLatencyMs - decreaseAmount);
                recommendation = $"Réduction possible: buffer élevé et stable ({highRate:P1} au-dessus de {HIGH_BUFFER_THRESHOLD:P0})";
            }
            // Cas 3: Performance optimale => garder ou ajustement mineur
            else if (avgFill > 0.40 && avgFill < 0.70 && stdDevFill < 0.20)
            {
                // Parfait, possibilité de réduction très conservatrice si vraiment stable
                if (stdDevFill < 0.10 && minFill > 0.30)
                {
                    suggestedWasapiMs = Math.Max(MIN_WASAPI_MS, actualWasapiLatencyMs - 2);
                    recommendation = $"Performance excellente: réduction légère possible (écart-type: {stdDevFill:P1})";
                }
                else
                {
                    recommendation = "Performance optimale: aucun ajustement nécessaire";
                }
            }
            // Cas 4: Variabilité élevée => augmenter légèrement pour stabiliser
            else if (stdDevFill > 0.25)
            {
                suggestedWasapiMs = Math.Min(MAX_WASAPI_MS, actualWasapiLatencyMs + 3);
                recommendation = $"Stabilisation recommandée: variabilité élevée (écart-type: {stdDevFill:P1})";
            }
            else
            {
                recommendation = "Performance acceptable: aucun ajustement majeur nécessaire";
            }
            
            // Appliquer la recommandation
            recommendedWasapiLatency = suggestedWasapiMs;
            hasLongTermAnalysis = true;
            
            if (suggestedWasapiMs != actualWasapiLatencyMs)
            {
                LogManager.Log($"[AdaptiveBuffer] 💡 {recommendation}");
                LogManager.Log($"[AdaptiveBuffer] 🎯 Recommandation: WASAPI {actualWasapiLatencyMs}ms → {suggestedWasapiMs}ms");
                LogManager.Log($"[AdaptiveBuffer]    Cette latence sera appliquée au prochain démarrage");
            }
            else
            {
                LogManager.Log($"[AdaptiveBuffer] ✓ {recommendation}");
            }
        }
        
        /// <summary>
        /// Applique la latence WASAPI recommandée si une analyse a été effectuée.
        /// Appelé au Start() pour utiliser les paramètres optimisés.
        /// </summary>
        public void ApplyRecommendedWasapiLatency()
        {
            if (!hasLongTermAnalysis)
                return;
            
            if (recommendedWasapiLatency != currentWasapiLatencyMs)
            {
                int oldWasapi = currentWasapiLatencyMs;
                currentWasapiLatencyMs = recommendedWasapiLatency;
                
                LogManager.Log($"[AdaptiveBuffer] ✅ Application de la latence WASAPI recommandée: {oldWasapi}ms → {currentWasapiLatencyMs}ms");
            }
            
            // Réinitialiser pour la prochaine session
            hasLongTermAnalysis = false;
            longTermBufferFills.Clear();
            longTermBufferedMs.Clear();
            sessionStartTime = DateTime.Now;
        }
        
        /// <summary>
        /// Calcule l'écart-type d'une liste de valeurs
        /// </summary>
        private double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count < 2)
                return 0.0;
            
            double avg = values.Average();
            double sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumOfSquares / (values.Count - 1));
        }

        /// <summary>
        /// Structure pour stocker une mesure de buffer
        /// </summary>
        private struct BufferMeasurement
        {
            public DateTime Timestamp;
            public double BufferedMs;
            public int BufferCapacityMs;
            public double FillPercentage;
        }
    }
}
