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
        /// <summary>
        /// Ko and Jajanken are aura strikes: their power comes from Nen, so the body adds only half its strength bonus
        /// (otherwise a lifter's ×3 strength multiplies a ×5 Hatsu and one-shots every opponent).
        /// </summary>
        public static float NenStrikeStrengthFactor(double strength) => 1f + 0.5f * (StrengthDamageFactor(strength) - 1f);
        public static float OneRepMaxKg(double strength) => 50f * (1f + 1.2f * G(strength));
        // Enhancer affinity (Water Divination): aura-powered strikes and natural healing.
        public const float EnhancerStrikeMultiplier = 1.1f;
        public const float EnhancerRegenMultiplier = 1.5f;

        // Hunter Level: experience from real fights, on top of what training builds.
        public const float HunterLevelHealthBonus = 0.03f;
        public const float HunterLevelDamageBonus = 0.02f;
        /// <summary>XP needed to go from <paramref name="level"/> to the next one (60, 100, 140, …).</summary>
        public static double XpToNextLevel(int level) => 20 + 40 * level;
        public static int HunterLevel(double xp)
        {
            int level = 1;
            while (level < 99 && xp >= XpToNextLevel(level))
            {
                xp -= XpToNextLevel(level);
                level++;
            }
            return level;
        }
        /// <summary>XP gathered towards the next level, and how much that level needs.</summary>
        public static (double into, double needed) LevelProgress(double xp)
        {
            int level = 1;
            while (level < 99 && xp >= XpToNextLevel(level))
            {
                xp -= XpToNextLevel(level);
                level++;
            }
            return (xp, XpToNextLevel(level));
        }
        public static float HunterLevelHealthFactor(int level) => 1f + HunterLevelHealthBonus * (Mathf.Max(1, level) - 1);
        public static float HunterLevelDamageFactor(int level) => 1f + HunterLevelDamageBonus * (Mathf.Max(1, level) - 1);

        public static float MaxHealth(double durability, double endurance) => 100f * (1f + 0.6f * G(durability) + 0.25f * G(endurance));
        public static float DamageReduction(double durability) { float g = G(durability); return g / (g + 3f); }
        public static float ComfortableLaps(double endurance) => 2f * (1f + 0.8f * G(endurance));
        /// <summary>Seconds the player comfortably needs to reach a precision pad.</summary>
        public static float ComfortWindow(double coordination, double speed) => 2.4f / (1f + 0.5f * G(coordination) + 0.2f * G(speed));
        /// <summary>Extra dodge invulnerability (seconds) from Coordination.</summary>
        public static float DodgeWindowBonus(double coordination) => Mathf.Min(0.04f * G(coordination), 0.12f);

        // Aura
        // Aura is a daily budget: a large pool, no passive regeneration. A long rest at the bench refills it; out in the
        // field, standing still in Zetsu recovers it slowly. Holding techniques is cheap (AuraDrainScale), the big
        // strikes are not, so a full pool carries a camp fight with Ren up and a few Rocks — but not two in a row.
        public static float AuraCapacity(double capacity) => 500f * (1f + 0.8f * G(capacity));
        /// <summary>Share of each technique's power that a second of holding it costs (Ten, Ren, Gyo, the drills).</summary>
        public const float AuraDrainScale = 0.2f;
        /// <summary>Seconds on the rest bench to refill an empty pool (a short sit is a partial refill).</summary>
        public static float RestRefillSeconds(double recovery) => 20f / (1f + 0.5f * G(recovery));
        /// <summary>Aura per second recovered standing still in Zetsu.</summary>
        public static float ZetsuAuraRecovery(double recovery) => 3f * (1f + 0.7f * G(recovery));
        public static float Efficiency(double efficiency) => Mathf.Max(ProgressionMath.MinEfficiency, 1f + 0.5f * G(efficiency));
        public static float RenOutput(double maxOutput) => 12f * (1f + 0.8f * G(maxOutput));
        public static float RenDamageFactor(double maxOutput, double control) => Mathf.Min(1.4f + 0.35f * G(maxOutput) + 0.15f * G(control), 3f);

        /// <summary>How far Max Output has outgrown Control Precision (0 while control keeps up). Drives Ren instability.</summary>
        public static float RenOverdrive(double maxOutput, double control) => Mathf.Max(0f, G(maxOutput) - G(control));

        public static float RenLeakage(double maxOutput, double control, float renMastery)
            => Mathf.Clamp(0.25f + 0.35f * RenOverdrive(maxOutput, control) - 0.1f * G(control)
                           - (renMastery >= MasteryMilestones.RenLowLeak ? 0.1f : 0f), 0.05f, 0.8f);

        /// <summary>Chance that a hit taken while holding Ren knocks it loose.</summary>
        public static float RenBreakChance(double maxOutput, double control, float renMastery)
            => Mathf.Clamp(0.12f + 0.3f * RenOverdrive(maxOutput, control) - Mathf.Min(renMastery, 100f) / 400f, 0.03f, 0.7f);

        /// <summary>Damage-taken multiplier while Ren is held (stronger than Ten, much more expensive).</summary>
        public static float RenDamageTakenMultiplier(double maxOutput) => Mathf.Clamp(0.6f - 0.07f * G(maxOutput), 0.3f, 0.6f);

        // Ren floods the body with aura: faster movement and attacks, paid for with extra drain while moving.
        public static float RenSpeedMultiplier(double maxOutput) => Mathf.Min(1.15f + 0.08f * G(maxOutput), 1.35f);
        public static float RenAttackSpeedMultiplier(double maxOutput) => Mathf.Min(1.1f + 0.06f * G(maxOutput), 1.25f);
        public static float RenMovingDrainMultiplier(double sustained) => 1f + 0.5f / (1f + 0.6f * G(sustained));

        /// <summary>How large Ren's visible aura burns: 1 at awakening, growing with Max Output and Ren mastery.</summary>
        public static float RenPresence(double maxOutput, float renMastery)
            => Mathf.Min(1f + 0.55f * G(maxOutput) + 0.5f * Mathf.Clamp01(renMastery / 100f), 3.5f);

        public static float RenStagger(double maxOutput) => Mathf.Min(0.35f + 0.1f * G(maxOutput), 0.9f);
        public static float RenKnockback(double maxOutput) => Mathf.Min(2.5f + 1.5f * G(maxOutput), 8f);

        // Zetsu: aura shut off. Hidden and recovering fast, but defenceless.
        public const float ZetsuDamageTakenMultiplier = 1.5f;
        public const float ZetsuAmbushDamageMultiplier = 1.6f;
        /// <summary>Distance at which a rival notices a player hiding in Zetsu.</summary>
        public static float ZetsuDetectionRange(float zetsuMastery) => Mathf.Lerp(6f, 3f, Mathf.Clamp01(zetsuMastery / 100f));

        // Gyo: aura gathered in the eyes. Reads attacks; the body is a little less protected meanwhile.
        public const float GyoDamageTakenMultiplier = 1.15f;
        public static float GyoDrain(float gyoMastery) => gyoMastery >= MasteryMilestones.GyoCheap ? 2f : 3f;
        public static float GyoParryBonus(double perception) => Mathf.Min(0.04f + 0.03f * G(perception), 0.15f);
        public static float GyoDodgeBonus(double perception) => Mathf.Min(0.02f + 0.02f * G(perception), 0.08f);

        // Ken: Ten + Ren as one stance. Far sturdier than either alone; rooted (no Ren speed), steadier Ren.
        public static float KenDamageTakenMultiplier(double maxOutput, double control, float tenMastery)
            => Mathf.Clamp(Mathf.Min(TenDamageMultiplier(control, tenMastery), RenDamageTakenMultiplier(maxOutput)) * 0.75f, 0.2f, 0.5f);
        public static float KenDrainMultiplier(float kenMastery) => kenMastery >= MasteryMilestones.KenCheap ? 0.85f : 1.15f;
        public const float KenBreakChanceMultiplier = 0.5f;

        // Ko: all aura into one strike. Huge damage, big cost, and a defenceless body while it's thrown.
        public static float KoDamageMultiplier(double maxOutput) => Mathf.Min(2.5f + 0.3f * G(maxOutput), 4f);
        public static float KoAuraCost(float koMastery) => koMastery >= MasteryMilestones.KoCheap ? 30f : 40f;
        public const float KoDamageTakenMultiplier = 1.8f;
        public const float KoStagger = 1.3f;

        // Jajanken (Rock): Gon's Hatsu. Power grows with each word of the chant; the chant roots you and leaves you open.
        // Mastery shortens the chant and raises the ceiling; completing it is the vow that adds the final burst.
        public static float JajankenChargeTime(float mastery) => Mathf.Lerp(2.4f, 1.3f, Mathf.Clamp01(mastery / 100f));
        /// <summary>Aura a full chant gathers into the hand (spent as the chant builds, whatever its length).</summary>
        public static float JajankenChantCost(double maxOutput) => 50f * (1f + 0.2f * G(maxOutput));
        /// <summary>Charge below this ("Saisho wa guu" alone) gathers nothing; releasing cancels.</summary>
        public const float JajankenMinCharge = 0.34f;
        public const float JajankenChantDamageTakenMultiplier = 1.35f;
        public const float JajankenFullChantBonus = 1.25f;
        /// <summary>Paper trades power for range; Scissors sits between Paper and Rock.</summary>
        public const float JajankenPaperFactor = 0.6f;
        public const float JajankenScissorsFactor = 0.75f;
        /// <summary>Rock's shockwave and Paper's burst: share of the hit dealt to others nearby, and their reach.</summary>
        public const float SplashDamageFactor = 0.5f;
        public const float RockShockwaveRadius = 3f;
        public const float PaperBurstRadius = 2.5f;

        // Combos: each follow-up in a light-light-heavy chain hits harder; the heavy finisher hits hardest.
        public static float ComboDamageMultiplier(int step, bool finisher) => finisher ? 1.35f : 1f + 0.15f * Mathf.Clamp(step, 0, 2);

        public static float JajankenDamageMultiplier(float charge, double maxOutput, float mastery)
        {
            charge = Mathf.Clamp01(charge);
            float ceiling = 3.5f + 0.8f * G(maxOutput) + 1.2f * Mathf.Clamp01(mastery / 100f);
            float multiplier = Mathf.Lerp(1.6f, ceiling, charge * charge);
            if (charge >= 0.999f) multiplier *= JajankenFullChantBonus;
            return Mathf.Min(multiplier, 9f);
        }

        /// <summary>Highest output (0..1 of the Ren Circle meter) the player can hold before Ren destabilises.</summary>
        public static float RenControlCeiling(double control) => Mathf.Clamp(0.7f + 0.08f * G(control), 0.7f, 0.97f);
        /// <summary>Output level (0..1) that is a comfortable challenge for the player's Max Output.</summary>
        public static float RenComfortOutput(double maxOutput) => Mathf.Clamp(0.5f + 0.08f * G(maxOutput), 0.5f, 0.9f);
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
            AttributeId.MaxOutput => $"Ren output {RenOutput(v):0}/s · guard −{(1f - RenDamageTakenMultiplier(v)) * 100:0}% damage · speed +{(RenSpeedMultiplier(v) - 1f) * 100:0}% · attacks +{(RenAttackSpeedMultiplier(v) - 1f) * 100:0}%",
            AttributeId.SustainedOutput => $"Ren strain {RenFatiguePerSecond(v):0.00} fatigue/s · moving drain ×{RenMovingDrainMultiplier(v):0.00}",
            AttributeId.ControlPrecision => $"Stable band ±{TenBand(v) * 100:0}% · Ren ceiling {RenControlCeiling(v) * 100:0}% (keep ahead of Max Output)",
            AttributeId.Efficiency => $"Efficiency ×{Efficiency(v):0.00}",
            AttributeId.Perception => $"With Gyo: parry window +{GyoParryBonus(v) * 1000:0} ms · dodge window +{GyoDodgeBonus(v) * 1000:0} ms",
            AttributeId.RecoveryRate => $"Rest refills aura in {RestRefillSeconds(v):0}s · Zetsu recovers {ZetsuAuraRecovery(v):0.0}/s",
            _ => ""
        };
    }
}
