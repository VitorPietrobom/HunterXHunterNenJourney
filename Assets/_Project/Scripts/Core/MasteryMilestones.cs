using System.Collections.Generic;

namespace HunterAwakening
{
    /// <summary>
    /// Qualitative mastery unlocks. Each threshold constant is checked by the gameplay code that implements it.
    /// </summary>
    public static class MasteryMilestones
    {
        public const float LightDodgeCancel = 30f;
        public const float HeavyLongStagger = 30f;
        public const float DodgeLongIFrames = 30f;
        public const float BlockWideParry = 25f;
        public const float LiftWideForm = 30f;
        public const float FootworkCheapDodge = 30f;
        public const float TenHalfDrain = 30f;
        public const float RenLowLeak = 30f;
        public const float ZetsuLongAmbush = 30f;
        public const float GyoCheap = 30f;
        public const float KenCheap = 30f;
        public const float KoCheap = 30f;
        public const float JajankenSteady = 30f;

        // Unlock thresholds for the advanced applications (checked by AuraController).
        public const float KenUnlockTenMastery = 25f;
        public const float KenUnlockRenMastery = 25f;
        public const float KoUnlockRenMastery = 40f;
        public const double KoUnlockMaxOutput = 18;
        public const float JajankenUnlockRenMastery = 50f;

        public readonly struct Milestone
        {
            public readonly float threshold;
            public readonly string description;
            public Milestone(float threshold, string description) { this.threshold = threshold; this.description = description; }
        }

        static readonly Dictionary<TechniqueId, Milestone[]> Table = new Dictionary<TechniqueId, Milestone[]>
        {
            { TechniqueId.LightAttack, new[] { new Milestone(LightDodgeCancel, "Cancel recovery into a dodge") } },
            { TechniqueId.HeavyAttack, new[] { new Milestone(HeavyLongStagger, "Guard breaks stagger 50% longer") } },
            { TechniqueId.Dodge, new[] { new Milestone(DodgeLongIFrames, "Longer invulnerability window") } },
            { TechniqueId.Block, new[] { new Milestone(BlockWideParry, "Wider parry timing window") } },
            { TechniqueId.Lift, new[] { new Milestone(LiftWideForm, "Wider form window under heavy loads") } },
            { TechniqueId.Footwork, new[] { new Milestone(FootworkCheapDodge, "Dodging costs 30% less stamina") } },
            { TechniqueId.Ten, new[] { new Milestone(TenHalfDrain, "Ten aura drain halved") } },
            { TechniqueId.Ren, new[] { new Milestone(RenLowLeak, "Less aura leakage while holding Ren") } },
            { TechniqueId.Zetsu, new[] { new Milestone(ZetsuLongAmbush, "Ambushes from Zetsu stagger 50% longer") } },
            { TechniqueId.Gyo, new[] { new Milestone(GyoCheap, "Gyo drains a third less aura") } },
            { TechniqueId.Ken, new[] { new Milestone(KenCheap, "Ken drains 25% less aura") } },
            { TechniqueId.Ko, new[] { new Milestone(KoCheap, "Ko costs 25% of your reserve instead of 35%") } },
            { TechniqueId.Jajanken, new[] { new Milestone(JajankenSteady, "A hit mid-chant only halves the charge instead of scattering it") } },
        };

        public static IReadOnlyList<Milestone> For(TechniqueId id) =>
            Table.TryGetValue(id, out var list) ? list : System.Array.Empty<Milestone>();

        public static string Tier(float mastery) => mastery switch
        {
            < 10f => "Novice",
            < 30f => "Practiced",
            < 60f => "Reliable",
            < 100f => "Refined",
            < 150f => "Mastered",
            _ => "Beyond"
        };

        /// <summary>Returns the description of a milestone crossed between before and after, or null.</summary>
        public static string Crossed(TechniqueId id, float before, float after)
        {
            foreach (var m in For(id))
                if (before < m.threshold && after >= m.threshold) return m.description;
            return null;
        }

        public static string Next(TechniqueId id, float mastery)
        {
            foreach (var m in For(id))
                if (mastery < m.threshold) return $"{m.threshold:0}: {m.description}";
            return mastery < 100f ? "100: Mastered tier" : "Advanced tier — slow, continued refinement";
        }
    }
}
