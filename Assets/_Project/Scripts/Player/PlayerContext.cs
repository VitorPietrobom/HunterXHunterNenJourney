using UnityEngine;

namespace HunterAwakening
{
    /// <summary>Bundles the player's components so drills, arena and UI share one reference.</summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerContext : MonoBehaviour
    {
        public InputReader Input { get; private set; }
        public PlayerMotor Motor { get; private set; }
        public PlayerVitals Vitals { get; private set; }
        public Health Health { get; private set; }
        public PlayerCombat Combat { get; private set; }
        public AuraController Aura { get; private set; }
        public PlayerInteractor Interactor { get; private set; }
        /// <summary>Optional: absent when the player has no animated model.</summary>
        public PlayerAnimator Animation { get; private set; }

        public void SetPose(PlayerPose pose)
        {
            if (Animation != null) Animation.Pose = pose;
        }

        public void PlayRep(float duration)
        {
            if (Animation != null) Animation.PlayRep(duration);
        }

        void Awake()
        {
            if (GetComponent<BreakthroughTracker>() == null) gameObject.AddComponent<BreakthroughTracker>();
            Animation = GetComponent<PlayerAnimator>();
            Input = GetComponent<InputReader>();
            Motor = GetComponent<PlayerMotor>();
            Vitals = GetComponent<PlayerVitals>();
            Health = GetComponent<Health>();
            Combat = GetComponent<PlayerCombat>();
            Aura = GetComponent<AuraController>();
            Interactor = GetComponent<PlayerInteractor>();
        }

        void Start()
        {
            GameSession.Instance.Progression.StateChanged += RefreshDerivedStats;
            RefreshDerivedStats();
            Health.RestoreFull();
        }

        void OnDestroy()
        {
            if (GameSession.Instance != null) GameSession.Instance.Progression.StateChanged -= RefreshDerivedStats;
        }

        void RefreshDerivedStats()
        {
            var p = GameSession.Instance.Progression;
            float maxHealth = StatEffects.MaxHealth(p.GetAttribute(AttributeId.Durability), p.GetAttribute(AttributeId.Endurance));
            if (p.HasBreakthrough(Breakthroughs.IronBody)) maxHealth *= 1.1f;
            Health.SetMax(maxHealth, true);
        }
    }
}
