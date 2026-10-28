using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Aura reserve plus the two fundamentals in the prototype: Ten (toggle, cheap defence) and Ren (hold, offence and
    /// heavy defence). Reserve, output, control and efficiency stay separate so they create different strengths and
    /// weaknesses: when Max Output outgrows Control Precision, Ren leaks more and can be knocked loose by hits.
    /// </summary>
    public class AuraController : MonoBehaviour
    {
        [SerializeField] Light auraLight;
        [SerializeField] Color tenColor = new Color(0.4f, 0.9f, 1f);
        [SerializeField] Color renColor = new Color(1f, 0.55f, 0.15f);
        [SerializeField] Color brokenColor = new Color(0.9f, 0.2f, 0.2f);
        [Tooltip("Seconds Ren cannot be re-raised after it breaks.")]
        [SerializeField] float renLockout = 1.5f;
        [Tooltip("Seconds of extra damage taken after Ren breaks (the opening).")]
        [SerializeField] float breakVulnerability = 1f;
        [SerializeField] float breakDamageMultiplier = 1.3f;

        InputReader input;
        PlayerMotor motor;
        float lockoutTimer;
        float vulnerableTimer;

        ProgressionService P => GameSession.Instance.Progression;

        public bool Awakened => P.IsAuraAwakened;
        public float Current => P.AuraCurrent;
        public float Capacity => StatEffects.AuraCapacity(P.GetAttribute(AttributeId.AuraCapacity));
        public bool TenActive { get; private set; }
        public bool RenActive { get; private set; }
        public bool RenBroken => lockoutTimer > 0f;
        /// <summary>Set by drills to show the Ren glow while the player channels output without holding R (0..1).</summary>
        public float DrillGlow { get; set; }

        double MaxOutput => P.GetAttribute(AttributeId.MaxOutput);
        double Control => P.GetAttribute(AttributeId.ControlPrecision);

        /// <summary>Damage multiplier applied to the player's outgoing attacks.</summary>
        public float OutgoingMultiplier => RenActive ? StatEffects.RenDamageFactor(MaxOutput, Control) : 1f;

        /// <summary>Damage multiplier applied to hits the player receives. Ten and Ren don't stack (that is Ken, later).</summary>
        public float IncomingMultiplier
        {
            get
            {
                float m = 1f;
                if (TenActive) m = StatEffects.TenDamageMultiplier(Control, P.GetMastery(TechniqueId.Ten));
                if (RenActive) m = Mathf.Min(m, StatEffects.RenDamageTakenMultiplier(MaxOutput));
                if (vulnerableTimer > 0f) m *= breakDamageMultiplier;
                return m;
            }
        }

        void Awake()
        {
            input = GetComponent<InputReader>();
            motor = GetComponent<PlayerMotor>();
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
            if (Random.value >= StatEffects.RenBreakChance(MaxOutput, Control, P.GetMastery(TechniqueId.Ren))) return false;
            RenActive = false;
            lockoutTimer = renLockout;
            vulnerableTimer = breakVulnerability;
            P.AddExertion(4f);
            GameSession.Instance.PushNotice("Ren destabilised! Your output outpaced your control.", 2.5f);
            return true;
        }

        void Update()
        {
            var session = GameSession.Instance;
            float dt = Time.deltaTime;
            lockoutTimer -= dt;
            vulnerableTimer -= dt;

            if (!Awakened)
            {
                TenActive = RenActive = false;
                UpdateVisual();
                return;
            }

            bool blocked = session.DashboardOpen || motor.Locked;

            if (!blocked && input.TenPressed)
            {
                TenActive = !TenActive;
                session.PushNotice(TenActive ? "Ten: aura shroud active (less damage taken)." : "Ten released.", 2f);
            }
            RenActive = !blocked && input.RenHeld && Current > 1f && !RenBroken;

            float powerPerSecond = 0f;
            if (TenActive) powerPerSecond += StatEffects.TenDrain(P.GetMastery(TechniqueId.Ten));
            if (RenActive)
            {
                float output = StatEffects.RenOutput(MaxOutput);
                float leakage = StatEffects.RenLeakage(MaxOutput, Control, P.GetMastery(TechniqueId.Ren));
                powerPerSecond += output * (1f + leakage);
                P.AddExertion(StatEffects.RenFatiguePerSecond(P.GetAttribute(AttributeId.SustainedOutput)) * dt);
            }

            float capacity = Capacity;
            if (powerPerSecond > 0f)
            {
                float cost = ProgressionMath.AuraCost(powerPerSecond, dt, StatEffects.Efficiency(P.GetAttribute(AttributeId.Efficiency)));
                P.SetAuraCurrent(Current - cost, capacity);
                if (Current <= 0f)
                {
                    TenActive = RenActive = false;
                    P.AddExertion(5f);
                    session.PushNotice("Aura depleted! Ten and Ren dropped.", 3f);
                }
            }
            if (!RenActive && DrillGlow <= 0f)
            {
                float recovery = StatEffects.AuraRecovery(P.GetAttribute(AttributeId.RecoveryRate)) * (TenActive ? 0.3f : 1f);
                P.SetAuraCurrent(Current + recovery * dt, capacity);
            }

            UpdateVisual();
        }

        void UpdateVisual()
        {
            if (auraLight == null) return;
            bool ren = RenActive || DrillGlow > 0.05f;
            auraLight.enabled = TenActive || ren || RenBroken;
            auraLight.color = RenBroken ? brokenColor : ren ? renColor : tenColor;
            auraLight.intensity = RenBroken ? 1.5f : RenActive ? 6f : DrillGlow > 0.05f ? 2f + 5f * DrillGlow : 2.5f;
        }
    }
}
