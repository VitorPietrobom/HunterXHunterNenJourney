using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Maps raw (uncapped) attributes to gameplay values. Every effect goes through a log curve and,
    /// where physics or animation could break, a hard clamp on the *effect* (never on the stored attribute).
    /// </summary>
    public static class StatEffects
    {
        const float BaseRunSpeed = 4.2f;
        const float SprintMultiplier = 1.65f;

        static float G(double a) => ProgressionMath.Gain(a);

        // Physical
        public static float RunSpeed(double speed) => Mathf.Min(BaseRunSpeed * (1f + 0.45f * G(speed)), 20f);
        public static float SprintSpeed(double speed) => Mathf.Min(RunSpeed(speed) * SprintMultiplier, 32f);
        public static float MaxStamina(double endurance) => 100f * (1f + 0.7f * G(endurance));
        public static float StaminaRegen(double endurance) => 16f * (1f + 0.6f * G(endurance));
        public static float SprintStaminaCost(double endurance) => 22f / (1f + 0.3f * G(endurance));
        public static float JumpHeight(double mobility) => Mathf.Min(1.1f * (1f + 0.5f * G(mobility)), 8f);
        public static float StrengthDamageFactor(double strength) => 1f + 0.9f * G(strength);
        public static float OneRepMaxKg(double strength) => 50f * (1f + 1.2f * G(strength));
        public static float MaxHealth(double durability, double endurance) => 100f * (1f + 0.6f * G(durability) + 0.25f * G(endurance));
        public static float DamageReduction(double durability) { float g = G(durability); return g / (g + 3f); }
        public static float ComfortableLaps(double endurance) => 2f * (1f + 0.8f * G(endurance));
        /// <summary>Seconds the player comfortably needs to reach a precision pad.</summary>
        public static float ComfortWindow(double coordination, double speed) => 2.4f / (1f + 0.5f * G(coordination) + 0.2f * G(speed));
        /// <summary>Extra dodge invulnerability (seconds) from Coordination.</summary>
        public static float DodgeWindowBonus(double coordination) => Mathf.Min(0.04f * G(coordination), 0.12f);

        // Aura
        public static float AuraCapacity(double capacity) => 100f * (1f + 0.8f * G(capacity));
        public static float AuraRecovery(double recovery) => 4f * (1f + 0.7f * G(recovery));
        public static float Efficiency(double efficiency) => Mathf.Max(ProgressionMath.MinEfficiency, 1f + 0.5f * G(efficiency));
        public static float RenOutput(double maxOutput) => 12f * (1f + 0.8f * G(maxOutput));
        public static float RenDamageFactor(double maxOutput, double control) => Mathf.Min(1.4f + 0.35f * G(maxOutput) + 0.15f * G(control), 3f);
        public static float RenLeakage(double control, float renMastery)
            => Mathf.Clamp(0.45f - 0.12f * G(control) - (renMastery >= MasteryMilestones.RenLowLeak ? 0.1f : 0f), 0.05f, 0.45f);
        /// <summary>Immediate fatigue per second while holding Ren; Sustained Output reduces it.</summary>
        public static float RenFatiguePerSecond(double sustained) => 0.8f / (1f + 0.6f * G(sustained));
        public static float TenDrain(float tenMastery) => tenMastery >= MasteryMilestones.TenHalfDrain ? 0.75f : 1.5f;
        public static float TenDamageMultiplier(double control, float tenMastery)
            => Mathf.Clamp(0.7f - 0.08f * G(control) - 0.001f * tenMastery, 0.35f, 0.8f);
        /// <summary>Half-width of the stable band in the aura control drill.</summary>
        public static float TenBand(double control) => 0.1f * (1f + 0.35f * G(control));

        public static string Describe(AttributeId id, double v) => id switch
        {
            AttributeId.Strength => $"Damage ×{StrengthDamageFactor(v):0.00} · est. 1RM {OneRepMaxKg(v):0} kg",
            AttributeId.Speed => $"Run {RunSpeed(v):0.0} m/s · sprint {SprintSpeed(v):0.0} m/s",
            AttributeId.Endurance => $"Stamina {MaxStamina(v):0} · regen {StaminaRegen(v):0}/s · comfortable laps {ComfortableLaps(v):0.0}",
            AttributeId.Coordination => $"Pad reach {ComfortWindow(v, 10):0.00}s · dodge window +{DodgeWindowBonus(v) * 1000:0} ms",
            AttributeId.Mobility => $"Jump height {JumpHeight(v):0.00} m",
            AttributeId.Durability => $"Damage taken −{DamageReduction(v) * 100:0}%",
            AttributeId.AuraCapacity => $"Aura reserve {AuraCapacity(v):0}",
            AttributeId.MaxOutput => $"Ren output {RenOutput(v):0}/s",
            AttributeId.SustainedOutput => $"Ren strain {RenFatiguePerSecond(v):0.00} fatigue/s",
            AttributeId.ControlPrecision => $"Stable band ±{TenBand(v) * 100:0}%",
            AttributeId.Efficiency => $"Efficiency ×{Efficiency(v):0.00}",
            AttributeId.Perception => "No gameplay effect yet (future: aura cues)",
            AttributeId.RecoveryRate => $"Aura recovery {AuraRecovery(v):0.0}/s",
            _ => ""
        };
    }
}
