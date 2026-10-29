using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Watches play for breakthrough conditions (GDD §5.5) and asks the ProgressionService to unlock them.
    /// Per-match counters reset when a match starts. Exposes live progress text for the dashboard.
    /// </summary>
    [RequireComponent(typeof(PlayerContext))]
    public class BreakthroughTracker : MonoBehaviour
    {
        const float TenSprintSeconds = 8f;
        const float RenHoldSeconds = 10f;
        const int RenHitsAbsorbed = 2;
        const int GyoAnswers = 3;
        const int AmbushesPerMatch = 2;

        PlayerContext player;
        bool inMatch;
        float tenSprint, bestTenSprint;
        float renHold;
        int renAbsorbed;
        int gyoAnswers, ambushes;

        ProgressionService P => GameSession.Instance.Progression;

        void Awake() => player = GetComponent<PlayerContext>();

        void Start()
        {
            player.Combat.HitTaken += OnHitTaken;
            player.Combat.Parried += OnAnswered;
            player.Combat.PerfectDodged += OnAnswered;
            player.Combat.Ambushed += OnAmbushed;
            GameSession.Instance.CombatReported += OnCombatReported;
            P.ExerciseApplied += OnExerciseApplied;
        }

        void OnDestroy()
        {
            if (player != null && player.Combat != null)
            {
                player.Combat.HitTaken -= OnHitTaken;
                player.Combat.Parried -= OnAnswered;
                player.Combat.PerfectDodged -= OnAnswered;
                player.Combat.Ambushed -= OnAmbushed;
            }
            if (GameSession.Instance == null) return;
            GameSession.Instance.CombatReported -= OnCombatReported;
            GameSession.Instance.Progression.ExerciseApplied -= OnExerciseApplied;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var aura = player.Aura;

            bool matchNow = player.Combat.Tracker != null;
            if (matchNow && !inMatch) gyoAnswers = ambushes = renAbsorbed = 0;
            inMatch = matchNow;

            if (aura.TenActive && player.Motor.IsSprinting)
            {
                tenSprint += dt;
                bestTenSprint = Mathf.Max(bestTenSprint, tenSprint);
                if (tenSprint >= TenSprintSeconds) Unlock(Breakthroughs.TenInMotion);
            }
            else tenSprint = 0f;

            if (inMatch && aura.RenActive) renHold += dt;
            else
            {
                renHold = 0f;
                renAbsorbed = 0;
            }
            if (renHold >= RenHoldSeconds && renAbsorbed >= RenHitsAbsorbed) Unlock(Breakthroughs.UnbrokenRen);
        }

        void OnHitTaken()
        {
            // HitTaken fires after Ren's break roll, so Ren still being up means the hit was absorbed.
            if (inMatch && player.Aura.RenActive) renAbsorbed++;
        }

        void OnAnswered()
        {
            if (!inMatch || !player.Aura.GyoActive) return;
            if (++gyoAnswers >= GyoAnswers) Unlock(Breakthroughs.ReadAndAnswer);
        }

        void OnAmbushed()
        {
            if (!inMatch) return;
            if (++ambushes >= AmbushesPerMatch) Unlock(Breakthroughs.ShadowStrike);
        }

        void OnCombatReported(CombatReport report)
        {
            if (report.won && (report.tierId == "veteran" || report.tierId == "elite")) Unlock(Breakthroughs.IronBody);
        }

        void OnExerciseApplied(TrainingReport report)
        {
            var o = report.outcome;
            // Lift intensity is relative load / 0.8, so 1.1 ≈ 88% of the estimated one-rep max.
            if (report.exerciseId == "lift_rack" && report.adapted && o.completion >= 0.999f && o.quality >= 0.9f && o.intensity >= 1.1f)
                Unlock(Breakthroughs.PerfectForm);
        }

        void Unlock(string id)
        {
            var def = Breakthroughs.Find(id);
            if (def == null || P.HasBreakthrough(id)) return;
            if (def.Value.requiresAura && !P.IsAuraAwakened) return;
            if (!P.UnlockBreakthrough(id)) return;
            HitFeedback.SlowMotion(0.5f, 0.6f);
            GameSession.Instance.PushNotice($"BREAKTHROUGH — {def.Value.title}: {def.Value.reward}", 7f);
        }

        /// <summary>Live progress toward a breakthrough, for the dashboard.</summary>
        public string Progress(string id) => id switch
        {
            Breakthroughs.TenInMotion => $"best {bestTenSprint:0.0}/{TenSprintSeconds:0}s",
            Breakthroughs.UnbrokenRen => inMatch ? $"Ren held {renHold:0.0}/{RenHoldSeconds:0}s · hits absorbed {renAbsorbed}/{RenHitsAbsorbed}" : "start a match",
            Breakthroughs.ReadAndAnswer => inMatch ? $"{gyoAnswers}/{GyoAnswers} this match" : "start a match",
            Breakthroughs.ShadowStrike => inMatch ? $"{ambushes}/{AmbushesPerMatch} this match" : "start a match",
            _ => ""
        };
    }
}
