using System.Collections.Generic;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Drives the rival's Humanoid Animator from EnemyController state (same controller and clips as the player).
    /// The heavy punch is time-scaled so the fist lands exactly when the telegraph ends and the attack resolves,
    /// which keeps the wind-up readable as the cue to dodge or parry.
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    public class EnemyAnimator : MonoBehaviour
    {
        enum State { Idle, Run, PunchHeavy, Block, HitReact, KnockedDown }

        [SerializeField] Animator animator;
        [SerializeField] float crossFade = 0.15f;
        [SerializeField] float actionCrossFade = 0.06f;
        [Tooltip("Clip root speed at 1× (humanoid units, set by the scene builder).")]
        [SerializeField] float runClipSpeed = 5.58f;
        [SerializeField] float idleThreshold = 0.3f;
        [Header("Key moments (normalized clip time, measured from the Mixamo clips)")]
        [SerializeField] float heavyImpact = 0.32f;
        [SerializeField] float heavyEnd = 0.8f;
        [SerializeField] float blockHold = 0.35f;

        static readonly Dictionary<State, int> Hashes = new Dictionary<State, int>();
        readonly Dictionary<State, float> lengths = new Dictionary<State, float>();
        EnemyController enemy;
        State state;
        bool hasState;
        Vector3 lastPosition;
        float groundSpeed;

        static EnemyAnimator()
        {
            foreach (State s in System.Enum.GetValues(typeof(State))) Hashes[s] = Animator.StringToHash(s.ToString());
        }

        void Awake()
        {
            enemy = GetComponent<EnemyController>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                enabled = false;
                return;
            }
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (System.Enum.TryParse(clip.name, out State s)) lengths[s] = clip.length;
            animator.applyRootMotion = false;
        }

        void OnEnable()
        {
            lastPosition = transform.position;
            enemy.Staggered += OnStaggered;
        }

        void OnDisable() => enemy.Staggered -= OnStaggered;

        /// <summary>Drives a different model (opponent tiers have their own fighters); all share the same controller.</summary>
        public void UseModel(Animator model)
        {
            if (model == null || model.runtimeAnimatorController == null || model == animator) return;
            animator = model;
            animator.applyRootMotion = false;
            hasState = false; // the new model starts its state fresh
            enabled = true;
        }

        // Back-to-back staggers restart the reaction instead of holding its last frame.
        void OnStaggered() => Enter(State.HitReact, true);

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 delta = transform.position - lastPosition;
            lastPosition = transform.position;
            delta.y = 0f;
            groundSpeed = Mathf.Lerp(groundSpeed, delta.magnitude / dt, 1f - Mathf.Exp(-12f * dt));

            State next = Decide(out float playback);
            Enter(next, false);
            animator.speed = playback;
        }

        State Decide(out float playback)
        {
            playback = 1f;
            if (enemy.IsDefeated) return State.KnockedDown;

            float heavyLength = Length(State.PunchHeavy);
            if (enemy.IsTelegraphing)
            {
                playback = heavyImpact * heavyLength / Mathf.Max(0.05f, enemy.TelegraphTime);
                return State.PunchHeavy;
            }
            if (enemy.IsRecovering && state == State.PunchHeavy)
            {
                playback = (heavyEnd - heavyImpact) * heavyLength / Mathf.Max(0.05f, enemy.RecoveryTime);
                return State.PunchHeavy;
            }
            if (enemy.IsStaggered)
            {
                playback = Mathf.Clamp(Length(State.HitReact) * 0.8f / Mathf.Max(0.05f, enemy.StaggerDuration), 0.5f, 2.5f);
                return State.HitReact;
            }
            if (enemy.IsGuarding)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                bool raised = info.shortNameHash == Hashes[State.Block] && info.normalizedTime >= blockHold;
                playback = raised ? 0f : 2f; // guards come up fast: the rival reacts to a visible wind-up
                return State.Block;
            }
            if (groundSpeed < idleThreshold) return State.Idle;

            float scale = animator.humanScale * animator.transform.lossyScale.y;
            playback = Mathf.Clamp(groundSpeed / (runClipSpeed * scale), 0.4f, 2f);
            return State.Run;
        }

        void Enter(State next, bool restart)
        {
            if (hasState && next == state && !restart) return;
            int hash = Hashes[next];
            if (hasState && next == state) animator.Play(hash, 0, 0f);
            else animator.CrossFadeInFixedTime(hash, next == State.Idle || next == State.Run ? crossFade : actionCrossFade, 0, 0f);
            state = next;
            hasState = true;
        }

        float Length(State s) => lengths.TryGetValue(s, out float l) && l > 0f ? l : 1f;
    }
}
