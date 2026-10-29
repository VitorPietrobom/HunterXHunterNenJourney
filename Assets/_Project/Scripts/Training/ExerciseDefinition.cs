using System;
using System.Collections.Generic;
using UnityEngine;

namespace HunterAwakening
{
    [Serializable]
    public struct AttributeTarget
    {
        public AttributeId attribute;
        [Range(0f, 1f)] public float specificity;
    }

    /// <summary>Static, data-driven exercise configuration (GDD §5.3). Never stores player progress.</summary>
    [CreateAssetMenu(menuName = "Hunter Awakening/Exercise Definition", fileName = "Exercise")]
    public class ExerciseDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public ExerciseCategory category;
        [TextArea] public string description;

        [Header("Targets")]
        public List<AttributeTarget> targets = new List<AttributeTarget>();
        public bool hasMastery;
        public TechniqueId masteryTechnique;

        [Header("Cost & validation")]
        [Min(0f)] public float fatigueCost = 20f;
        [Range(0f, 1f)] public float minCompletionForAdaptation = 0.3f;
        [Min(1)] public int maxIntensityLevel = 5;
        public bool requiresAura;
        [Tooltip("Body area this exercise loads: overload strains it, and heavy strain blocks it until rested.")]
        public BodyArea stressedArea;

        [Header("Feedback")]
        [TextArea] public string qualityHint;
        [TextArea] public string lowIntensityHint;
        public string metricLabel;
        public bool lowerMetricIsBetter;
    }
}
