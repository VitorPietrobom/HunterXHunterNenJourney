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
        [Tooltip("Jajanken — Rock: the punch released at the end of the chant (power comes from the charge).")]
        [SerializeField] AttackSpec rockAttack = new AttackSpec
        {
            name = "Rock", technique = TechniqueId.Jajanken, heavy = true, startup = 0.1f, active = 0.12f, recovery = 0.7f,
            baseDamage = 20f, staminaCost = 25f, range = 2.4f, moveMultiplier = 0f
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
        /// <summary>The current attack is a Ko strike (all aura in one fist).</summary>
        public bool IsKoStrike => IsAttacking && currentIsKo;
        bool currentIsKo;

        /// <summary>The Jajanken chant is building (rooted, open to punishment).</summary>
        public bool IsChargingRock { get; private set; }
        /// <summary>0..1: how much of the chant is complete.</summary>
        public float RockCharge { get; private set; }
        /// <summary>Seconds a full chant takes at the current Jajanken mastery.</summary>
        public float RockChargeTime { get; private set; } = 2.4f;
        /// <summary>The current attack is a released Rock.</summary>
        public bool IsRockStrike => IsAttacking && currentIsRock;
        /// <summary>Raised when Rock is thrown, with the charge it carries.</summary>
        public event Action<float> RockReleased;
        bool currentIsRock;
        float rockCharge;
        int chantStage;
        static readonly string[] Chant = { "Saisho wa guu!", "Jan…", "Ken…", "— everything into the fist!" };
        /// <summary>Wind-up time of the current attack, after fatigue.</summary>
        public float StartupDuration { get; private set; }
        /// <summary>Active + recovery time of the current attack, after mastery and fatigue.</summary>
        public float FollowThroughDuration => current != null ? current.active + recoveryDuration : 0f;

        /// <summary>Raised when an unblocked hit lands on the player.</summary>
        public event Action HitTaken;
        public event Action Parried;
        public event Action PerfectDodged;
        public event Action Ambushed;
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
            if (IsChargingRock) CancelChant("You broke off the chant.");
        }

        void Update()
        {
            bool blocked = GameSession.Instance.InputBlocked || motor.Locked || IsDefeated;

            if (!blocked && input.BlockPressed) blockPressedTime = Time.time;
            IsBlocking = !blocked && input.BlockHeld && CurrentPhase == Phase.None && !IsChargingRock && !motor.IsDodging && !vitals.IsExhausted;

            if (IsChargingRock) TickChant(Time.deltaTime, blocked);
            else if (!blocked && CurrentPhase == Phase.None && !motor.IsDodging)
            {
                if (input.LightPressed) Begin(lightAttack);
                else if (input.HeavyPressed) Begin(heavyAttack);
                else if (input.KoPressed) TryKo();
                else if (input.HatsuPressed) TryChant();
            }

            if (IsAttacking) AdvancePhase(Time.deltaTime);
            motor.MoveMultiplier = IsChargingRock ? 0f : IsAttacking ? current.moveMultiplier : IsBlocking ? 0.45f : 1f;
        }

        void TryKo()
        {
            if (!aura.KoUnlocked) return;
            var session = GameSession.Instance;
            if (!aura.RenActive)
            {
                session.PushNotice("Ko needs Ren — hold R, then press F.", 2f);
                return;
            }
            if (vitals.Stamina < heavyAttack.staminaCost)
            {
                session.PushNotice("Not enough stamina for Ko.", 1.5f);
                return;
            }
            if (!aura.TrySpendKo())
            {
                session.PushNotice("Not enough aura for Ko.", 1.5f);
                return;
            }
            Begin(heavyAttack, true);
        }

        // ------------------------------------------------------------------ Jajanken: Rock

        void TryChant()
        {
            if (!aura.JajankenUnlocked) return;
            var session = GameSession.Instance;
            if (aura.ZetsuActive)
            {
                session.PushNotice("Zetsu has sealed your aura — there is nothing to gather.", 2f);
                return;
            }
            if (vitals.Stamina < rockAttack.staminaCost)
            {
                session.PushNotice("Not enough stamina for Rock.", 1.5f);
                return;
            }
            if (aura.Current < 10f)
            {
                session.PushNotice("Not enough aura for Rock.", 1.5f);
                return;
            }
            IsChargingRock = true;
            RockCharge = 0f;
            chantStage = 0;
            RockChargeTime = StatEffects.JajankenChargeTime(P.GetMastery(TechniqueId.Jajanken));
            var target = NearestEnemy(engageRadius);
            if (target != null) motor.FaceTowards(target.transform.position);
            session.PushNotice(Chant[0], 1.2f);
            ImpactEffects.ChantBeat(0);
        }

        void TickChant(float dt, bool blocked)
        {
            if (blocked)
            {
                CancelChant(null);
                return;
            }
            if (!input.HatsuHeld)
            {
                ReleaseRock();
                return;
            }
            float drain = StatEffects.JajankenAuraPerSecond(P.GetAttribute(AttributeId.MaxOutput)) * dt;
            if (!aura.TrySpend(drain))
            {
                CancelChant("Your aura ran dry — the chant collapses.");
                return;
            }
            RockCharge = Mathf.Min(1f, RockCharge + dt / RockChargeTime);
            aura.HatsuCharge = RockCharge;
            P.AddExertion(0.6f * dt);
            int stage = RockCharge >= 1f ? 3 : RockCharge >= 2f / 3f ? 2 : RockCharge >= StatEffects.JajankenMinCharge ? 1 : 0;
            if (stage > chantStage)
            {
                chantStage = stage;
                GameSession.Instance.PushNotice(Chant[stage], stage == 3 ? 2f : 1.2f);
                ImpactEffects.ChantBeat(stage);
                if (stage == 3) HitFeedback.Shake(0.12f);
            }
            var target = NearestEnemy(engageRadius);
            if (target != null) motor.FaceTowards(target.transform.position);
        }

        void ReleaseRock()
        {
            float charge = RockCharge;
            EndChant();
            if (charge < StatEffects.JajankenMinCharge)
            {
                GameSession.Instance.PushNotice("Too soon — the chant gathered nothing.", 1.5f);
                return;
            }
            rockCharge = charge;
            Begin(rockAttack, rock: true);
            if (!IsAttacking) return; // no stamina left to throw it
            GameSession.Instance.PushNotice(charge >= 1f ? "GUU!!" : "Guu!", 1.5f);
            ImpactEffects.RockThrow(charge);
            RockReleased?.Invoke(charge);
        }

        /// <summary>A hit, a dodge or running dry breaks the chant; the aura already spent is gone.</summary>
        void CancelChant(string notice)
        {
            EndChant();
            if (notice != null) GameSession.Instance.PushNotice(notice, 2f);
        }

        void EndChant()
        {
            IsChargingRock = false;
            RockCharge = 0f;
            aura.HatsuCharge = 0f;
        }

        void Begin(AttackSpec spec, bool ko = false, bool rock = false)
        {
            if (!vitals.TrySpend(spec.staminaCost))
            {
                GameSession.Instance.PushNotice("Not enough stamina to attack.", 1.5f);
                return;
            }
            var target = NearestEnemy(engageRadius);
            if (target != null) motor.FaceTowards(target.transform.position);

            current = spec;
            currentIsKo = ko;
            currentIsRock = rock;
            CurrentPhase = Phase.Startup;
            float performance = P.Fatigue.PerformanceMultiplier * aura.AttackSpeedMultiplier;
            // Gathering everything into the fist takes Ko a moment; Rock gathered it during the chant.
            StartupDuration = spec.startup / performance * (ko ? 1.3f : 1f);
            // Mastery shortens recovery; fatigue lengthens it.
            float masteryCut = Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(P.GetMastery(spec.technique) / 100f));
            recoveryDuration = spec.recovery * masteryCut / performance;
            phaseTimer = StartupDuration;
            P.AddExertion(spec.heavy ? 0.6f : 0.25f);
            ImpactEffects.Swing(spec.heavy);
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

            float strength = StatEffects.StrengthDamageFactor(P.GetAttribute(AttributeId.Strength)) * P.StrainPenalty(BodyArea.Upper);
            float technique = 1f + 0.35f * Mathf.Clamp01(P.GetMastery(current.technique) / 100f);
            float execution = Mathf.Lerp(0.85f, 1.1f, quality);
            float nen = aura.OutgoingMultiplier;
            bool ambush = aura.ZetsuActive;
            double maxOutput = P.GetAttribute(AttributeId.MaxOutput);
            float damage = current.baseDamage * strength * technique * execution * nen * Mathf.Lerp(0.8f, 1f, P.Fatigue.Readiness)
                           * (ambush ? (P.HasBreakthrough(Breakthroughs.ShadowStrike) ? 2f : StatEffects.ZetsuAmbushDamageMultiplier) : 1f)
                           * (currentIsKo ? StatEffects.KoDamageMultiplier(maxOutput) : 1f)
                           * (currentIsRock ? StatEffects.JajankenDamageMultiplier(rockCharge, maxOutput, P.GetMastery(TechniqueId.Jajanken)) : 1f);

            var hit = new HitInfo { damage = damage, technique = current.technique, heavy = current.heavy, ren = aura.RenActive || currentIsKo || currentIsRock, ambush = ambush };
            if (ambush)
            {
                aura.EndZetsu("Ambush from Zetsu!");
                P.ApplyMastery(TechniqueId.Zetsu, 1f);
                HitFeedback.SlowMotion(0.35f, 0.35f);
                Ambushed?.Invoke();
            }
            if (hit.ren)
            {
                hit.renStagger = StatEffects.RenStagger(maxOutput) * (current.heavy ? 1.6f : 1f);
                hit.knockback = StatEffects.RenKnockback(maxOutput) * (current.heavy ? 1.5f : 1f);
            }
            if (currentIsKo)
            {
                hit.renStagger = Mathf.Max(hit.renStagger, StatEffects.KoStagger);
                hit.knockback *= 2f;
            }
            if (currentIsRock)
            {
                hit.renStagger = Mathf.Max(hit.renStagger, StatEffects.KoStagger * (0.6f + 0.5f * rockCharge));
                hit.knockback *= 1f + 1.5f * rockCharge;
            }
            var result = target.ReceiveHit(hit, out float dealt);
            if (result == HitResult.Guarded) quality *= 0.3f;

            P.ApplyMastery(current.technique, quality);
            if (aura.RenActive && result != HitResult.Guarded) P.ApplyMastery(TechniqueId.Ren, 0.7f);
            Tracker?.RegisterPlayerHit(result, dealt, current.heavy, hit.ren, punish, hit.ambush);

            Vector3 impactPoint = target.transform.position + Vector3.up * 0.4f;
            DamageNumbers.Show(target.transform.position + Vector3.up * 1.5f, dealt,
                currentIsRock || currentIsKo ? DamageNumbers.Kind.Finisher
                : result == HitResult.Guarded ? DamageNumbers.Kind.Blocked
                : result == HitResult.GuardBroken ? DamageNumbers.Kind.GuardBreak
                : hit.ren ? DamageNumbers.Kind.Ren : DamageNumbers.Kind.Hit);
            if (currentIsRock)
            {
                P.ApplyMastery(TechniqueId.Jajanken, rockCharge);
                P.ApplyMastery(TechniqueId.Ren, 0.5f);
                bool full = rockCharge >= 1f;
                HitFeedback.Impact(0.1f + 0.1f * rockCharge, 0.5f + 0.6f * rockCharge);
                if (full) HitFeedback.SlowMotion(0.3f, 0.45f);
                GameSession.Instance.PushNotice(full ? $"ROCK! {dealt:0} damage — the full chant." : $"Rock! {dealt:0} damage.", 2f);
                ImpactEffects.Ko(impactPoint);
                return;
            }
            if (currentIsKo)
            {
                P.ApplyMastery(TechniqueId.Ko, 1f);
                HitFeedback.Impact(0.16f, 0.8f);
                HitFeedback.SlowMotion(0.25f, 0.5f);
                GameSession.Instance.PushNotice($"KO! {dealt:0} damage.", 2f);
                ImpactEffects.Ko(impactPoint);
                return;
            }
            switch (result)
            {
                case HitResult.GuardBroken:
                    HitFeedback.Impact(0.1f, 0.45f);
                    ImpactEffects.GuardBreak(impactPoint);
                    break;
                case HitResult.Guarded:
                    HitFeedback.Impact(0.02f, 0.1f);
                    ImpactEffects.Blocked(impactPoint);
                    break;
                default:
                    HitFeedback.Impact((current.heavy ? 0.08f : 0.04f) + (hit.ren ? 0.03f : 0f), (current.heavy ? 0.3f : 0.15f) + (hit.ren ? 0.15f : 0f));
                    ImpactEffects.Hit(impactPoint, current.heavy, hit.ren);
                    break;
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
                DamageNumbers.Callout(transform.position + Vector3.up * 1.8f, "DODGE");
                HitFeedback.SlowMotion(0.3f, 0.45f);
                ImpactEffects.PerfectDodge(transform.position + Vector3.up * 0.3f);
                PerfectDodged?.Invoke();
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
                    DamageNumbers.Callout(transform.position + Vector3.up * 1.8f, "PARRY");
                    HitFeedback.Impact(0.12f, 0.3f);
                    HitFeedback.SlowMotion(0.4f, 0.4f);
                    ImpactEffects.Parry(transform.position + transform.forward * 0.5f + Vector3.up * 0.5f);
                    Parried?.Invoke();
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

            if (IsChargingRock)
            {
                // The chant leaves the body open: the hit lands harder and shakes the gathered power loose.
                damage *= StatEffects.JajankenChantDamageTakenMultiplier;
                if (!blocked)
                {
                    if (P.HasMastery(TechniqueId.Jajanken, MasteryMilestones.JajankenSteady) && RockCharge > 0.5f)
                    {
                        RockCharge *= 0.5f;
                        chantStage = Mathf.Min(chantStage, 1);
                        GameSession.Instance.PushNotice("Hit mid-chant — you hold on to half the power.", 1.5f);
                    }
                    else CancelChant("Hit mid-chant — the power scatters!");
                }
            }

            bool tenWasActive = aura.TenActive;
            bool renWasActive = aura.RenActive;
            bool kenWasActive = aura.KenActive;
            damage *= aura.IncomingMultiplier;
            if (IsKoStrike) damage *= StatEffects.KoDamageTakenMultiplier; // every drop of aura is in the fist
            if (tenWasActive) P.ApplyMastery(TechniqueId.Ten, 0.5f);
            if (kenWasActive) P.ApplyMastery(TechniqueId.Ken, 0.5f);
            damage *= 1f - StatEffects.DamageReduction(P.GetAttribute(AttributeId.Durability));

            // Ren protected this hit; the impact may still knock it loose for the next ones.
            if (renWasActive)
            {
                if (aura.TryBreakRen()) tracker?.RegisterRenBreak();
                else P.ApplyMastery(TechniqueId.Ren, 0.4f);
            }

            float dealt = health.TakeDamage(damage);
            DamageNumbers.Show(transform.position + Vector3.up * 1.6f, dealt, blocked ? DamageNumbers.Kind.Blocked : DamageNumbers.Kind.Taken);
            P.AddExertion(1.5f);
            tracker?.RegisterPlayerDamaged(dealt, blocked, tenWasActive || renWasActive);
            if (!blocked && dealt > 0f) HitTaken?.Invoke();
            HitFeedback.Impact(blocked ? 0.03f : 0.06f, blocked ? 0.2f : 0.5f);
            if (blocked) ImpactEffects.Blocked(transform.position + transform.forward * 0.4f + Vector3.up * 0.5f);
            else ImpactEffects.PlayerHurt(transform.position + Vector3.up * 0.5f);
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
