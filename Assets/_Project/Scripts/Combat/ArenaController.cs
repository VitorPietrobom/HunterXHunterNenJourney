using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Starts and ends repeatable sparring matches against a chosen opponent tier, hands the combat report to the
    /// session, and submits the fight itself as training (sparring, plus Gyo reading when Gyo was used).
    /// </summary>
    public class ArenaController : Interactable
    {
        [SerializeField] EnemyController enemy;
        [SerializeField] Transform arenaCenter;
        [SerializeField] Transform entryPoint;
        [SerializeField] float arenaRadius = 13f;
        [SerializeField] RivalTier[] tiers = RivalTier.Defaults();
        [SerializeField] int tierIndex = 1;
        [SerializeField] ExerciseDefinition sparringExercise;
        [SerializeField] ExerciseDefinition gyoReadingExercise;

        PlayerContext player;
        CombatTracker tracker;
        bool matchActive;

        public bool MatchActive => matchActive;
        public EnemyController Enemy => enemy;
        RivalTier Tier => tiers != null && tiers.Length > 0 ? tiers[Mathf.Clamp(tierIndex, 0, tiers.Length - 1)] : null;

        void Start()
        {
            if (Tier != null) enemy.ApplyTier(Tier);
        }

        public override string GetPrompt()
        {
            if (matchActive) return "Sparring in progress — leave the arena to forfeit";
            var tier = Tier;
            if (tier == null) return "[E] Start a sparring match (restores your health)";
            float best = Progression.BestSparringTime(tier.id);
            string record = best > 0f ? $" · best {best:0.0}s" : "";
            return $"[E] Spar vs {tier.displayName} (HP {tier.maxHealth:0}, hits {tier.attackDamage:0}{record})   [ / ] change opponent";
        }

        public override void Adjust(int delta)
        {
            if (matchActive || tiers == null || tiers.Length == 0) return;
            int next = Mathf.Clamp(tierIndex + delta, 0, tiers.Length - 1);
            if (next == tierIndex) return;
            tierIndex = next;
            enemy.ApplyTier(Tier);
            Session.PushNotice($"Opponent: {Tier.displayName}", 2f);
        }

        public override void Interact(PlayerContext ctx)
        {
            if (matchActive) return;
            if (ctx.Motor.Locked) return;
            Begin(ctx);
        }

        void Begin(PlayerContext ctx)
        {
            player = ctx;
            var tier = Tier;
            if (tier != null) enemy.ApplyTier(tier);
            tracker = new CombatTracker(tier);
            matchActive = true;

            player.Health.RestoreFull();
            player.Vitals.RestoreFull();
            player.Combat.Tracker = tracker;
            player.Vitals.Exhausted += tracker.RegisterStaminaOut;
            player.Health.Died += OnPlayerDied;

            enemy.Activate(player.Combat);
            enemy.Defeated += OnEnemyDefeated;
            enemy.AttackLaunched += tracker.RegisterEnemyAttack;
            enemy.TelegraphStarted += OnTelegraphStarted;
            enemy.PatternRead += OnPatternRead;
            Session.PushNotice($"Fight! {enemy.DisplayName} — watch for the red wind-up.", 3f);
        }

        void OnTelegraphStarted()
        {
            if (!player.Aura.GyoActive) return;
            tracker.RegisterGyoRead();
            Progression.ApplyMastery(TechniqueId.Gyo, 0.5f);
        }

        void OnPatternRead()
        {
            tracker.RegisterPatternRead();
            Session.PushNotice($"The {enemy.DisplayName} is reading your repeated attacks.", 2f);
        }

        void OnEnemyDefeated() => End(true);
        void OnPlayerDied() => End(false);

        void Update()
        {
            if (!matchActive) return;
            Vector3 d = player.transform.position - arenaCenter.position;
            d.y = 0f;
            if (d.magnitude > arenaRadius)
            {
                Session.PushNotice("You left the arena — match forfeited.");
                End(false);
            }
        }

        void End(bool won)
        {
            if (!matchActive) return;
            matchActive = false;

            player.Vitals.Exhausted -= tracker.RegisterStaminaOut;
            player.Health.Died -= OnPlayerDied;
            player.Combat.Tracker = null;
            enemy.Defeated -= OnEnemyDefeated;
            enemy.AttackLaunched -= tracker.RegisterEnemyAttack;
            enemy.TelegraphStarted -= OnTelegraphStarted;
            enemy.PatternRead -= OnPatternRead;

            var report = tracker.Finish(won, enemy.Health, Progression);
            var sparring = tracker.SparringOutcome(won, Progression);
            var gyo = tracker.GyoOutcome(Progression);

            enemy.Deactivate();
            if (!won)
            {
                enemy.ResetToIdle();
                if (player.Health.IsDead)
                {
                    player.Health.RestoreFraction(0.5f);
                    if (entryPoint != null) player.Motor.Teleport(entryPoint.position, entryPoint.eulerAngles.y);
                    Progression.AddExertion(10f);
                }
            }
            else
            {
                HitFeedback.SlowMotion(0.3f, 0.9f);
                Invoke(nameof(ResetEnemy), 3f);
            }

            Session.ReportCombat(report);
            // The fight was training too (GDD §5.2: losing doesn't mean no progress).
            if (sparringExercise != null) Progression.ApplyExercise(sparringExercise, sparring);
            if (gyo != null && gyoReadingExercise != null && Progression.IsAuraAwakened) Progression.ApplyExercise(gyoReadingExercise, gyo);
            tracker = null;
            player = null;
        }

        void ResetEnemy()
        {
            if (!matchActive) enemy.ResetToIdle();
        }
    }
}
