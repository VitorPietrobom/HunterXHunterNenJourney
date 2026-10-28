using System.Collections.Generic;
using UnityEngine;

namespace HunterAwakening
{
    public class CombatReport
    {
        public bool won;
        public float duration;
        public string headline;
        public readonly List<string> stats = new List<string>();
        public readonly List<string> weaknesses = new List<string>();
    }

    /// <summary>
    /// Records what happened in a sparring match and turns it into causal feedback that points at a specific
    /// training activity (GDD §3: "identify a weakness, train deliberately").
    /// </summary>
    public class CombatTracker
    {
        readonly float startTime = Time.time;

        int hitsLanded, hitsGuarded, guardBreaks, whiffs, punishes, renHits;
        float damageDealt;
        int enemyAttacks, hitsTaken, blocks, parries, perfectDodges, hitsWithoutTen;
        float damageTaken;
        int staminaOuts, patternReads;

        public void RegisterPlayerHit(HitResult result, float dealt, bool heavy, bool ren, bool punish)
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
        }

        public void RegisterWhiff() => whiffs++;
        public void RegisterEnemyAttack() => enemyAttacks++;
        public void RegisterPerfectDodge() => perfectDodges++;
        public void RegisterParry() => parries++;
        public void RegisterBlock() => blocks++;
        public void RegisterStaminaOut() => staminaOuts++;
        public void RegisterPatternRead() => patternReads++;

        public void RegisterPlayerDamaged(float amount, bool blocked, bool tenActive)
        {
            damageTaken += amount;
            if (blocked) return;
            hitsTaken++;
            if (!tenActive) hitsWithoutTen++;
        }

        public CombatReport Finish(bool won, Health enemyHealth, ProgressionService p)
        {
            float duration = Time.time - startTime;
            var r = new CombatReport { won = won, duration = duration };
            float previousBest = p.BestSparringTime;
            r.headline = won ? $"Victory in {duration:0.0}s" : $"Defeat — rival at {enemyHealth.Fraction:P0} health";

            int landedOrGuarded = hitsLanded + hitsGuarded;
            float avgDamage = landedOrGuarded > 0 ? damageDealt / landedOrGuarded : 0f;
            r.stats.Add($"Duration {duration:0.0}s{(previousBest > 0f ? $" (best win {previousBest:0.0}s)" : "")}");
            r.stats.Add($"Hits landed {hitsLanded} · guarded {hitsGuarded} · guard breaks {guardBreaks} · whiffs {whiffs} · punishes {punishes}");
            r.stats.Add($"Damage dealt {damageDealt:0} (avg {avgDamage:0.0}/hit) · taken {damageTaken:0}");
            r.stats.Add($"Rival attacks {enemyAttacks}: hit you {hitsTaken} · blocked {blocks} · parried {parries} · perfect dodges {perfectDodges}");
            r.stats.Add($"Stamina ran out {staminaOuts}×");

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
                r.weaknesses.Add($"{hitsTaken} telegraphed attacks connected (avoided {avoided}). Dodge (Ctrl) as the rival flashes red, or tap Q just before impact to parry. Coordination (Precision Pads) widens the dodge window.");
            if (hitsGuarded >= 4 || patternReads >= 2)
                r.weaknesses.Add($"The rival read your repeated pattern and guarded {hitsGuarded} attacks. Mix in heavy attacks (RMB) — they break guard.");
            if (punishes == 0 && enemyAttacks >= 3)
                r.weaknesses.Add("You never punished the rival's recovery (yellow). Attack right after it whiffs for bonus quality.");
            if (p.IsAuraAwakened && hitsWithoutTen >= 2)
                r.weaknesses.Add($"{hitsWithoutTen} hits landed without Ten. Toggle Ten (T) before engaging to reduce damage.");
            if (!p.IsAuraAwakened && !won && p.TrainingSessions >= 3)
                r.weaknesses.Add("Your aura is still dormant. Meditate at the Meditation Stone to awaken it.");
            if (won && r.weaknesses.Count == 0)
                r.weaknesses.Add("A clean win. Raise drill intensities to keep progressing — this rival will not get stronger.");
            return r;
        }
    }
}
