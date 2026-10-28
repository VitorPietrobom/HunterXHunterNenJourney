using System;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>Stamina pool driven by Endurance and fatigue.</summary>
    public class PlayerVitals : MonoBehaviour
    {
        [SerializeField] float regenDelay = 0.7f;

        float sinceSpend;
        bool exhausted;

        public float Stamina { get; private set; } = 100f;
        public float MaxStamina => StatEffects.MaxStamina(Endurance);
        public float Fraction => MaxStamina > 0f ? Stamina / MaxStamina : 0f;
        public bool IsExhausted => exhausted;

        /// <summary>Raised when stamina hits zero. The arena counts these for combat feedback.</summary>
        public event Action Exhausted;

        static double Endurance => GameSession.Instance.Progression.GetAttribute(AttributeId.Endurance);

        void Start() => Stamina = MaxStamina;

        void Update()
        {
            var p = GameSession.Instance.Progression;
            float max = MaxStamina;
            sinceSpend += Time.deltaTime;
            if (sinceSpend >= regenDelay)
                Stamina = Mathf.Min(max, Stamina + StatEffects.StaminaRegen(Endurance) * p.Fatigue.PerformanceMultiplier * Time.deltaTime);
            if (exhausted && Stamina >= max * 0.3f) exhausted = false;
            Stamina = Mathf.Min(Stamina, max);
        }

        /// <summary>Discrete costs (attacks, jumps, dodges). Fails without spending if not enough stamina.</summary>
        public bool TrySpend(float amount)
        {
            if (Stamina < amount) return false;
            Spend(amount);
            return true;
        }

        /// <summary>Continuous costs (sprint, block). Can run the pool to zero.</summary>
        public void Drain(float amount) => Spend(amount);

        void Spend(float amount)
        {
            if (!(amount > 0f)) return;
            Stamina = Mathf.Max(0f, Stamina - amount);
            sinceSpend = 0f;
            if (Stamina <= 0f && !exhausted)
            {
                exhausted = true;
                Exhausted?.Invoke();
            }
        }

        public void RestoreFull()
        {
            Stamina = MaxStamina;
            exhausted = false;
        }
    }
}
