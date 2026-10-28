using UnityEngine;

namespace HunterAwakening
{
    /// <summary>Starts and ends repeatable sparring matches and hands the combat report to the session.</summary>
    public class ArenaController : Interactable
    {
        [SerializeField] EnemyController enemy;
        [SerializeField] Transform arenaCenter;
        [SerializeField] Transform entryPoint;
        [SerializeField] float arenaRadius = 13f;

        PlayerContext player;
        CombatTracker tracker;
        bool matchActive;

        public bool MatchActive => matchActive;
        public EnemyController Enemy => enemy;

        public override string GetPrompt() => matchActive
            ? "Sparring in progress — leave the arena to forfeit"
            : "[E] Start a sparring match (restores your health)";

        public override void Interact(PlayerContext ctx)
        {
            if (matchActive) return;
            if (ctx.Motor.Locked) return;
            Begin(ctx);
        }

        void Begin(PlayerContext ctx)
        {
            player = ctx;
            tracker = new CombatTracker();
            matchActive = true;

            player.Health.RestoreFull();
            player.Vitals.RestoreFull();
            player.Combat.Tracker = tracker;
            player.Vitals.Exhausted += tracker.RegisterStaminaOut;
            player.Health.Died += OnPlayerDied;

            enemy.Activate(player.Combat);
            enemy.Defeated += OnEnemyDefeated;
            enemy.AttackLaunched += tracker.RegisterEnemyAttack;
            enemy.PatternRead += OnPatternRead;
            Session.PushNotice("Fight! Watch for the red wind-up.", 3f);
        }

        void OnPatternRead()
        {
            tracker.RegisterPatternRead();
            Session.PushNotice("The rival is reading your repeated attacks.", 2f);
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
            enemy.PatternRead -= OnPatternRead;

            var report = tracker.Finish(won, enemy.Health, Progression);
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
            else Invoke(nameof(ResetEnemy), 3f);

            Session.ReportCombat(report);
            tracker = null;
            player = null;
        }

        void ResetEnemy()
        {
            if (!matchActive) enemy.ResetToIdle();
        }
    }
}
