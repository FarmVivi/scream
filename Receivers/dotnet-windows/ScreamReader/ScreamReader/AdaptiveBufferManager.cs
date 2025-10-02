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
        private int currentWasapiLatencyMs;
        private int consecutiveLowBufferWarnings;
        private int consecutiveStablePackets;
        private DateTime lastAdjustmentTime;
        private readonly bool isUserSpecified;          // L'utilisateur a-t-il spécifié des valeurs manuelles ?
        private readonly Queue<BufferMeasurement> recentMeasurements;
        private const int MEASUREMENT_HISTORY_SIZE = 20;
        #endregion

        #region Properties
        public int CurrentBufferDurationMs => currentBufferDurationMs;
        public int CurrentWasapiLatencyMs => currentWasapiLatencyMs;
        public double TotalLatencyMs => currentBufferDurationMs + currentWasapiLatencyMs;
        public bool IsAdaptive => !isUserSpecified;
        #endregion

        public AdaptiveBufferManager(int userBufferDuration, int userWasapiLatency, bool useExclusiveMode, int bitWidth, int sampleRate)
        {
            this.recentMeasurements = new Queue<BufferMeasurement>(MEASUREMENT_HISTORY_SIZE);
            this.lastAdjustmentTime = DateTime.Now;
            this.isUserSpecified = (userBufferDuration > 0 || userWasapiLatency > 0);

            if (isUserSpecified)
            {
                // L'utilisateur a spécifié des valeurs, les utiliser directement
                this.currentBufferDurationMs = userBufferDuration > 0 ? userBufferDuration : TARGET_BUFFER_MS;
                this.currentWasapiLatencyMs = userWasapiLatency > 0 ? userWasapiLatency : TARGET_WASAPI_MS;
                LogManager.Log($"[AdaptiveBuffer] Mode manuel: Buffer={currentBufferDurationMs}ms, WASAPI={currentWasapiLatencyMs}ms");
            }
            else
            {
                // Mode adaptatif intelligent basé sur les caractéristiques audio
                InitializeAdaptiveBuffers(useExclusiveMode, bitWidth, sampleRate);
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

            LogManager.Log($"[AdaptiveBuffer] Mode adaptatif initialisé: Buffer={currentBufferDurationMs}ms, WASAPI={currentWasapiLatencyMs}ms");
            LogManager.Log($"[AdaptiveBuffer] Latence totale estimée: {TotalLatencyMs}ms");
        }

        /// <summary>
        /// Enregistre une mesure de buffer et ajuste si nécessaire
        /// </summary>
        public void RecordMeasurement(double bufferedMs, int bufferCapacityMs, int packetCount)
        {
            var measurement = new BufferMeasurement
            {
                Timestamp = DateTime.Now,
                BufferedMs = bufferedMs,
                BufferCapacityMs = bufferCapacityMs,
                FillPercentage = bufferedMs / bufferCapacityMs
            };

            recentMeasurements.Enqueue(measurement);
            if (recentMeasurements.Count > MEASUREMENT_HISTORY_SIZE)
                recentMeasurements.Dequeue();

            // Ne pas ajuster si l'utilisateur a spécifié des valeurs
            if (!IsAdaptive)
                return;

            // Analyser et ajuster
            AnalyzeAndAdjust(measurement, packetCount);
        }

        /// <summary>
        /// Analyse les mesures récentes et ajuste les buffers si nécessaire
        /// </summary>
        private void AnalyzeAndAdjust(BufferMeasurement current, int packetCount)
        {
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

                // Log périodique du bon fonctionnement
                if (packetCount % 500 == 0)
                {
                    LogManager.Log($"[AdaptiveBuffer] ✓ Stable: {bufferedMs:F1}ms ({fillPercentage:P0}), Latence totale: {TotalLatencyMs}ms");
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

            // Augmenter légèrement WASAPI aussi
            if (currentWasapiLatencyMs < MAX_WASAPI_MS)
            {
                currentWasapiLatencyMs = Math.Min(MAX_WASAPI_MS, currentWasapiLatencyMs + ADJUSTMENT_STEP_MS);
            }

            lastAdjustmentTime = DateTime.Now;
            
            LogManager.Log($"[AdaptiveBuffer] 📈 Augmentation des buffers: {reason}");
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

            // Réduire WASAPI aussi mais plus doucement
            if (currentWasapiLatencyMs > MIN_WASAPI_MS)
            {
                currentWasapiLatencyMs = Math.Max(MIN_WASAPI_MS, currentWasapiLatencyMs - ADJUSTMENT_STEP_MS / 2);
            }

            lastAdjustmentTime = DateTime.Now;

            LogManager.Log($"[AdaptiveBuffer] 📉 Réduction de la latence: {reason}");
            LogManager.Log($"[AdaptiveBuffer]    Buffer: {oldBufferMs}ms → {currentBufferDurationMs}ms ({currentBufferDurationMs - oldBufferMs}ms)");
            LogManager.Log($"[AdaptiveBuffer]    WASAPI: {oldWasapiMs}ms → {currentWasapiLatencyMs}ms ({currentWasapiLatencyMs - oldWasapiMs}ms)");
            LogManager.Log($"[AdaptiveBuffer]    Latence totale: {TotalLatencyMs}ms");
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
