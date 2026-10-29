using System;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// The only system allowed to change attributes, mastery, fatigue and knowledge (GDD §6.5, §12.1).
    /// Gameplay and UI read state through this service; drills and combat submit validated events to it.
    /// </summary>
    public class ProgressionService
    {
        readonly ProgressionConfig config;
        readonly PlayerState state;

        TechniqueId lastTechnique;
        float lastTechniqueTime = -100f;
        int techniqueStreak;

        public event Action<TrainingReport> ExerciseApplied;
        public event Action<TechniqueId, float, float> MasteryChanged;
        public event Action StateChanged;
        public event Action<string> Notice;

        public ProgressionService(ProgressionConfig config, PlayerState state)
        {
            this.config = config;
            this.state = state ?? PlayerState.CreateNew();
            this.state.Sanitize();
            Fatigue = new FatigueService(config, this.state.fatigue);
        }

        public PlayerState State => state;
        public FatigueService Fatigue { get; }
        public bool IsAuraAwakened => state.aura.awakened;
        public float AuraCurrent => state.aura.current;
        public int TrainingSessions => state.trainingSessions;

        public double GetAttribute(AttributeId id) => state.Attribute(id).value;
        public float GetMastery(TechniqueId id) => state.Mastery(id).value;
        public bool HasMastery(TechniqueId id, float threshold) => GetMastery(id) >= threshold;
        public bool KnowsInsight(string id) => state.knowledge.Contains(id);
        public bool HasBreakthrough(string id) => state.knowledge.Contains("breakthrough:" + id);

        /// <summary>Records a proven breakthrough (stored with the player's knowledge). Returns false if already known.</summary>
        public bool UnlockBreakthrough(string id)
        {
            var def = Breakthroughs.Find(id);
            if (def == null || !UnlockInsight("breakthrough:" + id, $"Breakthrough — {def.Value.title}", def.Value.reward, announce: false)) return false;
            StateChanged?.Invoke(); // derived stats (e.g. max health) may change
            return true;
        }

        // ------------------------------------------------------------------ Training

        public TrainingReport ApplyExercise(ExerciseDefinition def, ExerciseOutcome o)
        {
            if (def == null || o == null) throw new ArgumentNullException(def == null ? nameof(def) : nameof(o));
            o.Sanitize();

            var report = new TrainingReport
            {
                exerciseId = def.id,
                exerciseName = def.displayName,
                outcome = o,
                accumulatedFactor = Fatigue.AdaptationEfficiency
            };
            var record = state.Record(def.id);

            bool completedEnough = o.completion >= def.minCompletionForAdaptation;
            bool auraOk = !def.requiresAura || state.aura.awakened;
            bool valid = completedEnough && auraOk && o.quality > 0f;

            float sloppyFactor = 1f;
            if (o.quality < config.lowQualityThreshold)
            {
                record.lowQualityStreak++;
                sloppyFactor = 1f / (1f + 0.5f * record.lowQualityStreak);
            }
            else record.lowQualityStreak = 0;

            if (valid)
            {
                foreach (var target in def.targets)
                {
                    var curve = config.GetCurve(target.attribute);
                    double before = GetAttribute(target.attribute);
                    double stimulus = ProgressionMath.EffectiveStimulus(o.completion, o.intensity, o.quality, o.readiness, target.specificity)
                                      * report.accumulatedFactor * sloppyFactor;
                    double delta = ProgressionMath.AttributeGain(before, stimulus, curve.learningRate, curve.baseline, curve.scale, curve.exponent);
                    state.Attribute(target.attribute).value = before + delta;
                    report.changes.Add(new AttributeChange
                    {
                        attribute = target.attribute,
                        before = before,
                        after = before + delta,
                        response = ProgressionMath.ResponseMultiplier(before, curve.baseline, curve.scale, curve.exponent)
                    });
                }

                if (def.hasMastery)
                {
                    report.hasMastery = true;
                    report.masteryTechnique = def.masteryTechnique;
                    report.masteryBefore = GetMastery(def.masteryTechnique);
                    ApplyMasteryInternal(def.masteryTechnique, o.quality * o.completion * sloppyFactor, config.drillMasteryMultiplier);
                    report.masteryAfter = GetMastery(def.masteryTechnique);
                }
                report.adapted = true;
                state.trainingSessions++;
            }

            // Fatigue is paid even for a bad or aborted session: training too hard has a visible cost.
            float load = def.fatigueCost * Mathf.Lerp(0.5f, 1.2f, Mathf.InverseLerp(0f, 1.4f, o.intensity)) * Mathf.Max(0.2f, o.completion);
            Fatigue.AddLoad(load);
            report.fatigueAdded = load;

            record.sessions++;
            if (valid)
            {
                record.bestQuality = Mathf.Max(record.bestQuality, o.quality);
                if (o.primaryMetric > 0f && o.completion >= 0.999f)
                {
                    bool better = record.bestMetric <= 0f ||
                                  (def.lowerMetricIsBetter ? o.primaryMetric < record.bestMetric : o.primaryMetric > record.bestMetric);
                    if (better)
                    {
                        report.newRecord = record.bestMetric > 0f;
                        record.bestMetric = o.primaryMetric;
                    }
                }
            }

            BuildNotes(def, report, completedEnough, auraOk, sloppyFactor);
            AddHistory(def.displayName, report.Headline);
            ExerciseApplied?.Invoke(report);
            StateChanged?.Invoke();
            return report;
        }

        void BuildNotes(ExerciseDefinition def, TrainingReport r, bool completedEnough, bool auraOk, float sloppyFactor)
        {
            var o = r.outcome;
            if (!string.IsNullOrEmpty(o.endReason)) r.notes.Add(o.endReason);
            if (!auraOk) r.notes.Add("This exercise requires an awakened aura.");
            if (!completedEnough)
                r.notes.Add($"Session too short ({o.completion:P0} complete, {def.minCompletionForAdaptation:P0} needed) — no adaptation, but fatigue still accrued.");
            if (completedEnough && o.quality <= 0f)
                r.notes.Add("No correctly executed repetitions — no adaptation.");

            if (o.intensity < 0.65f)
                r.notes.Add($"Intensity {o.intensity:P0}: {(string.IsNullOrEmpty(def.lowIntensityHint) ? "this setting is too easy for you now." : def.lowIntensityHint)}");
            else if (o.intensity > 1.25f)
                r.notes.Add($"Intensity {o.intensity:P0}: a strong stimulus, but harder to complete with good form.");

            if (o.quality < 0.6f && o.quality > 0f)
                r.notes.Add($"Execution quality {o.quality:P0}: {def.qualityHint}");
            if (o.readiness < 0.75f)
                r.notes.Add($"Fatigue reduced effectiveness (readiness {o.readiness:P0}). Rest at the bench before the next hard set.");
            if (r.accumulatedFactor < 0.85f)
                r.notes.Add($"Accumulated fatigue limited adaptation to {r.accumulatedFactor:P0}. A longer rest at the bench clears it.");
            if (sloppyFactor < 1f)
                r.notes.Add("Repeated low-quality sessions give diminishing returns — focus on form over volume.");

            foreach (var c in r.changes)
                if (c.response < 0.75)
                    r.notes.Add($"{AttributeInfo.Label(c.attribute)} is well above baseline: equal training now yields {c.response:P0} of a beginner's gain. Raise intensity or unlock harder methods.");

            if (r.newRecord) r.notes.Add($"New personal record ({def.metricLabel}).");
        }

        // ------------------------------------------------------------------ Mastery

        /// <summary>
        /// Combat-action mastery. Rapidly repeating the same technique yields diminishing returns (anti-spam, GDD §5.2).
        /// </summary>
        public float ApplyMastery(TechniqueId id, float quality)
        {
            float now = Time.time;
            if (id == lastTechnique && now - lastTechniqueTime < 4f) techniqueStreak++;
            else techniqueStreak = 0;
            lastTechnique = id;
            lastTechniqueTime = now;

            float repetition = 1f / (1f + 0.4f * Mathf.Max(0, techniqueStreak - 2));
            return ApplyMasteryInternal(id, quality * repetition, 1f);
        }

        float ApplyMasteryInternal(TechniqueId id, float quality, float multiplier)
        {
            var entry = state.Mastery(id);
            float before = entry.value;
            float delta = ProgressionMath.MasteryGain(before, quality, config.masteryRate * multiplier, config.masteryExponent);
            if (delta <= 0f) return 0f;
            entry.value = before + delta;

            string crossed = MasteryMilestones.Crossed(id, before, entry.value);
            if (crossed != null)
            {
                Notice?.Invoke($"{AttributeInfo.Label(id)} milestone: {crossed}!");
                AddHistory($"{AttributeInfo.Label(id)} breakthrough", crossed);
            }
            MasteryChanged?.Invoke(id, before, entry.value);
            return delta;
        }

        // ------------------------------------------------------------------ Fatigue / resources / knowledge

        public void AddExertion(float amount) => Fatigue.AddExertion(amount);

        public void TickActiveTime(float dt, bool resting)
        {
            if (!(dt > 0f)) return;
            state.activePlaySeconds += dt;
            Fatigue.Recover(dt, resting);
        }

        public void SetAuraCurrent(float value, float capacity)
        {
            if (!state.aura.awakened) { state.aura.current = 0f; return; }
            state.aura.current = float.IsNaN(value) ? 0f : Mathf.Clamp(value, 0f, Mathf.Max(0f, capacity));
        }

        public void AwakenAura(float startingReserve)
        {
            if (state.aura.awakened) return;
            state.aura.awakened = true;
            state.aura.current = startingReserve;
            UnlockInsight("aura_awakening", "Aura Awakening", "You can now sense and hold your aura. Ten (T) and Ren (hold R) are available; train control at the Aura Mat.");
            StateChanged?.Invoke();
        }

        public bool UnlockInsight(string id, string title, string description, bool announce = true)
        {
            if (state.knowledge.Contains(id)) return false;
            state.knowledge.Add(id);
            AddHistory($"Insight: {title}", description);
            if (announce) Notice?.Invoke($"Insight gained: {title}");
            return true;
        }

        public void RecordCombat(string tierId, bool won, float duration, string summary)
        {
            var record = state.Record(SparringRecordId(tierId));
            record.sessions++;
            if (won && (record.bestMetric <= 0f || duration < record.bestMetric)) record.bestMetric = duration;
            AddHistory(won ? "Sparring: victory" : "Sparring: defeat", summary);
            StateChanged?.Invoke();
        }

        /// <summary>Fastest win against an opponent tier, or 0 if never beaten.</summary>
        public float BestSparringTime(string tierId) => state.Record(SparringRecordId(tierId)).bestMetric;

        // The default tier keeps the pre-tier record id so existing saves keep their best time.
        static string SparringRecordId(string tierId) => "sparring_" + (string.IsNullOrEmpty(tierId) ? "rival" : tierId);

        public void CapturePosition(Vector3 position, float yaw)
        {
            state.hasPosition = true;
            state.position = position;
            state.yaw = yaw;
        }

        void AddHistory(string title, string summary)
        {
            state.history.Add(new HistoryEntry { title = title, summary = summary, activeTime = state.activePlaySeconds });
            while (state.history.Count > PlayerState.MaxHistory) state.history.RemoveAt(0);
        }
    }
}
