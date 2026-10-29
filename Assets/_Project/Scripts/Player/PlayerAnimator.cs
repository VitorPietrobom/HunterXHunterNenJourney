using System.Collections.Generic;
using UnityEngine;

namespace HunterAwakening
{
    public enum PlayerPose { None, Rest, Lift, Meditate }

    /// <summary>
    /// Drives the Humanoid Animator from gameplay state. Gameplay owns timing: one-shot clips are time-scaled so their
    /// key frame (fist impact, take-off) lines up with the gameplay event instead of the other way round.
    /// </summary>
    public class PlayerAnimator : MonoBehaviour
    {
        enum State { Idle, Run, Sprint, Jump, Dodge, PunchLight, PunchHeavy, Block, HitReact, KnockedDown, SitIdle, Squat }

        [SerializeField] Animator animator;
        [SerializeField] float crossFade = 0.15f;
        [SerializeField] float actionCrossFade = 0.06f;

        [Header("Locomotion: clip root speed at 1× (humanoid units, set by the scene builder)")]
        [SerializeField] float runClipSpeed = 5.58f;
        [SerializeField] float sprintClipSpeed = 5.05f;
        [SerializeField] float idleThreshold = 0.3f;

        [Header("Key moments (normalized clip time, measured from the Mixamo clips)")]
        [SerializeField] float lightImpact = 0.55f;
        [SerializeField] float lightEnd = 0.85f;
        [SerializeField] float heavyImpact = 0.32f;
        [SerializeField] float heavyEnd = 0.8f;
        [SerializeField] float jumpTakeoff = 0.33f;
        [SerializeField] float jumpLanding = 0.6f;
        [SerializeField] float blockHold = 0.35f;
        [SerializeField] float dodgeEnd = 0.85f;
        [Tooltip("The roll keeps playing this many times longer than the gameplay dodge, unless another action interrupts it.")]
        [SerializeField] float dodgeDurationScale = 2.2f;
        [SerializeField] float squatEnd = 0.85f;

        static readonly int[] Hashes = BuildHashes();

        readonly Dictionary<State, float> lengths = new Dictionary<State, float>();
        PlayerContext player;
        State state;
        bool hasState;
        float hitTimer, dodgeTimer, repTimer, repDuration = 1f, airTime;
        bool airborne;
        Vector3 lastPosition;
        float groundSpeed;

        public PlayerPose Pose { get; set; }
        /// <summary>The model's Humanoid Animator (for attaching props such as the barbell to bones).</summary>
        public Animator Animator => animator;

        static int[] BuildHashes()
        {
            var names = System.Enum.GetNames(typeof(State));
            var hashes = new int[names.Length];
            for (int i = 0; i < names.Length; i++) hashes[i] = Animator.StringToHash(names[i]);
            return hashes;
        }

        void Awake()
        {
            player = GetComponent<PlayerContext>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                enabled = false;
                return;
            }
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (System.Enum.TryParse(clip.name, out State s)) lengths[s] = clip.length;
            animator.applyRootMotion = false;
        }

        void Start()
        {
            lastPosition = transform.position;
            player.Combat.HitTaken += OnHitTaken;
            player.Motor.Jumped += OnJumped;
            player.Motor.Dodged += OnDodged;
        }

        void OnDestroy()
        {
            if (player == null || player.Combat == null) return;
            player.Combat.HitTaken -= OnHitTaken;
            player.Motor.Jumped -= OnJumped;
            player.Motor.Dodged -= OnDodged;
        }

        /// <summary>Plays one squat rep over the given duration (lift drill).</summary>
        public void PlayRep(float duration)
        {
            repDuration = Mathf.Max(0.1f, duration);
            repTimer = repDuration;
            Enter(State.Squat, 0f, true);
        }

        void OnHitTaken() => hitTimer = Length(State.HitReact) * 0.8f;

        void OnJumped()
        {
            airborne = true;
            airTime = 0f;
            Enter(State.Jump, jumpTakeoff, true);
        }

        void OnDodged()
        {
            dodgeTimer = player.Motor.DodgeDuration * dodgeDurationScale;
            Enter(State.Dodge, 0f, true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;
            groundSpeed = Mathf.Lerp(groundSpeed, delta.magnitude / dt, 1f - Mathf.Exp(-12f * dt));

            hitTimer -= dt;
            dodgeTimer -= dt;
            repTimer -= dt;
            if (airborne)
            {
                airTime += dt;
                if (player.Motor.IsGrounded && airTime > 0.1f) airborne = false;
            }

            State next = Decide(out float playback);
            Enter(next, next == State.Jump ? jumpTakeoff : 0f, false);
            animator.speed = playback;
        }

        State Decide(out float playback)
        {
            var combat = player.Combat;
            var motor = player.Motor;
            playback = 1f;

            if (combat.IsDefeated) return State.KnockedDown;

            switch (Pose)
            {
                case PlayerPose.Rest:
                case PlayerPose.Meditate:
                    return State.SitIdle;
                case PlayerPose.Lift:
                    // Between reps the clip is held on its standing frame.
                    playback = repTimer > 0f ? Length(State.Squat) * squatEnd / repDuration : 0f;
                    return State.Squat;
            }

            if (combat.IsAttacking)
            {
                bool heavy = combat.IsHeavyAttack;
                var attack = heavy ? State.PunchHeavy : State.PunchLight;
                float impact = heavy ? heavyImpact : lightImpact;
                float end = heavy ? heavyEnd : lightEnd;
                float length = Length(attack);
                playback = combat.CurrentPhase == PlayerCombat.Phase.Startup
                    ? impact * length / Mathf.Max(0.05f, combat.StartupDuration)
                    : (end - impact) * length / Mathf.Max(0.05f, combat.FollowThroughDuration);
                return attack;
            }

            if (hitTimer > 0f) return State.HitReact;

            if (motor.IsDodging || dodgeTimer > 0f)
            {
                playback = Length(State.Dodge) * dodgeEnd / Mathf.Max(0.05f, motor.DodgeDuration * dodgeDurationScale);
                return State.Dodge;
            }

            if (airborne)
            {
                playback = (jumpLanding - jumpTakeoff) * Length(State.Jump) / Mathf.Max(0.2f, motor.ExpectedAirTime);
                return State.Jump;
            }

            if (combat.IsBlocking)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                bool raised = info.shortNameHash == Hashes[(int)State.Block] && info.normalizedTime >= blockHold;
                playback = raised ? 0f : 1f;
                return State.Block;
            }

            if (groundSpeed < idleThreshold) return State.Idle;

            float scale = animator.humanScale * animator.transform.lossyScale.y;
            if (motor.IsSprinting)
            {
                playback = Mathf.Clamp(groundSpeed / (sprintClipSpeed * scale), 0.6f, 2.4f);
                return State.Sprint;
            }
            playback = Mathf.Clamp(groundSpeed / (runClipSpeed * scale), 0.4f, 2f);
            return State.Run;
        }

        void Enter(State next, float normalizedStart, bool restart)
        {
            if (hasState && next == state && !restart) return;
            int hash = Hashes[(int)next];
            if (hasState && next == state) animator.Play(hash, 0, normalizedStart);
            else animator.CrossFadeInFixedTime(hash, IsAction(next) ? actionCrossFade : crossFade, 0, normalizedStart * Length(next));
            state = next;
            hasState = true;
        }

        static bool IsAction(State s) => s == State.PunchLight || s == State.PunchHeavy || s == State.Dodge
                                         || s == State.HitReact || s == State.Jump || s == State.Block;

        float Length(State s) => lengths.TryGetValue(s, out float l) && l > 0f ? l : 1f;
    }
}
