using UnityEngine;

namespace HunterAwakening
{
    /// <summary>Which aura the body shows this frame (drawn by <see cref="AuraVisual"/>).</summary>
    public enum AuraLook { None, Gyo, Ten, Ren, Ken, Broken }

    /// <summary>
    /// Aura reserve and the four fundamentals:
    /// Ten (toggle, cheap defence), Ren (hold, offence + heavy defence + body enhancement),
    /// Zetsu (toggle, aura shut off: hidden and recovering, but defenceless) and Gyo (hold, aura in the eyes: reads attacks).
    /// Reserve, output, control and efficiency stay separate so they create different strengths and weaknesses:
    /// when Max Output outgrows Control Precision, Ren leaks more and can be knocked loose by hits.
    /// </summary>
    public class AuraController : MonoBehaviour
    {
        [SerializeField] Light auraLight;
        [SerializeField] Color tenColor = new Color(0.4f, 0.9f, 1f);
        [SerializeField] Color renColor = new Color(1f, 0.55f, 0.15f);
        [SerializeField] Color brokenColor = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] Color gyoColor = new Color(0.75f, 0.85f, 1f);
        [SerializeField] Color kenColor = new Color(0.85f, 0.5f, 1f);
        [Tooltip("Seconds Ren cannot be re-raised after it breaks.")]
        [SerializeField] float renLockout = 1.5f;
        [Tooltip("Seconds of extra damage taken after Ren breaks (the opening).")]
        [SerializeField] float breakVulnerability = 1f;
        [SerializeField] float breakDamageMultiplier = 1.3f;
        [Tooltip("Zetsu mastery is earned only while hiding near an active opponent (no idle farming).")]
        [SerializeField] float zetsuMasteryInterval = 5f;

        InputReader input;
        PlayerMotor motor;
        float lockoutTimer;
        float vulnerableTimer;
        float zetsuHiddenTime;
        bool suppressNoticeShown;

        ProgressionService P => GameSession.Instance.Progression;

        public bool Awakened => P.IsAuraAwakened;
        public float Current => P.AuraCurrent;
        public float Capacity => StatEffects.AuraCapacity(P.GetAttribute(AttributeId.AuraCapacity));
        public bool TenActive { get; private set; }
        public bool RenActive { get; private set; }
        public bool ZetsuActive { get; private set; }
        public bool GyoActive { get; private set; }
        public bool RenBroken => lockoutTimer > 0f;

        public AuraLook Look { get; private set; }
        public Color LookColor { get; private set; }
        /// <summary>0..1: how fully the look shows (the Ren drill's output, otherwise 1).</summary>
        public float LookStrength { get; private set; }
        /// <summary>How large Ren burns: 1 at awakening, growing with Max Output and Ren mastery.</summary>
        public float RenPresence => StatEffects.RenPresence(MaxOutput, P.GetMastery(TechniqueId.Ren));

        public const string KenInsight = "technique:ken";
        public const string KoInsight = "technique:ko";
        public bool KenUnlocked => P.KnowsInsight(KenInsight);
        public bool KoUnlocked => P.KnowsInsight(KoInsight);
        public const string JajankenInsight = "hatsu:jajanken";
        public bool JajankenUnlocked => P.KnowsInsight(JajankenInsight);
        /// <summary>Set by PlayerCombat while the Jajanken chant builds (0..1): the aura swells toward Rock.</summary>
        public float HatsuCharge { get; set; }
        /// <summary>Ten and Ren held together as one stance (once learned).</summary>
        public bool KenActive => TenActive && RenActive && KenUnlocked;
        /// <summary>Ko can be thrown: learned and Ren is up.</summary>
        public bool CanKo => KoUnlocked && RenActive;
        /// <summary>Set by drills to show the Ren glow while the player channels output without holding R (0..1).</summary>
        public float DrillGlow { get; set; }
        /// <summary>Set by physical drills: Ren would inflate what they measure, so it can't be raised.</summary>
        public bool SuppressRen { get; set; }

        /// <summary>Ren-enhanced movement (run, sprint, dodge).</summary>
        public float SpeedMultiplier => RenActive && !KenActive ? StatEffects.RenSpeedMultiplier(MaxOutput) : 1f;
        /// <summary>Ren-enhanced attack wind-up and recovery (timings are divided by this). Ken is a rooted stance.</summary>
        public float AttackSpeedMultiplier => RenActive && !KenActive ? StatEffects.RenAttackSpeedMultiplier(MaxOutput) : 1f;
        /// <summary>Extra parry window (seconds) while Gyo reads the opponent.</summary>
        public float ParryWindowBonus => GyoActive ? StatEffects.GyoParryBonus(P.GetAttribute(AttributeId.Perception)) * GyoBreakthroughFactor : 0f;
        /// <summary>Extra dodge invulnerability (seconds) while Gyo reads the opponent.</summary>
        public float DodgeWindowBonus => GyoActive ? StatEffects.GyoDodgeBonus(P.GetAttribute(AttributeId.Perception)) * GyoBreakthroughFactor : 0f;
        float GyoBreakthroughFactor => P.HasBreakthrough(Breakthroughs.ReadAndAnswer) ? 1.5f : 1f;

        double MaxOutput => P.GetAttribute(AttributeId.MaxOutput);
        double Control => P.GetAttribute(AttributeId.ControlPrecision);

        /// <summary>Damage multiplier applied to the player's outgoing attacks.</summary>
        public float OutgoingMultiplier => RenActive ? StatEffects.RenDamageFactor(MaxOutput, Control) : 1f;

        /// <summary>Damage multiplier applied to hits the player receives. Ten and Ren don't stack (that is Ken, later).</summary>
        public float IncomingMultiplier
        {
            get
            {
                if (ZetsuActive) return StatEffects.ZetsuDamageTakenMultiplier;
                float m = 1f;
                if (KenActive) m = StatEffects.KenDamageTakenMultiplier(MaxOutput, Control, P.GetMastery(TechniqueId.Ten));
                else
                {
                    if (TenActive) m = StatEffects.TenDamageMultiplier(Control, P.GetMastery(TechniqueId.Ten));
                    if (RenActive) m = Mathf.Min(m, StatEffects.RenDamageTakenMultiplier(MaxOutput));
                }
                if (GyoActive) m *= StatEffects.GyoDamageTakenMultiplier;
                if (vulnerableTimer > 0f) m *= breakDamageMultiplier;
                return m;
            }
        }

        void Awake()
        {
            input = GetComponent<InputReader>();
            motor = GetComponent<PlayerMotor>();
            if (GetComponent<AuraVisual>() == null) gameObject.AddComponent<AuraVisual>();
        }

        public void FillReserve() => P.SetAuraCurrent(Capacity, Capacity);

        public bool TrySpend(float amount)
        {
            if (!Awakened || Current < amount) return false;
            P.SetAuraCurrent(Current - amount, Capacity);
            return true;
        }

        /// <summary>
        /// Called when a hit lands while Ren is held. Rolls the break chance (higher when output outpaces control);
        /// on a break Ren drops, can't be re-raised for a moment, and the player takes extra damage briefly.
        /// </summary>
        public bool TryBreakRen()
        {
            if (!RenActive) return false;
            float chance = StatEffects.RenBreakChance(MaxOutput, Control, P.GetMastery(TechniqueId.Ren))
                           * (P.HasBreakthrough(Breakthroughs.UnbrokenRen) ? 0.7f : 1f)
                           * (KenActive ? StatEffects.KenBreakChanceMultiplier : 1f);
            if (Random.value >= chance) return false;
            RenActive = false;
            lockoutTimer = renLockout;
            vulnerableTimer = breakVulnerability;
            P.AddExertion(4f);
            HitFeedback.Shake(0.45f);
            GameSession.Instance.PushNotice("Ren destabilised! Your output outpaced your control.", 2.5f);
            return true;
        }

        /// <summary>Pays for a Ko strike. Returns false (and spends nothing) if Ko can't be thrown or the reserve is short.</summary>
        public bool TrySpendKo()
        {
            if (!CanKo) return false;
            return TrySpend(StatEffects.KoAuraCost(Current, P.GetMastery(TechniqueId.Ko)));
        }

        void CheckAdvancedUnlocks()
        {
            if (!KenUnlocked && P.GetMastery(TechniqueId.Ten) >= MasteryMilestones.KenUnlockTenMastery
                             && P.GetMastery(TechniqueId.Ren) >= MasteryMilestones.KenUnlockRenMastery)
                P.UnlockInsight(KenInsight, "Ken", "Hold Ren (R) while Ten (T) is up: a rooted stance far sturdier than either alone, with steadier Ren.");
            if (!KoUnlocked && P.GetMastery(TechniqueId.Ren) >= MasteryMilestones.KoUnlockRenMastery
                            && MaxOutput >= MasteryMilestones.KoUnlockMaxOutput)
                P.UnlockInsight(KoInsight, "Ko", "With Ren up, press F to pour all your aura into one strike — devastating, expensive, and defenceless while thrown.");
            if (!JajankenUnlocked && KoUnlocked && P.GetMastery(TechniqueId.Ren) >= MasteryMilestones.JajankenUnlockRenMastery)
                P.UnlockInsight(JajankenInsight, "Jajanken — Rock",
                    "Your first Hatsu. Hold V to chant \"Saisho wa guu… Jan… Ken…\" and release for Rock: the longer the chant, the harder the punch, and finishing it adds a final burst. The chant roots you and leaves you open — a hit scatters the power.");
        }

        /// <summary>Leaves Zetsu (e.g. after striking from hiding).</summary>
        public void EndZetsu(string notice = null)
        {
            if (!ZetsuActive) return;
            ZetsuActive = false;
            if (notice != null) GameSession.Instance.PushNotice(notice, 2f);
        }

        void Update()
        {
            var session = GameSession.Instance;
            float dt = Time.deltaTime;
            lockoutTimer -= dt;
            vulnerableTimer -= dt;

            if (!Awakened)
            {
                TenActive = RenActive = ZetsuActive = GyoActive = false;
                UpdateVisual();
                return;
            }

            bool blocked = session.InputBlocked || motor.Locked;
            CheckAdvancedUnlocks();

            // Physical drills measure the body: Zetsu's fast fatigue recovery would inflate them, like Ren would.
            if (SuppressRen && ZetsuActive) EndZetsu("Zetsu released — physical drills train the body, not the aura.");
            if (!blocked && input.ZetsuPressed && SuppressRen)
                session.PushNotice("Zetsu is unavailable during physical drills.", 2f);
            else if (!blocked && input.ZetsuPressed)
            {
                ZetsuActive = !ZetsuActive;
                if (ZetsuActive) TenActive = false;
                session.PushNotice(ZetsuActive
                    ? "Zetsu: aura sealed. Hidden and recovering fast — but every hit lands harder."
                    : "Zetsu released.", 2.5f);
            }
            if (!blocked && input.TenPressed)
            {
                TenActive = !TenActive;
                if (TenActive) EndZetsu();
                session.PushNotice(TenActive ? "Ten: aura shroud active (less damage taken)." : "Ten released.", 2f);
            }
            if (SuppressRen && input.RenHeld && !suppressNoticeShown)
            {
                session.PushNotice("Ren is suppressed during physical drills — train the body, not the aura.", 2.5f);
                suppressNoticeShown = true;
            }
            if (!input.RenHeld) suppressNoticeShown = false;
            RenActive = !blocked && input.RenHeld && Current > 1f && !RenBroken && !SuppressRen && !ZetsuActive;
            GyoActive = !blocked && input.GyoHeld && Current > 1f && !ZetsuActive;

            float powerPerSecond = 0f;
            if (TenActive) powerPerSecond += StatEffects.TenDrain(P.GetMastery(TechniqueId.Ten));
            if (GyoActive) powerPerSecond += StatEffects.GyoDrain(P.GetMastery(TechniqueId.Gyo));
            if (RenActive)
            {
                double sustained = P.GetAttribute(AttributeId.SustainedOutput);
                float output = StatEffects.RenOutput(MaxOutput);
                float leakage = StatEffects.RenLeakage(MaxOutput, Control, P.GetMastery(TechniqueId.Ren));
                float renPower = output * (1f + leakage);
                if (KenActive) renPower *= StatEffects.KenDrainMultiplier(P.GetMastery(TechniqueId.Ken));
                else if (motor.IsMoving) renPower *= StatEffects.RenMovingDrainMultiplier(sustained);
                powerPerSecond += renPower;
                P.AddExertion(StatEffects.RenFatiguePerSecond(sustained) * dt);
            }

            float capacity = Capacity;
            if (powerPerSecond > 0f)
            {
                float cost = ProgressionMath.AuraCost(powerPerSecond, dt, StatEffects.Efficiency(P.GetAttribute(AttributeId.Efficiency)));
                P.SetAuraCurrent(Current - cost, capacity);
                if (Current <= 0f)
                {
                    TenActive = RenActive = GyoActive = false;
                    P.AddExertion(5f);
                    session.PushNotice("Aura depleted! Ten, Ren and Gyo dropped.", 3f);
                }
            }
            if (!RenActive && DrillGlow <= 0f && HatsuCharge <= 0f)
            {
                float recovery = StatEffects.AuraRecovery(P.GetAttribute(AttributeId.RecoveryRate))
                                 * (TenActive ? 0.3f : 1f) * (ZetsuActive ? StatEffects.ZetsuAuraRecoveryMultiplier : 1f);
                P.SetAuraCurrent(Current + recovery * dt, capacity);
            }
            if (ZetsuActive)
            {
                P.Fatigue.Recover(dt * 4f, false); // sealing the aura lets the body recover faster
                TickZetsuMastery(dt);
            }

            UpdateVisual();
        }

        void TickZetsuMastery(float dt)
        {
            bool hidingFromSomeone = false;
            float detection = StatEffects.ZetsuDetectionRange(P.GetMastery(TechniqueId.Zetsu));
            foreach (var e in EnemyController.All)
            {
                if (!e.IsActive) continue;
                float d = Vector3.Distance(e.transform.position, transform.position);
                if (d > detection && d < detection + 8f) hidingFromSomeone = true;
            }
            if (!hidingFromSomeone) return;
            zetsuHiddenTime += dt;
            if (zetsuHiddenTime < zetsuMasteryInterval) return;
            zetsuHiddenTime = 0f;
            P.ApplyMastery(TechniqueId.Zetsu, 0.3f);
        }

        void UpdateVisual()
        {
            bool drillRen = DrillGlow > 0.05f;
            bool chant = HatsuCharge > 0.01f;
            Look = ZetsuActive || !Awakened ? AuraLook.None
                 : lockoutTimer > renLockout - 0.6f ? AuraLook.Broken // a short sputter, then whatever is still held
                 : KenActive ? AuraLook.Ken
                 : RenActive || drillRen || chant ? AuraLook.Ren
                 : TenActive ? AuraLook.Ten
                 : GyoActive ? AuraLook.Gyo
                 : AuraLook.None;
            LookStrength = Look == AuraLook.Ren && !RenActive
                ? Mathf.Max(DrillGlow, chant ? 0.35f + 0.65f * HatsuCharge : 0f)
                : 1f;
            LookColor = Look switch
            {
                AuraLook.Broken => brokenColor,
                AuraLook.Ken => kenColor,
                AuraLook.Ren => renColor,
                AuraLook.Ten => tenColor,
                _ => gyoColor
            };

            if (auraLight == null) return;
            auraLight.enabled = Look != AuraLook.None;
            if (!auraLight.enabled) return;
            float presence = Look == AuraLook.Ren || Look == AuraLook.Ken ? RenPresence : 1f;
            float flicker = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 9f, 0.37f);
            auraLight.color = LookColor;
            auraLight.range = 4f * Mathf.Sqrt(presence);
            auraLight.intensity = flicker * Look switch
            {
                AuraLook.Broken => 1.5f,
                AuraLook.Ken => 3.5f * Mathf.Sqrt(presence),
                AuraLook.Ren => (RenActive ? 3.5f : 1.5f + 2.5f * LookStrength) * Mathf.Sqrt(presence),
                AuraLook.Ten => 2.5f,
                _ => 1.2f
            };
        }
    }
}
