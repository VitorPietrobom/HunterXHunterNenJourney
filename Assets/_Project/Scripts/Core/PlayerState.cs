using System;
using System.Collections.Generic;
using UnityEngine;

namespace HunterAwakening
{
    [Serializable] public class AttributeEntry { public AttributeId id; public double value; }
    [Serializable] public class MasteryEntry { public TechniqueId id; public float value; }
    [Serializable] public class FatigueState { public float immediate; public float accumulated; }
    [Serializable] public class AuraState { public bool awakened; public float current; }
    /// <summary>Strain per body area, 0..100 (GDD §9.3). Comes from overload, not chance.</summary>
    [Serializable] public class InjuryState { public float upper; public float lower; }

    [Serializable]
    public class ExerciseRecord
    {
        public string exerciseId;
        public int sessions;
        public float bestQuality;
        public float bestMetric;
        public int lowQualityStreak;
    }

    [Serializable]
    public class HistoryEntry
    {
        public string title;
        public string summary;
        public double activeTime;
    }

    /// <summary>
    /// Canonical, serializable player progress. Only <see cref="ProgressionService"/> should write to it.
    /// </summary>
    [Serializable]
    public class PlayerState
    {
        public const int CurrentSchemaVersion = 1;
        public const int MaxHistory = 40;
        public const double Baseline = 10;

        public int schemaVersion = CurrentSchemaVersion;
        public List<AttributeEntry> attributes = new List<AttributeEntry>();
        public List<MasteryEntry> mastery = new List<MasteryEntry>();
        public FatigueState fatigue = new FatigueState();
        public AuraState aura = new AuraState();
        public InjuryState injury = new InjuryState();
        public List<ExerciseRecord> records = new List<ExerciseRecord>();
        public List<HistoryEntry> history = new List<HistoryEntry>();
        public List<string> knowledge = new List<string>();
        /// <summary>Field encounters (ant squads, the King) beaten for good.</summary>
        public List<string> clearedEncounters = new List<string>();
        public int trainingSessions;
        public double activePlaySeconds;
        public bool hasPosition;
        public Vector3 position;
        public float yaw;
        public string lastSavedUtc;

        public static PlayerState CreateNew()
        {
            var s = new PlayerState();
            s.Sanitize();
            return s;
        }

        public AttributeEntry Attribute(AttributeId id)
        {
            foreach (var a in attributes)
                if (a.id == id) return a;
            var entry = new AttributeEntry { id = id, value = Baseline };
            attributes.Add(entry);
            return entry;
        }

        public MasteryEntry Mastery(TechniqueId id)
        {
            foreach (var m in mastery)
                if (m.id == id) return m;
            var entry = new MasteryEntry { id = id };
            mastery.Add(entry);
            return entry;
        }

        public ExerciseRecord Record(string exerciseId)
        {
            foreach (var r in records)
                if (r.exerciseId == exerciseId) return r;
            var entry = new ExerciseRecord { exerciseId = exerciseId };
            records.Add(entry);
            return entry;
        }

        /// <summary>Repairs missing or invalid data after loading (GDD §12.3 validation).</summary>
        public void Sanitize()
        {
            attributes ??= new List<AttributeEntry>();
            mastery ??= new List<MasteryEntry>();
            fatigue ??= new FatigueState();
            aura ??= new AuraState();
            injury ??= new InjuryState();
            records ??= new List<ExerciseRecord>();
            history ??= new List<HistoryEntry>();
            knowledge ??= new List<string>();
            clearedEncounters ??= new List<string>();

            attributes.RemoveAll(a => a == null || !Enum.IsDefined(typeof(AttributeId), a.id));
            mastery.RemoveAll(m => m == null || !Enum.IsDefined(typeof(TechniqueId), m.id));
            records.RemoveAll(r => r == null || string.IsNullOrEmpty(r.exerciseId));
            history.RemoveAll(h => h == null);

            foreach (AttributeId id in Enum.GetValues(typeof(AttributeId)))
            {
                var a = Attribute(id);
                if (double.IsNaN(a.value) || double.IsInfinity(a.value) || a.value < 1) a.value = Baseline;
            }
            foreach (TechniqueId id in Enum.GetValues(typeof(TechniqueId)))
            {
                var m = Mastery(id);
                if (float.IsNaN(m.value) || float.IsInfinity(m.value)) m.value = 0;
                m.value = Mathf.Clamp(m.value, 0f, 10000f);
            }

            fatigue.immediate = SafeClamp(fatigue.immediate, 0, 100);
            fatigue.accumulated = SafeClamp(fatigue.accumulated, 0, 100);
            aura.current = SafeClamp(aura.current, 0, float.MaxValue);
            injury.upper = SafeClamp(injury.upper, 0, 100);
            injury.lower = SafeClamp(injury.lower, 0, 100);
            if (!aura.awakened) aura.current = 0;
            if (trainingSessions < 0) trainingSessions = 0;
            if (double.IsNaN(activePlaySeconds) || activePlaySeconds < 0) activePlaySeconds = 0;
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z)) hasPosition = false;
            while (history.Count > MaxHistory) history.RemoveAt(0);
        }

        static float SafeClamp(float v, float min, float max) => float.IsNaN(v) ? min : Mathf.Clamp(v, min, max);
    }
}
