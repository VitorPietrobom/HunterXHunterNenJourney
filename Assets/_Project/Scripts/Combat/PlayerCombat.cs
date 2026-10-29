using System;
using UnityEngine;

namespace HunterAwakening
{
    public struct HitInfo
    {
        public float damage;
        public TechniqueId technique;
        public bool heavy;
        /// <summary>Delivered with Ren: breaks guard even when light, staggers and knocks back.</summary>
        public bool ren;
        public float renStagger;
        public float knockback;
        /// <summary>Struck from Zetsu, unseen: ignores guard and wind-up armour and staggers.</summary>
        public bool ambush;
    }

    public enum HitResult { None, Landed, Guarded, GuardBroken }

    /// <summary>
    /// Light/heavy attacks with startup/active/recovery phases, blocking with a parry window, and hit reception.
    /// Execution quality (spacing, punishing openings) is scored per action and sent to the ProgressionService.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Serializable]
        public class AttackSpec
        {
            public string name;
            public TechniqueId technique;
            public bool heavy;
            public float startup;
            public float active;
            public float recovery;
            public float baseDamage;
            public float staminaCost;
            public float range;
            [Range(0f, 1f)] public float moveMultiplier;
        }

        public enum Phase { None, Startup, Active, Recovery }

        [SerializeField] AttackSpec lightAttack = new AttackSpec
        {
            name = "Light", technique = TechniqueId.LightAttack, startup = 0.12f, active = 0.1f, recovery = 0.28f,
            baseDamage = 8f, staminaCost = 8f, range = 1.9f, moveMultiplier = 0.3f
        };
        [SerializeField] AttackSpec heavyAttack = new AttackSpec
        {
            name = "Heavy", technique = TechniqueId.HeavyAttack, heavy = true, startup = 0.45f, active = 0.12f, recovery = 0.55f,
            baseDamage = 20f, staminaCost = 20f, range = 2.1f, moveMultiplier = 0.1f
        };
        [SerializeField] float blockDamageMultiplier = 0.3f;
        [SerializeField] float blockStaminaPerHit = 14f;
        [SerializeField] float parryWindow = 0.18f;
        [SerializeField] float engageRadius = 6f;

        InputReader input;
        PlayerMotor motor;
        PlayerVitals vitals;
        Health health;
        AuraController aura;
        AttackSpec current;
        float phaseTimer;
        float recoveryDuration;
        float blockPressedTime = -10f;

        public Phase CurrentPhase { get; private set; }
        public bool IsAttacking => CurrentPhase != Phase.None;
        public bool IsHeavyAttack => IsAttacking && current != null && current.heavy;
        /// <summary>Wind-up time of the current attack, after fatigue.</summary>
        public float StartupDuration { get; private set; }
        /// <summary>Active + recovery time of the current attack, after mastery and fatigue.</summary>
        public float FollowThroughDuration => current != null ? current.active + recoveryDuration : 0f;

        /// <summary>Raised when an unblocked hit lands on the player.</summary>
        public event Action HitTaken;
        public bool IsBlocking { get; private set; }
        public bool IsDefeated => health.IsDead;
        public CombatTracker Tracker { get; set; }

        /// <summary>
        /// Can the player dodge right now? Normally only when idle; Light Attack mastery unlocks cancelling recovery.
        /// </summary>
        public bool CanDodge => CurrentPhase == Phase.None ||
                                (CurrentPhase == Phase.Recovery && current == lightAttack &&
                                 P.HasMastery(TechniqueId.LightAttack, MasteryMilestones.LightDodgeCancel));

        /// <summary>Raised when an attack starts its visible wind-up. Enemies react to this, never to raw input.</summary>
        public event Action<AttackSpec> AttackStarted;

        ProgressionService P => GameSession.Instance.Progression;

        void Awake()
        {
            input = GetComponent<InputReader>();
            motor = GetComponent<PlayerMotor>();
            vitals = GetComponent<PlayerVitals>();
            health = GetComponent<Health>();
            aura = GetComponent<AuraController>();
            motor.Dodged += OnDodged;
        }

        void OnDestroy() => motor.Dodged -= OnDodged;

        void OnDodged()
        {
            if (IsAttacking) CurrentPhase = Phase.None;
        }

        void Update()
        {
            bool blocked = GameSession.Instance.InputBlocked || motor.Locked || IsDefeated;

            if (!blocked && input.BlockPressed) blockPressedTime = Time.time;
            IsBlocking = !blocked && input.BlockHeld && CurrentPhase == Phase.None && !motor.IsDodging && !vitals.IsExhausted;

            if (!blocked && CurrentPhase == Phase.None && !motor.IsDodging)
            {
                if (input.LightPressed) Begin(lightAttack);
                else if (input.HeavyPressed) Begin(heavyAttack);
            }

            if (IsAttacking) AdvancePhase(Time.deltaTime);
            motor.MoveMultiplier = IsAttacking ? current.moveMultiplier : IsBlocking ? 0.45f : 1f;
        }

        void Begin(AttackSpec spec)
        {
            if (!vitals.TrySpend(spec.staminaCost))
            {
                GameSession.Instance.PushNotice("Not enough stamina to attack.", 1.5f);
                return;
            }
            var target = NearestEnemy(engageRadius);
            if (target != null) motor.FaceTowards(target.transform.position);

            current = spec;
            CurrentPhase = Phase.Startup;
            float performance = P.Fatigue.PerformanceMultiplier * aura.AttackSpeedMultiplier;
            StartupDuration = spec.startup / performance;
            // Mastery shortens recovery; fatigue lengthens it.
            float masteryCut = Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(P.GetMastery(spec.technique) / 100f));
            recoveryDuration = spec.recovery * masteryCut / performance;
            phaseTimer = StartupDuration;
            P.AddExertion(spec.heavy ? 0.6f : 0.25f);
            AttackStarted?.Invoke(spec);
        }

        void AdvancePhase(float dt)
        {
            phaseTimer -= dt;
            if (phaseTimer > 0f) return;
            switch (CurrentPhase)
            {
                case Phase.Startup:
                    CurrentPhase = Phase.Active;
                    phaseTimer = current.active;
                    ResolveHit();
                    break;
                case Phase.Active:
                    CurrentPhase = Phase.Recovery;
                    phaseTimer = recoveryDuration;
                    break;
                default:
                    CurrentPhase = Phase.None;
                    break;
            }
        }

        void ResolveHit()
        {
            var target = TargetInArc(current.range, 60f, out float distance);
            if (target == null)
            {
                if (NearestEnemy(engageRadius) != null) Tracker?.RegisterWhiff();
                return; // whiffs grant no mastery: shadow-boxing cannot be farmed
            }

            float ideal = current.range * 0.7f;
            float spacing = 1f - Mathf.Clamp01(Mathf.Abs(distance - ideal) / ideal);
            bool punish = target.IsVulnerable;
            float quality = Mathf.Clamp01(0.4f + 0.4f * spacing + (punish ? 0.3f : 0f));

            float strength = StatEffects.StrengthDamageFactor(P.GetAttribute(AttributeId.Strength));
            float technique = 1f + 0.35f * Mathf.Clamp01(P.GetMastery(current.technique) / 100f);
            float execution = Mathf.Lerp(0.85f, 1.1f, quality);
            float nen = aura.OutgoingMultiplier;
            bool ambush = aura.ZetsuActive;
            float damage = current.baseDamage * strength * technique * execution * nen * Mathf.Lerp(0.8f, 1f, P.Fatigue.Readiness)
                           * (ambush ? StatEffects.ZetsuAmbushDamageMultiplier : 1f);

            double maxOutput = P.GetAttribute(AttributeId.MaxOutput);
            var hit = new HitInfo { damage = damage, technique = current.technique, heavy = current.heavy, ren = aura.RenActive, ambush = ambush };
            if (ambush)
            {
                aura.EndZetsu("Ambush from Zetsu!");
                P.ApplyMastery(TechniqueId.Zetsu, 1f);
                HitFeedback.SlowMotion(0.35f, 0.35f);
            }
            if (hit.ren)
            {
                hit.renStagger = StatEffects.RenStagger(maxOutput) * (current.heavy ? 1.6f : 1f);
                hit.knockback = StatEffects.RenKnockback(maxOutput) * (current.heavy ? 1.5f : 1f);
            }
            var result = target.ReceiveHit(hit, out float dealt);
            if (result == HitResult.Guarded) quality *= 0.3f;

            P.ApplyMastery(current.technique, quality);
            if (aura.RenActive && result != HitResult.Guarded) P.ApplyMastery(TechniqueId.Ren, 0.7f);
            Tracker?.RegisterPlayerHit(result, dealt, current.heavy, aura.RenActive, punish, hit.ambush);

            switch (result)
            {
                case HitResult.GuardBroken: HitFeedback.Impact(0.1f, 0.45f); break;
                case HitResult.Guarded: HitFeedback.Impact(0.02f, 0.1f); break;
                default: HitFeedback.Impact((current.heavy ? 0.08f : 0.04f) + (hit.ren ? 0.03f : 0f), (current.heavy ? 0.3f : 0.15f) + (hit.ren ? 0.15f : 0f)); break;
            }
        }

        /// <summary>Called by an enemy whose attack connects this frame.</summary>
        public void ReceiveHit(float damage, EnemyController attacker)
        {
            if (IsDefeated) return;
            var tracker = Tracker;

            if (motor.IsInvulnerable)
            {
                P.ApplyMastery(TechniqueId.Dodge, 1f);
                tracker?.RegisterPerfectDodge();
                GameSession.Instance.PushNotice("Perfect dodge!", 1f);
                HitFeedback.SlowMotion(0.3f, 0.45f);
                return;
            }

            Vector3 toAttacker = attacker.transform.position - transform.position;
            toAttacker.y = 0f;
            bool facing = Vector3.Angle(transform.forward, toAttacker) < 75f;
            bool blocked = false;

            if (IsBlocking && facing)
            {
                float window = parryWindow + (P.HasMastery(TechniqueId.Block, MasteryMilestones.BlockWideParry) ? 0.1f : 0f) + aura.ParryWindowBonus;
                if (Time.time - blockPressedTime <= window)
                {
                    attacker.Stagger(1.1f);
                    P.ApplyMastery(TechniqueId.Block, 1f);
                    tracker?.RegisterParry();
                    GameSession.Instance.PushNotice("Parry! The rival is open.", 1.5f);
                    HitFeedback.Impact(0.12f, 0.3f);
                    HitFeedback.SlowMotion(0.4f, 0.4f);
                    return;
                }
                blocked = true;
                bool guardHeld = vitals.Stamina >= blockStaminaPerHit;
                vitals.Drain(blockStaminaPerHit);
                damage *= guardHeld ? blockDamageMultiplier : blockDamageMultiplier * 2f;
                P.ApplyMastery(TechniqueId.Block, guardHeld ? 0.5f : 0.2f);
                tracker?.RegisterBlock();
            }
            else if (motor.IsDodging)
            {
                P.ApplyMastery(TechniqueId.Dodge, 0.2f); // mistimed dodge still teaches something
            }

            bool tenWasActive = aura.TenActive;
            bool renWasActive = aura.RenActive;
            damage *= aura.IncomingMultiplier;
            if (tenWasActive) P.ApplyMastery(TechniqueId.Ten, 0.5f);
            damage *= 1f - StatEffects.DamageReduction(P.GetAttribute(AttributeId.Durability));

            // Ren protected this hit; the impact may still knock it loose for the next ones.
            if (renWasActive)
            {
                if (aura.TryBreakRen()) tracker?.RegisterRenBreak();
                else P.ApplyMastery(TechniqueId.Ren, 0.4f);
            }

            float dealt = health.TakeDamage(damage);
            P.AddExertion(1.5f);
            tracker?.RegisterPlayerDamaged(dealt, blocked, tenWasActive || renWasActive);
            if (!blocked && dealt > 0f) HitTaken?.Invoke();
            HitFeedback.Impact(blocked ? 0.03f : 0.06f, blocked ? 0.2f : 0.5f);
        }

        EnemyController NearestEnemy(float radius)
        {
            EnemyController best = null;
            float bestDistance = radius;
            foreach (var e in EnemyController.All)
            {
                if (!e.IsActive) continue;
                float d = FlatDistance(e.transform.position);
                if (d < bestDistance)
                {
                    best = e;
                    bestDistance = d;
                }
            }
            return best;
        }

        EnemyController TargetInArc(float range, float halfAngle, out float distance)
        {
            distance = 0f;
            EnemyController best = null;
            float bestDistance = float.MaxValue;
            foreach (var e in EnemyController.All)
            {
                if (!e.IsActive) continue;
                float d = FlatDistance(e.transform.position);
                if (d > range + e.Radius) continue;
                Vector3 to = e.transform.position - transform.position;
                to.y = 0f;
                if (Vector3.Angle(transform.forward, to) > halfAngle) continue;
                if (d < bestDistance)
                {
                    best = e;
                    bestDistance = d;
                }
            }
            if (best != null) distance = Mathf.Max(0f, bestDistance - best.Radius);
            return best;
        }

        float FlatDistance(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            return d.magnitude;
        }
    }
}
