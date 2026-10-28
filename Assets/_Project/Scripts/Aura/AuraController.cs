using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Aura reserve plus the two fundamentals in the prototype: Ten (toggle, defensive) and Ren (hold, offensive).
    /// Reserve, output, control and efficiency stay separate so they create different strengths and weaknesses.
    /// </summary>
    public class AuraController : MonoBehaviour
    {
        [SerializeField] Light auraLight;
        [SerializeField] Color tenColor = new Color(0.4f, 0.9f, 1f);
        [SerializeField] Color renColor = new Color(1f, 0.55f, 0.15f);

        InputReader input;
        PlayerMotor motor;

        ProgressionService P => GameSession.Instance.Progression;

        public bool Awakened => P.IsAuraAwakened;
        public float Current => P.AuraCurrent;
        public float Capacity => StatEffects.AuraCapacity(P.GetAttribute(AttributeId.AuraCapacity));
        public bool TenActive { get; private set; }
        public bool RenActive { get; private set; }

        /// <summary>Damage multiplier applied to the player's outgoing attacks.</summary>
        public float OutgoingMultiplier => RenActive
            ? StatEffects.RenDamageFactor(P.GetAttribute(AttributeId.MaxOutput), P.GetAttribute(AttributeId.ControlPrecision))
            : 1f;

        /// <summary>Damage multiplier applied to hits the player receives.</summary>
        public float IncomingMultiplier => TenActive
            ? StatEffects.TenDamageMultiplier(P.GetAttribute(AttributeId.ControlPrecision), P.GetMastery(TechniqueId.Ten))
            : 1f;

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

        void Update()
        {
            var session = GameSession.Instance;
            if (!Awakened)
            {
                TenActive = RenActive = false;
                UpdateVisual();
                return;
            }

            float dt = Time.deltaTime;
            bool blocked = session.DashboardOpen || motor.Locked;

            if (!blocked && input.TenPressed)
            {
                TenActive = !TenActive;
                session.PushNotice(TenActive ? "Ten: aura shroud active (less damage taken)." : "Ten released.", 2f);
            }
            RenActive = !blocked && input.RenHeld && Current > 1f;

            float powerPerSecond = 0f;
            if (TenActive) powerPerSecond += StatEffects.TenDrain(P.GetMastery(TechniqueId.Ten));
            if (RenActive)
            {
                float output = StatEffects.RenOutput(P.GetAttribute(AttributeId.MaxOutput));
                float leakage = StatEffects.RenLeakage(P.GetAttribute(AttributeId.ControlPrecision), P.GetMastery(TechniqueId.Ren));
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
            if (!RenActive)
            {
                float recovery = StatEffects.AuraRecovery(P.GetAttribute(AttributeId.RecoveryRate)) * (TenActive ? 0.3f : 1f);
                P.SetAuraCurrent(Current + recovery * dt, capacity);
            }

            UpdateVisual();
        }

        void UpdateVisual()
        {
            if (auraLight == null) return;
            auraLight.enabled = TenActive || RenActive;
            auraLight.color = RenActive ? renColor : tenColor;
            auraLight.intensity = RenActive ? 6f : 2.5f;
        }
    }
}
