using System.Collections.Generic;
using UnityEngine;

namespace HunterAwakening
{
    public class CombatReport
    {
        public bool won;
        public float duration;
        public string tierId;
        public string opponent;
        public string headline;
        public readonly List<string> stats = new List<string>();
        public readonly List<string> weaknesses = new List<string>();
    }

    /// <summary>
    /// Records what happened in a sparring match and turns it into causal feedback that points at a specific
    /// training activity (GDD §3: "identify a weakness, train deliberately"). The match itself is also training:
    /// it produces exercise outcomes for the ProgressionService (losing doesn't mean no progress, GDD §5.2).
    /// </summary>
    public class CombatTracker
    {
        readonly float startTime = Time.time;
        readonly RivalTier tier;

        int hitsLanded, hitsGuarded, guardBreaks, whiffs, punishes, renHits, ambushes;
        float damageDealt;
        int enemyAttacks, hitsTaken, blocks, parries, perfectDodges, hitsWithoutTen;
        float damageTaken;
        int staminaOuts, patternReads, renBreaks;
        int gyoReads, gyoReadsAvoided;
        bool currentAttackRead, pendingReadOutcome;

        public CombatTracker(RivalTier tier) => this.tier = tier ?? new RivalTier();

        public float Duration => Time.time - startTime;

        public void RegisterPlayerHit(HitResult result, float dealt, bool heavy, bool ren, bool punish, bool ambush = false)
        {
            damageDealt += dealt;
            switch (result)
            {
                case HitResult.Landed: hitsLanded++; break;
                case HitResult.Guarded: hitsGuarded++; break;
                case HitResult.GuardBroken: guardBreaks++; hitsLanded++; break;
            }
            if (ren && result != HitResult.Guarded) renHits++;
            if (punish) punishes++;
            if (ambush) ambushes++;
        }

        public void RegisterWhiff() => whiffs++;
        public void RegisterStaminaOut() => staminaOuts++;
        public void RegisterPatternRead() => patternReads++;
        public void RegisterRenBreak() => renBreaks++;

        /// <summary>The rival began a wind-up while the player was using Gyo.</summary>
        public void RegisterGyoRead()
        {
            gyoReads++;
            currentAttackRead = true;
        }

        public void RegisterEnemyAttack()
        {
            SettlePendingRead(true); // the previous read attack never touched the player: avoided by spacing
            enemyAttacks++;
            pendingReadOutcome = currentAttackRead;
            currentAttackRead = false;
        }

        public void RegisterPerfectDodge()
        {
            perfectDodges++;
            SettlePendingRead(true);
        }

        public void RegisterParry()
        {
            parries++;
            SettlePendingRead(true);
        }

        public void RegisterBlock()
        {
            blocks++;
            SettlePendingRead(true);
        }

        public void RegisterPlayerDamaged(float amount, bool blocked, bool tenActive)
        {
            damageTaken += amount;
            if (blocked) return;
            hitsTaken++;
            if (!tenActive) hitsWithoutTen++;
            SettlePendingRead(false);
        }

        void SettlePendingRead(bool avoided)
        {
            if (!pendingReadOutcome) return;
            pendingReadOutcome = false;
            if (avoided) gyoReadsAvoided++;
        }

        public CombatReport Finish(bool won, Health enemyHealth, ProgressionService p)
        {
            SettlePendingRead(true);
            float duration = Duration;
            var r = new CombatReport { won = won, duration = duration, tierId = tier.id, opponent = tier.displayName };
            float previousBest = p.BestSparringTime(tier.id);
            r.headline = won ? $"Victory vs {tier.displayName} in {duration:0.0}s" : $"Defeat — {tier.displayName} at {enemyHealth.Fraction:P0} health";

            int landedOrGuarded = hitsLanded + hitsGuarded;
            float avgDamage = landedOrGuarded > 0 ? damageDealt / landedOrGuarded : 0f;
            r.stats.Add($"Duration {duration:0.0}s{(previousBest > 0f ? $" (best win vs {tier.displayName} {previousBest:0.0}s)" : "")}");
            r.stats.Add($"Hits landed {hitsLanded} · guarded {hitsGuarded} · guard breaks {guardBreaks} · whiffs {whiffs} · punishes {punishes}" + (ambushes > 0 ? $" · ambushes {ambushes}" : ""));
            r.stats.Add($"Damage dealt {damageDealt:0} (avg {avgDamage:0.0}/hit) · taken {damageTaken:0}");
            r.stats.Add($"Opponent attacks {enemyAttacks}: hit you {hitsTaken} · blocked {blocks} · parried {parries} · perfect dodges {perfectDodges}");
            r.stats.Add($"Stamina ran out {staminaOuts}×" + (p.IsAuraAwakened ? $" · Ren hits {renHits} · Ren broke {renBreaks}× · Gyo reads {gyoReads} ({gyoReadsAvoided} avoided)" : ""));

            // Weakness analysis: each line names the cause and the training that addresses it.
            if (landedOrGuarded > 0)
            {
                float hitsToKill = enemyHealth.Max / Mathf.Max(0.1f, avgDamage);
                if (hitsToKill > 20f)
                    r.weaknesses.Add($"Low damage: ~{hitsToKill:0} average hits needed to win. Train Strength at the Lift Rack{(p.IsAuraAwakened ? " and use Ren (hold R) on openings" : "")}.");
            }
            if (staminaOuts > 0)
                r.weaknesses.Add($"You ran out of stamina {staminaOuts}× and lost tempo. Train Endurance on the Sprint Course.");
            int avoided = parries + perfectDodges;
            if (hitsTaken >= 3 && avoided < hitsTaken)
                r.weaknesses.Add($"{hitsTaken} telegraphed attacks connected (avoided {avoided}). Dodge (Ctrl) as the opponent flashes red, or tap Q just before impact to parry." +
                                 (p.IsAuraAwakened ? " Hold Gyo (G) to read the exact moment of impact." : " Coordination (Precision Pads) widens the dodge window."));
            if (hitsGuarded >= 4 || patternReads >= 2)
                r.weaknesses.Add($"The opponent read your repeated pattern and guarded {hitsGuarded} attacks. Mix in heavy attacks (RMB) — they break guard.");
            if (punishes == 0 && enemyAttacks >= 3)
                r.weaknesses.Add("You never punished the opponent's recovery (yellow). Attack right after it whiffs for bonus quality.");
            if (p.IsAuraAwakened && hitsWithoutTen >= 2)
                r.weaknesses.Add($"{hitsWithoutTen} hits landed without Ten or Ren. Toggle Ten (T) before engaging, or hold Ren (R) to brace.");
            if (renBreaks > 0)
                r.weaknesses.Add($"Ren broke under pressure {renBreaks}× — your output outpaces your control. Train Control Precision at the Aura Mat.");
            if (p.IsAuraAwakened && renHits == 0 && hitsLanded >= 5)
                r.weaknesses.Add("You never struck with Ren. Hold R as you hit an opening: Ren hits break guard, stagger and knock back.");
            if (!p.IsAuraAwakened && !won && p.TrainingSessions >= 3)
                r.weaknesses.Add("Your aura is still dormant. Meditate at the Meditation Stone to awaken it.");
            if (won && r.weaknesses.Count == 0)
                r.weaknesses.Add($"A clean win. Try a stronger opponent at the arena post ([ / ]) — the {tier.displayName} will not get stronger.");
            return r;
        }

        /// <summary>
        /// The fight as training: Durability from absorbing a real opponent's pressure, Coordination from avoiding it,
        /// Endurance from lasting. Short fights count partially; an opponent far below the player's level is "too easy".
        /// </summary>
        public ExerciseOutcome SparringOutcome(bool won, ProgressionService p)
        {
            float offense = hitsLanded / Mathf.Max(1f, hitsLanded + hitsGuarded + whiffs);
            float defense = enemyAttacks > 0 ? Mathf.Clamp01((enemyAttacks - hitsTaken - 0.5f * blocks) / enemyAttacks) : 0.5f;
            float durabilityComfort = 24f * (1f + ProgressionMath.Gain(p.GetAttribute(AttributeId.Durability)));
            var o = new ExerciseOutcome
            {
                completion = Mathf.Clamp01(Duration / 40f),
                quality = Mathf.Clamp01(0.5f * offense + 0.5f * defense + (won ? 0.1f : 0f)),
                intensity = Mathf.Clamp(tier.attackDamage / durabilityComfort, 0.3f, 1.4f)
            };
            o.metrics.Add($"Opponent: {tier.displayName} · fought {Duration:0}s");
            o.metrics.Add($"Offense {HudGui.Pct(offense)} (landed vs guarded/whiffed) · defense {HudGui.Pct(defense)} (attacks avoided)");
            return o;
        }

        /// <summary>Gyo reading as Perception training, or null if Gyo was never used on a wind-up.</summary>
        public ExerciseOutcome GyoOutcome(ProgressionService p)
        {
            if (gyoReads == 0) return null;
            float comfort = 1f + 0.3f * ProgressionMath.Gain(p.GetAttribute(AttributeId.Perception));
            var o = new ExerciseOutcome
            {
                completion = Mathf.Clamp01((float)gyoReads / Mathf.Max(1, enemyAttacks)),
                quality = Mathf.Clamp01((float)gyoReadsAvoided / gyoReads),
                intensity = Mathf.Clamp(0.8f / Mathf.Max(0.2f, tier.telegraphTime) / comfort, 0.3f, 1.4f)
            };
            o.metrics.Add($"Read {gyoReads} of {enemyAttacks} wind-ups with Gyo · avoided {gyoReadsAvoided}");
            return o;
        }
    }
}
