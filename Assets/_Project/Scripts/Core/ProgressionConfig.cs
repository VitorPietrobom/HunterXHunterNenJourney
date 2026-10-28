using System;
using System.Collections.Generic;
using UnityEngine;

namespace HunterAwakening
{
    [Serializable]
    public struct AttributeCurve
    {
        public AttributeId attribute;
        [Tooltip("A0: ordinary-person reference value.")] public double baseline;
        [Tooltip("k: attribute-specific learning rate.")] public double learningRate;
        [Tooltip("C: progression scale.")] public double scale;
        [Tooltip("p: curve exponent.")] public double exponent;
    }

    /// <summary>Static tuning data for the progression service. Never holds live player state.</summary>
    [CreateAssetMenu(menuName = "Hunter Awakening/Progression Config", fileName = "ProgressionConfig")]
    public class ProgressionConfig : ScriptableObject
    {
        public List<AttributeCurve> curves = new List<AttributeCurve>();

        [Header("Technique mastery")]
        public float masteryRate = 4f;
        public float masteryExponent = 1.5f;
        [Tooltip("Mastery multiplier for a whole completed drill session vs. a single combat action.")]
        public float drillMasteryMultiplier = 2.5f;

        [Header("Fatigue")]
        public float readinessGamma = 1.5f;
        [Tooltip("Share of each fatigue load that also becomes accumulated fatigue.")]
        public float accumulatedFraction = 0.3f;
        public float passiveRecoveryPerSecond = 0.6f;
        public float restImmediatePerSecond = 18f;
        public float restAccumulatedPerSecond = 2.5f;

        [Header("Exploit guards")]
        [Tooltip("Sessions below this quality count as sloppy; repeated sloppy sessions give diminishing returns.")]
        public float lowQualityThreshold = 0.4f;

        public AttributeCurve GetCurve(AttributeId id)
        {
            foreach (var c in curves)
                if (c.attribute == id) return c;
            return DefaultCurve(id);
        }

        public static AttributeCurve DefaultCurve(AttributeId id) => new AttributeCurve
        {
            attribute = id,
            baseline = 10,
            learningRate = AttributeInfo.IsAura(id) ? 1.6 : 2.0,
            scale = 100,
            exponent = 2
        };

        public void PopulateDefaults()
        {
            curves.Clear();
            foreach (AttributeId id in Enum.GetValues(typeof(AttributeId)))
                curves.Add(DefaultCurve(id));
        }

        void Reset() => PopulateDefaults();

        public static ProgressionConfig CreateRuntimeDefault()
        {
            var config = CreateInstance<ProgressionConfig>();
            config.PopulateDefaults();
            return config;
        }
    }
}
