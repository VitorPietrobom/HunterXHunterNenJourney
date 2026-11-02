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

        /// <summary>A Humanoid bone of the animated model, or null when the player has no model.</summary>
        public Transform Bone(HumanBodyBones bone)
        {
            var animator = Animation != null ? Animation.Animator : null;
            return animator != null && animator.isHuman ? animator.GetBoneTransform(bone) : null;
        }

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

        const float RegenDelay = 4f;
        const float RegenPerSecond = 6f;
        float sinceDamaged = RegenDelay;

        void Start()
        {
            GameSession.Instance.Progression.StateChanged += RefreshDerivedStats;
            Health.Damaged += OnDamaged;
            RefreshDerivedStats();
            Health.RestoreFull();
        }

        void OnDamaged(float amount) => sinceDamaged = 0f;

        void Update()
        {
            // Out of a match, health trickles back after a short breather (the bench is still faster and clears fatigue).
            sinceDamaged += Time.deltaTime;
            if (Combat.Tracker == null && sinceDamaged >= RegenDelay && !Health.IsDead)
                Health.Heal(RegenPerSecond * Time.deltaTime
                            * (GameSession.Instance.Progression.KnowsInsight(WaterDivination.EnhancerInsight)
                               && GameSession.Instance.Progression.Hunter.nenType == NenType.Enhancer ? StatEffects.EnhancerRegenMultiplier : 1f));
        }

        void OnDestroy()
        {
            if (Health != null) Health.Damaged -= OnDamaged;
            if (GameSession.Instance != null) GameSession.Instance.Progression.StateChanged -= RefreshDerivedStats;
        }

        void RefreshDerivedStats()
        {
            var p = GameSession.Instance.Progression;
            float maxHealth = StatEffects.MaxHealth(p.GetAttribute(AttributeId.Durability), p.GetAttribute(AttributeId.Endurance));
            if (p.HasBreakthrough(Breakthroughs.IronBody)) maxHealth *= 1.1f;
            maxHealth *= StatEffects.HunterLevelHealthFactor(p.HunterLevel);
            Health.SetMax(maxHealth, true);
        }
    }
}
