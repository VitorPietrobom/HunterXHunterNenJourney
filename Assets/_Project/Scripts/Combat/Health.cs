using System;
using UnityEngine;

namespace HunterAwakening
{
    public class Health : MonoBehaviour
    {
        [SerializeField] float maxHealth = 100f;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;
        public float Fraction => maxHealth > 0f ? Current / maxHealth : 0f;

        public event Action<float> Damaged;
        public event Action Died;

        void Awake() => Current = maxHealth;

        public void SetMax(float max, bool keepRatio)
        {
            if (!(max > 0f)) return;
            float ratio = Fraction;
            maxHealth = max;
            Current = keepRatio ? ratio * max : Mathf.Min(Current, max);
        }

        public float TakeDamage(float amount)
        {
            if (IsDead || !(amount > 0f)) return 0f;
            float dealt = Mathf.Min(amount, Current);
            Current -= dealt;
            Damaged?.Invoke(dealt);
            if (Current <= 0f)
            {
                Current = 0f;
                Died?.Invoke();
            }
            return dealt;
        }

        public void Heal(float amount)
        {
            if (!(amount > 0f) || IsDead) return;
            Current = Mathf.Min(maxHealth, Current + amount);
        }

        public void RestoreFraction(float fraction) => Current = Mathf.Clamp01(fraction) * maxHealth;
        public void RestoreFull() => Current = maxHealth;
    }
}
