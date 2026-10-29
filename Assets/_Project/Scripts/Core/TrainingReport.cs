using System.Collections.Generic;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>Scored result of one drill session, produced by a TrainingDrill and consumed by the ProgressionService.</summary>
    public class ExerciseOutcome
    {
        /// <summary>D_norm: share of the target duration/reps completed, 0..1.</summary>
        public float completion;
        /// <summary>Q: execution quality, 0..1.</summary>
        public float quality;
        /// <summary>I: challenge relative to current capability, 0..1.5. ~1 is ideal.</summary>
        public float intensity;
        /// <summary>R: average readiness during the session, 0..1.</summary>
        public float readiness = 1f;
        /// <summary>Drill-specific record metric (0 = none).</summary>
        public float primaryMetric;
        public string endReason;
        /// <summary>Strain added to the exercise's stressed body area (e.g. a failed rep under an overloaded bar).</summary>
        public float strain;
        public readonly List<string> metrics = new List<string>();

        public void Sanitize()
        {
            completion = ProgressionMath.Clean01(completion);
            quality = ProgressionMath.Clean01(quality);
            readiness = ProgressionMath.Clean01(readiness);
            intensity = float.IsNaN(intensity) ? 0f : Mathf.Clamp(intensity, 0f, (float)ProgressionMath.MaxIntensity);
            if (float.IsNaN(primaryMetric) || float.IsInfinity(primaryMetric) || primaryMetric < 0f) primaryMetric = 0f;
            strain = float.IsNaN(strain) ? 0f : Mathf.Clamp(strain, 0f, 100f);
        }
    }

    public class AttributeChange
    {
        public AttributeId attribute;
        public double before;
        public double after;
        /// <summary>Diminishing-return multiplier that applied (1 = beginner gains).</summary>
        public double response;
        public double Delta => after - before;
    }

    public class TrainingReport
    {
        public string exerciseId;
        public string exerciseName;
        public ExerciseOutcome outcome;
        public bool adapted;
        public float accumulatedFactor = 1f;
        public readonly List<AttributeChange> changes = new List<AttributeChange>();
        public bool hasMastery;
        public TechniqueId masteryTechnique;
        public float masteryBefore;
        public float masteryAfter;
        public float fatigueAdded;
        public float strainAdded;
        public bool newRecord;
        public readonly List<string> notes = new List<string>();

        public string Headline
        {
            get
            {
                if (!adapted) return $"{exerciseName}: no adaptation — see dashboard (Tab).";
                var parts = new List<string>();
                foreach (var c in changes)
                    if (c.Delta >= 0.005) parts.Add($"{AttributeInfo.Label(c.attribute)} +{c.Delta:0.00}");
                return $"{exerciseName} complete: {(parts.Count > 0 ? string.Join(", ", parts) : "minimal gains")}";
            }
        }
    }
}
