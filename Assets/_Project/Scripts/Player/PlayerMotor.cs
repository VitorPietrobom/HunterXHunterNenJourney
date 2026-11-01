using System;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>Third-person CharacterController movement. Speeds come from attributes via StatEffects.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] Transform cameraTransform;
        [SerializeField] float gravity = -22f;
        [SerializeField] float turnSpeed = 14f;
        [SerializeField] float jumpStaminaCost = 6f;

        [Header("Dodge")]
        [SerializeField] float dodgeDuration = 0.32f;
        [SerializeField] float dodgeSpeedMultiplier = 2.4f;
        [SerializeField] float dodgeStaminaCost = 18f;
        [SerializeField] float dodgeCooldown = 0.15f;
        [SerializeField] float iFrameStart = 0.03f;
        [SerializeField] float iFrameEnd = 0.2f;

        CharacterController controller;
        InputReader input;
        PlayerVitals vitals;
        PlayerCombat combat;
        AuraController aura;
        float verticalVelocity;
        float dodgeTimer;
        float dodgeCooldownTimer;
        Vector3 dodgeDirection;

        public bool Locked { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsMoving { get; private set; }
        public bool IsGrounded => controller.isGrounded;
        public bool IsDodging => dodgeTimer > 0f;
        public float MoveMultiplier { get; set; } = 1f;

        public bool IsInvulnerable
        {
            get
            {
                if (!IsDodging) return false;
                float elapsed = dodgeDuration - dodgeTimer;
                return elapsed >= iFrameStart && elapsed <= IFrameEnd;
            }
        }

        public Vector3 FeetPosition => transform.position + controller.center - Vector3.up * (controller.height * 0.5f);
        public float DodgeDuration => dodgeDuration;

        /// <summary>Time from take-off to landing on flat ground for the current jump height.</summary>
        public float ExpectedAirTime => 2f * Mathf.Sqrt(2f * StatEffects.JumpHeight(GameSession.Instance.Progression.GetAttribute(AttributeId.Mobility)) / -gravity);

        public event Action Dodged;
        public event Action Jumped;

        float IFrameEnd
        {
            get
            {
                var p = GameSession.Instance.Progression;
                float bonus = StatEffects.DodgeWindowBonus(p.GetAttribute(AttributeId.Coordination));
                if (p.HasMastery(TechniqueId.Dodge, MasteryMilestones.DodgeLongIFrames)) bonus += 0.06f;
                if (aura != null) bonus += aura.DodgeWindowBonus;
                return iFrameEnd + bonus;
            }
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<InputReader>();
            vitals = GetComponent<PlayerVitals>();
            combat = GetComponent<PlayerCombat>();
            aura = GetComponent<AuraController>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        public void SetLocked(bool locked)
        {
            Locked = locked;
            if (locked) dodgeTimer = 0f;
        }

        void Update()
        {
            var session = GameSession.Instance;
            var p = session.Progression;
            float dt = Time.deltaTime;
            bool blocked = Locked || session.InputBlocked || (combat != null && combat.IsDefeated);

            Vector2 raw = blocked ? Vector2.zero : input.Move;
            Vector3 direction = CameraRelative(raw);
            IsMoving = direction.sqrMagnitude > 0.01f;

            double speedStat = p.GetAttribute(AttributeId.Speed);
            double endurance = p.GetAttribute(AttributeId.Endurance);
            float performance = p.Fatigue.PerformanceMultiplier * (aura != null ? aura.SpeedMultiplier : 1f) * p.StrainPenalty(BodyArea.Lower);

            bool wantsSprint = input.SprintHeld && IsMoving && !blocked && !vitals.IsExhausted
                               && (combat == null || (!combat.IsBlocking && !combat.IsAttacking));
            IsSprinting = wantsSprint && vitals.Stamina > 0f;
            if (IsSprinting)
            {
                float sprintCost = StatEffects.SprintStaminaCost(endurance) * (p.HasBreakthrough(Breakthroughs.TenInMotion) ? 0.8f : 1f);
                vitals.Drain(sprintCost * dt);
                p.AddExertion(0.25f * dt);
            }

            float speed = (IsSprinting ? StatEffects.SprintSpeed(speedStat) : StatEffects.RunSpeed(speedStat)) * performance * MoveMultiplier;
            Vector3 horizontal = direction * speed;

            dodgeCooldownTimer -= dt;
            if (!blocked && input.DodgePressed && !IsDodging && dodgeCooldownTimer <= 0f && controller.isGrounded
                && (combat == null || combat.CanDodge))
            {
                float cost = dodgeStaminaCost * (p.HasMastery(TechniqueId.Footwork, MasteryMilestones.FootworkCheapDodge) ? 0.7f : 1f);
                if (vitals.TrySpend(cost))
                {
                    dodgeDirection = IsMoving ? direction.normalized : -transform.forward;
                    dodgeTimer = dodgeDuration;
                    ImpactEffects.Dodge();
                    Dodged?.Invoke();
                }
            }

            if (IsDodging)
            {
                dodgeTimer -= dt;
                if (dodgeTimer <= 0f) dodgeCooldownTimer = dodgeCooldown;
                horizontal = dodgeDirection * StatEffects.RunSpeed(speedStat) * dodgeSpeedMultiplier * performance;
            }

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (!blocked && input.JumpPressed && controller.isGrounded && !IsDodging && (combat == null || !combat.IsChargingRock)
                && vitals.TrySpend(jumpStaminaCost))
            {
                verticalVelocity = Mathf.Sqrt(2f * StatEffects.JumpHeight(p.GetAttribute(AttributeId.Mobility)) * -gravity);
                Jumped?.Invoke();
            }
            verticalVelocity += gravity * dt;

            controller.Move((horizontal + Vector3.up * verticalVelocity) * dt);

            var lockTarget = combat != null ? combat.LockTarget : null;
            if (lockTarget != null && !IsDodging && !combat.IsAttacking)
            {
                // Locked on: keep facing the opponent and strafe around them.
                Vector3 to = lockTarget.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to, Vector3.up), 1f - Mathf.Exp(-turnSpeed * dt));
            }
            else if (IsMoving && !IsDodging && (combat == null || !combat.IsAttacking))
            {
                var target = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSpeed * dt));
            }
        }

        Vector3 CameraRelative(Vector2 raw)
        {
            if (raw.sqrMagnitude < 0.0001f) return Vector3.zero;
            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            Vector3 dir = forward.normalized * raw.y + right.normalized * raw.x;
            return Vector3.ClampMagnitude(dir, 1f);
        }

        public void FaceTowards(Vector3 worldPosition)
        {
            Vector3 to = worldPosition - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(to, Vector3.up);
        }

        public void Teleport(Vector3 position, float yaw)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            controller.enabled = true;
            verticalVelocity = 0f;
        }
    }
}
