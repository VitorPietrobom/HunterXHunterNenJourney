using System;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Pure progression formulas (GDD §6, §7.3, §9.4). Stateless so they can be unit tested.
    /// </summary>
    public static class ProgressionMath
    {
        public const double MaxIntensity = 1.5;
        public const float MinEfficiency = 0.25f;
        public const float MasteryAdvancedFloor = 0.02f;

        /// <summary>S = D_norm × I × Q × R × F, every factor clamped to its valid range.</summary>
        public static double EffectiveStimulus(double durationNorm, double intensity, double quality, double readiness, double specificity)
        {
            return Clamp(durationNorm, 0, 1) * Clamp(intensity, 0, MaxIntensity) * Clamp(quality, 0, 1)
                 * Clamp(readiness, 0, 1) * Clamp(specificity, 0, 1);
        }

        /// <summary>1 / (1 + (max(0, A - A0) / C)^p). Always in (0, 1], never zero, so there is no hard cap.</summary>
        public static double ResponseMultiplier(double attribute, double baseline, double scale, double exponent)
        {
            if (!(scale > 0)) scale = 1;
            if (!(exponent > 0)) exponent = 1;
            double over = Math.Max(0, attribute - baseline) / scale;
            return 1.0 / (1.0 + Math.Pow(over, exponent));
        }

        /// <summary>deltaA = k × S / (1 + (max(0, A - A0) / C)^p)</summary>
        public static double AttributeGain(double attribute, double stimulus, double learningRate, double baseline, double scale, double exponent)
        {
            if (!(stimulus > 0) || !(learningRate > 0)) return 0;
            return learningRate * stimulus * ResponseMultiplier(attribute, baseline, scale, exponent);
        }

        /// <summary>
        /// deltaM = km × Q × (1 - M/100)^q, with a small floor so mastery keeps creeping past 100 (advanced tiers).
        /// </summary>
        public static float MasteryGain(float mastery, float quality, float rate, float exponent)
        {
            quality = Clean01(quality);
            if (quality <= 0f || !(rate > 0f)) return 0f;
            mastery = Mathf.Max(0f, mastery);
            float classic = Mathf.Pow(Mathf.Max(0f, 1f - mastery / 100f), Mathf.Max(0.01f, exponent));
            float advanced = MasteryAdvancedFloor / (1f + mastery / 100f);
            return rate * quality * Mathf.Max(classic, advanced);
        }

        /// <summary>readiness = max(0, 1 - F/100)^gamma</summary>
        public static float Readiness(float fatigue, float gamma)
        {
            if (float.IsNaN(fatigue)) fatigue = 0f;
            return Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Clamp(fatigue, 0f, 100f) / 100f), Mathf.Max(0.01f, gamma));
        }

        /// <summary>F_next = clamp(F + load - recovery, 0, 100)</summary>
        public static float NextFatigue(float current, float load, float recovery)
        {
            float next = current + load - recovery;
            return float.IsNaN(next) ? Mathf.Clamp(current, 0f, 100f) : Mathf.Clamp(next, 0f, 100f);
        }

        /// <summary>auraCost = power × duration / efficiency, with a safe efficiency floor and no negative costs.</summary>
        public static float AuraCost(float powerPerSecond, float duration, float efficiency)
        {
            if (float.IsNaN(efficiency) || efficiency < MinEfficiency) efficiency = MinEfficiency;
            float power = float.IsNaN(powerPerSecond) ? 0f : Mathf.Max(0f, powerPerSecond);
            float time = float.IsNaN(duration) ? 0f : Mathf.Max(0f, duration);
            return power * time / efficiency;
        }

        /// <summary>
        /// Logarithmic response that turns an uncapped stored attribute into a numerically stable gameplay effect (GDD §11).
        /// 0 at baseline, ~0.69 at 20, ~2.3 at 100, ~3.7 at 400.
        /// </summary>
        public static float Gain(double attribute, double baseline = 10, double scale = 10)
        {
            if (double.IsNaN(attribute)) return 0f;
            return (float)Math.Log(1 + Math.Max(0, attribute - baseline) / scale);
        }

        public static float Clean01(float v) => float.IsNaN(v) ? 0f : Mathf.Clamp01(v);

        static double Clamp(double v, double min, double max) => double.IsNaN(v) ? min : Math.Min(max, Math.Max(min, v));
    }
}
