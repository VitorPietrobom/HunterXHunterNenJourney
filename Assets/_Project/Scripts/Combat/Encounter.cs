using System.Collections.Generic;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// A camp of opponents out in the world (a chimera ant squad, or the King in his castle). Each member waits at its
    /// post and notices the player on its own — come close to one and only that one attacks, so a careful hunter can
    /// pull them one or two at a time. A player hidden in Zetsu can get closer, and a strike on an unaware member
    /// opens with an ambush. A member chased too far from its post gives up and goes back healed; falling in a fight
    /// sends the player back to the rest bench. Every kill grants Hunter XP. Beaten ants return after a while (so they
    /// can be hunted again); the King does not. Having every member down at once clears the camp for good (saved).
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
        [SerializeField] float aggroRadius = 8f;
        [Tooltip("How far from its post a member follows the player before giving up.")]
        [SerializeField] float leashRadius = 30f;
        [Tooltip("Seconds before a beaten member returns (0 = never).")]
        [SerializeField] float respawnTime = 90f;
        [SerializeField] Transform respawnPoint;
        [SerializeField] ExerciseDefinition sparringExercise;
        [SerializeField] ExerciseDefinition gyoReadingExercise;

        PlayerContext player;
        CombatTracker tracker;
        float[] defeatedAt;
        bool fightLost;

        public string Id => id;
        public string DisplayName => displayName;
        public bool IsBoss => isBoss;
        /// <summary>Any member is fighting the player.</summary>
        public bool Engaged
        {
            get
            {
                foreach (var m in members)
                    if (m.IsActive) return true;
                return false;
            }
        }
        /// <summary>Beaten for good at least once (opens the castle; ants still come back to be hunted).</summary>
        public bool Cleared => GameSession.Instance != null && GameSession.Instance.Progression.IsEncounterCleared(id);

        /// <summary>Set by the scene builder.</summary>
        public void Configure(string encounterId, string name, bool boss, EnemyController[] enemies, RivalTier[] tiers,
            float aggro, float leash, float respawn, Transform respawnAt, ExerciseDefinition sparring, ExerciseDefinition gyo)
        {
            id = encounterId;
            displayName = name;
            isBoss = boss;
            members = enemies;
            stats = tiers;
            aggroRadius = aggro;
            leashRadius = leash;
            respawnTime = respawn;
            respawnPoint = respawnAt;
            sparringExercise = sparring;
            gyoReadingExercise = gyo;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            defeatedAt = new float[members.Length];
            for (int i = 0; i < members.Length; i++)
            {
                var m = members[i];
                int index = i;
                if (i < stats.Length) m.ApplyTier(stats[i]);
                m.Provoked += () => OnProvoked(m);
                m.Defeated += () => OnMemberDefeated(index);
                m.AttackLaunched += () => tracker?.RegisterEnemyAttack();
                m.TelegraphStarted += OnTelegraphStarted;
            }
            // A beaten King stays beaten.
            if (isBoss && Cleared)
                foreach (var m in members) m.gameObject.SetActive(false);
        }

        void Update()
        {
            var session = GameSession.Instance;
            if (session == null || session.Player == null) return;
            if (player == null) player = session.Player;
            if (session.TitleOpen) return;

            for (int i = 0; i < members.Length; i++)
            {
                var m = members[i];
                if (!m.gameObject.activeInHierarchy) continue;
                if (m.IsDefeated)
                {
                    TryRespawn(i);
                    continue;
                }
                float fromPost = Flat(player.transform.position - m.SpawnPosition);
                if (m.IsActive)
                {
                    // Chased too far from its post: it gives up and goes home to lick its wounds.
                    if (fromPost > leashRadius)
                    {
                        m.ResetToIdle();
                        fightLost = true;
                        session.PushNotice(isBoss ? "You slip out of the throne room. The King waits." : $"The {m.DisplayName} gives up the chase.", 2.5f);
                    }
                }
                else if (!player.Health.IsDead && Flat(player.transform.position - m.transform.position) <= AggroRange())
                {
                    Engage(m);
                }
            }
            if (tracker != null && !Engaged) EndFight();
        }

        static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }

        /// <summary>How close the player can get before a member notices: Zetsu hides their presence.</summary>
        float AggroRange()
        {
            if (!player.Aura.ZetsuActive) return aggroRadius;
            float zetsu = StatEffects.ZetsuDetectionRange(GameSession.Instance.Progression.GetMastery(TechniqueId.Zetsu));
            return Mathf.Min(aggroRadius, zetsu);
        }

        /// <summary>Struck before it noticed the player (a Zetsu ambush): only that one wakes.</summary>
        void OnProvoked(EnemyController m)
        {
            if (GameSession.Instance == null) return;
            if (player == null) player = GameSession.Instance.Player;
            Engage(m);
        }

        void Engage(EnemyController m)
        {
            if (m.IsActive || m.IsDefeated) return;
            if (tracker == null) StartFight();
            m.Engage(player.Combat);
            var session = GameSession.Instance;
            if (isBoss) session.PushNotice("The King rises from his throne. \"So you are the one who came this far.\"", 4f);
            else session.PushNotice($"A {m.DisplayName} has noticed you!", 2f);
            ImpactEffects.RivalFlare(isBoss ? 3f : 1f);
        }

        void StartFight()
        {
            fightLost = false;
            tracker = new CombatTracker(ReportTier());
            player.Combat.Tracker = tracker;
            player.Vitals.Exhausted += tracker.RegisterStaminaOut;
            player.Health.Died += OnPlayerDied;
        }

        RivalTier ReportTier()
        {
            RivalTier best = null;
            foreach (var t in stats)
                if (best == null || t.maxHealth > best.maxHealth) best = t;
            if (best == null) return null;
            // The report names the camp, not one member.
            return new RivalTier
            {
                id = id, displayName = displayName, maxHealth = best.maxHealth, attackDamage = best.attackDamage,
                telegraphTime = best.telegraphTime, presence = best.presence,
            };
        }

        void OnTelegraphStarted()
        {
            if (tracker == null || !player.Aura.GyoActive) return;
            tracker.RegisterGyoRead();
            GameSession.Instance.Progression.ApplyMastery(TechniqueId.Gyo, 0.5f);
        }

        void OnMemberDefeated(int index)
        {
            var m = members[index];
            defeatedAt[index] = Time.time;
            if (m.TryGetComponent(out CharacterController cc)) cc.enabled = false; // the fallen stop blocking the way

            var session = GameSession.Instance;
            var progression = session.Progression;
            GrantXp(index < stats.Length ? stats[index].xp : 0f, m.transform.position);

            bool allDown = true;
            foreach (var other in members)
                if (!other.IsDefeated) allDown = false;
            if (!allDown) return;

            bool first = !Cleared;
            progression.MarkEncounterCleared(id);
            HitFeedback.SlowMotion(0.3f, isBoss ? 1.6f : 0.9f);
            if (isBoss) session.PushNotice("The King has fallen. You did it, Hunter — the Chimera Ant threat is over.", 10f);
            else if (first)
            {
                int left = SquadsLeft();
                session.PushNotice(left > 0 ? $"Camp cleared! {left} chimera ant camp{(left == 1 ? "" : "s")} left in the woods."
                    : "Every camp is down. The castle gate to the north has opened.", 6f);
            }
            else session.PushNotice("Camp cleared again. They'll be back.", 3f);
        }

        void OnPlayerDied()
        {
            var session = GameSession.Instance;
            fightLost = true;
            // Whoever was fighting goes home healed; the dead stay dead until they respawn.
            foreach (var m in members)
                if (m.IsActive) m.ResetToIdle();
            if (player.Health.IsDead) // another camp in the same brawl may have handled it already
            {
                player.Health.RestoreFraction(0.5f);
                if (respawnPoint != null) player.Motor.Teleport(respawnPoint.position, respawnPoint.eulerAngles.y);
                session.Progression.AddExertion(10f);
                session.PushNotice("You were beaten and dragged yourself back to the rest bench. Train, then try again.", 6f);
            }
            EndFight();
        }

        /// <summary>No member is fighting any more: report the fight, and count it as training.</summary>
        void EndFight()
        {
            if (tracker == null) return;
            var session = GameSession.Instance;
            var progression = session.Progression;
            player.Vitals.Exhausted -= tracker.RegisterStaminaOut;
            player.Health.Died -= OnPlayerDied;
            if (player.Combat.Tracker == tracker) player.Combat.Tracker = null;

            bool won = !fightLost;
            Health strongest = members.Length > 0 ? members[0].Health : null;
            var report = tracker.Finish(won, strongest, progression);
            var sparring = tracker.SparringOutcome(won, progression);
            var gyo = tracker.GyoOutcome(progression);
            tracker = null;

            session.ReportCombat(report);
            if (sparringExercise != null) progression.ApplyExercise(sparringExercise, sparring);
            if (gyo != null && gyoReadingExercise != null && progression.IsAuraAwakened) progression.ApplyExercise(gyoReadingExercise, gyo);
        }

        /// <summary>A beaten ant comes back once its time is up — never in front of the player.</summary>
        void TryRespawn(int index)
        {
            if (isBoss || respawnTime <= 0f || Time.time - defeatedAt[index] < respawnTime) return;
            var m = members[index];
            if (Flat(player.transform.position - m.SpawnPosition) < aggroRadius + 15f) return;
            m.ResetToIdle();
        }

        /// <summary>Hunter XP for a beaten opponent: a callout where it fell, and a full heal and fanfare on a level-up.</summary>
        public static void GrantXp(float xp, Vector3 at)
        {
            var session = GameSession.Instance;
            if (session == null || xp <= 0f) return;
            DamageNumbers.Callout(at + Vector3.up * 2.6f, $"+{xp:0} XP");
            if (session.Progression.AddHunterXp(xp) <= 0) return;
            var p = session.Player;
            if (p == null) return;
            p.Health.RestoreFull();
            p.Vitals.RestoreFull();
            DamageNumbers.Callout(p.transform.position + Vector3.up * 2.4f, $"LEVEL UP!  Lv {session.Progression.HunterLevel}");
            ImpactEffects.RivalFlare(2.5f);
            HitFeedback.SlowMotion(0.4f, 0.6f);
        }

        /// <summary>Ant camps (not the King) never yet cleared.</summary>
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
            foreach (var m in members)
            {
                if (m == null) continue;
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(m.transform.position, aggroRadius);
            }
        }
    }
}
