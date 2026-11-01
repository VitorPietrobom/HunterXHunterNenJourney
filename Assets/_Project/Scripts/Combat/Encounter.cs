using System.Collections.Generic;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// A group of opponents out in the world (a chimera ant squad, or the King in his castle). They wait at their post
    /// until the player comes close — a player hidden in Zetsu can slip past — then all turn on them at once. Running
    /// far enough away makes them give up and regroup; falling in the fight sends the player back to the rest bench.
    /// Beating every member clears the encounter for good (saved), and the fight counts as sparring training.
    /// </summary>
    public class Encounter : MonoBehaviour
    {
        public static readonly List<Encounter> All = new List<Encounter>();

        [SerializeField] string id = "ants_1";
        [SerializeField] string displayName = "Chimera Ant squad";
        [SerializeField] bool isBoss;
        [SerializeField] EnemyController[] members = System.Array.Empty<EnemyController>();
        [Tooltip("Fixed stats per member, applied on start (same order as members).")]
        [SerializeField] RivalTier[] stats = System.Array.Empty<RivalTier>();
        [SerializeField] float aggroRadius = 12f;
        [SerializeField] float leashRadius = 32f;
        [SerializeField] Transform respawnPoint;
        [SerializeField] ExerciseDefinition sparringExercise;
        [SerializeField] ExerciseDefinition gyoReadingExercise;

        PlayerContext player;
        CombatTracker tracker;
        bool engaged;
        int remaining;

        public string Id => id;
        public string DisplayName => displayName;
        public bool IsBoss => isBoss;
        public bool Engaged => engaged;
        public bool Cleared => GameSession.Instance != null && GameSession.Instance.Progression.IsEncounterCleared(id);

        /// <summary>Set by the scene builder.</summary>
        public void Configure(string encounterId, string name, bool boss, EnemyController[] enemies, RivalTier[] tiers,
            float aggro, float leash, Transform respawn, ExerciseDefinition sparring, ExerciseDefinition gyo)
        {
            id = encounterId;
            displayName = name;
            isBoss = boss;
            members = enemies;
            stats = tiers;
            aggroRadius = aggro;
            leashRadius = leash;
            respawnPoint = respawn;
            sparringExercise = sparring;
            gyoReadingExercise = gyo;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            for (int i = 0; i < members.Length; i++)
                if (i < stats.Length) members[i].ApplyTier(stats[i]);
            if (Cleared)
                foreach (var m in members) m.gameObject.SetActive(false);
            foreach (var m in members) m.Provoked += OnProvoked;
        }

        void OnDestroy()
        {
            foreach (var m in members)
                if (m != null) m.Provoked -= OnProvoked;
        }

        /// <summary>Struck before it noticed the player (a Zetsu ambush): the whole group wakes.</summary>
        void OnProvoked()
        {
            if (engaged || Cleared || GameSession.Instance == null) return;
            if (player == null) player = GameSession.Instance.Player;
            Engage();
        }

        void Update()
        {
            var session = GameSession.Instance;
            if (session == null || session.Player == null || Cleared) return;
            if (player == null) player = session.Player;

            if (!engaged)
            {
                if (player.Health.IsDead || session.TitleOpen) return;
                if (Spots(player)) Engage();
                return;
            }

            Vector3 d = player.transform.position - transform.position;
            d.y = 0f;
            if (d.magnitude > leashRadius)
            {
                session.PushNotice(isBoss ? "You slip out of the throne room. The King waits." : $"The {displayName} loses interest and regroups.", 3f);
                End(false, regroup: true);
            }
        }

        /// <summary>Whether a waiting member notices the player: close enough, unless Zetsu hides them.</summary>
        bool Spots(PlayerContext p)
        {
            var progression = GameSession.Instance.Progression;
            float range = aggroRadius;
            if (p.Aura.ZetsuActive)
                range = Mathf.Min(range, StatEffects.ZetsuDetectionRange(progression.GetMastery(TechniqueId.Zetsu)));
            foreach (var m in members)
            {
                if (m.IsDefeated || !m.gameObject.activeInHierarchy) continue;
                Vector3 d = p.transform.position - m.transform.position;
                d.y = 0f;
                if (d.magnitude <= range) return true;
            }
            return false;
        }

        void Engage()
        {
            engaged = true;
            remaining = 0;
            tracker = new CombatTracker(Strongest());
            player.Combat.Tracker = tracker;
            player.Vitals.Exhausted += tracker.RegisterStaminaOut;
            player.Health.Died += OnPlayerDied;
            foreach (var m in members)
            {
                if (m.IsDefeated) continue;
                remaining++;
                m.Engage(player.Combat);
                m.Defeated += OnMemberDefeated;
                m.AttackLaunched += tracker.RegisterEnemyAttack;
                m.TelegraphStarted += OnTelegraphStarted;
            }
            var session = GameSession.Instance;
            session.PushNotice(isBoss ? "The King rises from his throne. \"So you are the one who came this far.\""
                : remaining > 1 ? $"Chimera ants! {remaining} of them — keep them in front of you." : "A chimera ant attacks!", 3.5f);
            ImpactEffects.RivalFlare(isBoss ? 3f : 1.5f);
        }

        RivalTier Strongest()
        {
            RivalTier best = null;
            foreach (var t in stats)
                if (best == null || t.maxHealth > best.maxHealth) best = t;
            if (best == null) return null;
            // The report names the encounter, not one member.
            return new RivalTier
            {
                id = id, displayName = displayName, maxHealth = best.maxHealth, attackDamage = best.attackDamage,
                telegraphTime = best.telegraphTime, presence = best.presence,
            };
        }

        void OnTelegraphStarted()
        {
            if (!player.Aura.GyoActive) return;
            tracker.RegisterGyoRead();
            GameSession.Instance.Progression.ApplyMastery(TechniqueId.Gyo, 0.5f);
        }

        void OnMemberDefeated()
        {
            remaining--;
            // The fallen stop blocking the way.
            foreach (var m in members)
                if (m.IsDefeated && m.TryGetComponent(out CharacterController cc)) cc.enabled = false;
            if (remaining <= 0) End(true, regroup: false);
        }

        void OnPlayerDied() => End(false, regroup: true);

        void End(bool won, bool regroup)
        {
            if (!engaged) return;
            engaged = false;
            var session = GameSession.Instance;
            var progression = session.Progression;

            player.Vitals.Exhausted -= tracker.RegisterStaminaOut;
            player.Health.Died -= OnPlayerDied;
            if (player.Combat.Tracker == tracker) player.Combat.Tracker = null;
            Health lastHealth = null;
            foreach (var m in members)
            {
                m.Defeated -= OnMemberDefeated;
                m.AttackLaunched -= tracker.RegisterEnemyAttack;
                m.TelegraphStarted -= OnTelegraphStarted;
                lastHealth = m.Health;
                if (!won) m.Deactivate();
            }

            var report = tracker.Finish(won, lastHealth, progression);
            var sparring = tracker.SparringOutcome(won, progression);
            var gyo = tracker.GyoOutcome(progression);
            tracker = null;

            if (won)
            {
                progression.MarkEncounterCleared(id);
                HitFeedback.SlowMotion(0.3f, isBoss ? 1.6f : 0.9f);
                if (isBoss) session.PushNotice("The King has fallen. You did it, Hunter — the Chimera Ant threat is over.", 10f);
                else
                {
                    int left = SquadsLeft();
                    session.PushNotice(left > 0 ? $"Squad cleared! {left} chimera ant squad{(left == 1 ? "" : "s")} left in the woods."
                        : "Every squad is down. The castle gate to the north has opened.", 6f);
                }
            }
            else if (regroup)
            {
                if (player.Health.IsDead)
                {
                    player.Health.RestoreFraction(0.5f);
                    if (respawnPoint != null) player.Motor.Teleport(respawnPoint.position, respawnPoint.eulerAngles.y);
                    progression.AddExertion(10f);
                    session.PushNotice("You were beaten and dragged yourself back to the rest bench. Train, then try again.", 6f);
                }
                // Survivors and the fallen alike regroup at full strength.
                foreach (var m in members) m.ResetToIdle();
            }

            session.ReportCombat(report);
            if (sparringExercise != null) progression.ApplyExercise(sparringExercise, sparring);
            if (gyo != null && gyoReadingExercise != null && progression.IsAuraAwakened) progression.ApplyExercise(gyoReadingExercise, gyo);
        }

        /// <summary>Ant squads (not the King) still standing.</summary>
        public static int SquadsLeft()
        {
            int left = 0;
            foreach (var e in All)
                if (!e.isBoss && !e.Cleared) left++;
            return left;
        }

        public static int SquadCount()
        {
            int n = 0;
            foreach (var e in All)
                if (!e.isBoss) n++;
            return n;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, aggroRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, leashRadius);
        }
    }
}
