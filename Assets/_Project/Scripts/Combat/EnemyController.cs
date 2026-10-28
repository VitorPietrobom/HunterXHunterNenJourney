using System;
using System.Collections.Generic;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Sparring rival with fixed stats (never scaled to the player). One readable telegraphed attack, a guard that
    /// adapts to repeated player patterns it can *see*, and a punishable recovery window.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(Health))]
    public class EnemyController : MonoBehaviour
    {
        public static readonly List<EnemyController> All = new List<EnemyController>();

        enum State { Inactive, Chase, Telegraph, Recovery, Stagger, Guard, Defeated }

        [Header("Fixed stats — never scaled to the player")]
        [SerializeField] string displayName = "Sparring Rival";
        [SerializeField] float maxHealth = 240f;
        [SerializeField] float moveSpeed = 3.4f;
        [SerializeField] float attackDamage = 24f;
        [SerializeField] float attackRange = 2.1f;
        [SerializeField] float telegraphTime = 0.8f;
        [SerializeField] float recoveryTime = 1.0f;
        [SerializeField] float attackCooldown = 1.1f;
        [SerializeField] float telegraphTurnSpeed = 90f;
        [SerializeField] float damageReduction = 0.1f;
        [SerializeField] float radius = 0.5f;

        [Header("Guard & adaptation")]
        [SerializeField] float baseGuardChance = 0.2f;
        [SerializeField] float adaptationGuardBonus = 0.55f;
        [SerializeField] int adaptationThreshold = 3;
        [SerializeField] int patternMemory = 6;
        [SerializeField] float lightGuardMultiplier = 0.15f;
        [SerializeField] float guardBreakMultiplier = 0.6f;
        [SerializeField] float guardBreakStagger = 0.9f;

        [Header("Visuals")]
        [SerializeField] Renderer bodyRenderer;
        [SerializeField] GameObject guardVisual;
        [SerializeField] Color baseColor = new Color(0.75f, 0.3f, 0.3f);
        [SerializeField] Color telegraphColor = new Color(1f, 0.1f, 0.05f);
        [SerializeField] Color vulnerableColor = new Color(0.95f, 0.85f, 0.3f);

        readonly Queue<TechniqueId> recentPlayerAttacks = new Queue<TechniqueId>();
        CharacterController controller;
        Health health;
        PlayerCombat player;
        Material material;
        State state = State.Inactive;
        float stateTimer;
        float cooldown;
        float verticalVelocity;
        float flashTimer;
        Vector3 knockback;
        Vector3 spawnPosition;
        Quaternion spawnRotation;

        public string DisplayName => displayName;
        public Health Health => health;
        public float Radius => radius;
        public bool IsActive => state != State.Inactive && state != State.Defeated;
        public bool IsTelegraphing => state == State.Telegraph;
        public bool IsVulnerable => state == State.Recovery || state == State.Stagger;
        public bool IsGuarding => state == State.Guard;
        public float TelegraphProgress => state == State.Telegraph ? 1f - stateTimer / telegraphTime : 0f;

        public event Action Defeated;
        public event Action AttackLaunched;
        public event Action PatternRead;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            health.SetMax(maxHealth, false);
            health.RestoreFull();
            if (bodyRenderer != null) material = bodyRenderer.material;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            SetColor(baseColor);
            if (guardVisual != null) guardVisual.SetActive(false);
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public void Activate(PlayerCombat target)
        {
            Deactivate();
            player = target;
            player.AttackStarted += OnPlayerAttackStarted;
            controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            controller.enabled = true;
            health.RestoreFull();
            recentPlayerAttacks.Clear();
            knockback = Vector3.zero;
            cooldown = 1.2f;
            Enter(State.Chase, 0f);
        }

        public void Deactivate()
        {
            if (player != null) player.AttackStarted -= OnPlayerAttackStarted;
            player = null;
            if (state != State.Defeated) Enter(State.Inactive, 0f);
        }

        public void ResetToIdle()
        {
            Deactivate();
            controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            controller.enabled = true;
            health.RestoreFull();
            knockback = Vector3.zero;
            Enter(State.Inactive, 0f);
        }

        public void Stagger(float duration)
        {
            if (!IsActive) return;
            Enter(State.Stagger, duration);
        }

        void OnPlayerAttackStarted(PlayerCombat.AttackSpec spec)
        {
            recentPlayerAttacks.Enqueue(spec.technique);
            while (recentPlayerAttacks.Count > patternMemory) recentPlayerAttacks.Dequeue();
            if (state != State.Chase) return; // committed to attacking or open to punishment

            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            if (to.magnitude > 4f) return;

            int repeats = 0;
            foreach (var t in recentPlayerAttacks)
                if (t == spec.technique) repeats++;
            bool adapted = repeats >= adaptationThreshold;
            float chance = baseGuardChance + (adapted ? adaptationGuardBonus : 0f);
            if (UnityEngine.Random.value >= chance) return;

            FaceTowards(player.transform.position, 1000f);
            Enter(State.Guard, spec.startup + spec.active + 0.2f);
            if (adapted) PatternRead?.Invoke();
        }

        public HitResult ReceiveHit(HitInfo hit, out float dealt)
        {
            dealt = 0f;
            if (!IsActive) return HitResult.None;
            float damage = hit.damage * (1f - damageReduction);
            var result = HitResult.Landed;

            if (state == State.Guard)
            {
                if (hit.heavy || hit.ren)
                {
                    damage *= guardBreakMultiplier;
                    bool longStagger = GameSession.Instance.Progression.HasMastery(TechniqueId.HeavyAttack, MasteryMilestones.HeavyLongStagger);
                    Enter(State.Stagger, guardBreakStagger * (longStagger ? 1.5f : 1f));
                    result = HitResult.GuardBroken;
                }
                else
                {
                    damage *= lightGuardMultiplier;
                    result = HitResult.Guarded;
                }
            }
            else if (hit.heavy && state == State.Chase)
            {
                Enter(State.Stagger, 0.35f); // heavies interrupt movement; the telegraph has armour (only a Ren heavy breaks it)
            }

            if (hit.ren && result != HitResult.Guarded)
            {
                Vector3 away = transform.position - player.transform.position;
                away.y = 0f;
                knockback = away.normalized * hit.knockback;
                bool armoured = state == State.Telegraph && !hit.heavy;
                if (!armoured && state != State.Stagger) Enter(State.Stagger, hit.renStagger);
            }

            dealt = health.TakeDamage(damage);
            flashTimer = 0.08f;
            if (health.IsDead)
            {
                Enter(State.Defeated, 0f);
                transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 90f);
                Defeated?.Invoke();
            }
            return result;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (controller.enabled && state != State.Defeated)
            {
                verticalVelocity = controller.isGrounded ? -2f : verticalVelocity - 22f * dt;
                controller.Move((knockback + Vector3.up * verticalVelocity) * dt);
            }
            knockback = Vector3.MoveTowards(knockback, Vector3.zero, 14f * dt);

            stateTimer -= dt;
            switch (state)
            {
                case State.Chase: TickChase(dt); break;
                case State.Telegraph:
                    FaceTowards(player.transform.position, telegraphTurnSpeed * dt);
                    SetColor(Color.Lerp(baseColor, telegraphColor, TelegraphProgress));
                    if (stateTimer <= 0f) ResolveAttack();
                    break;
                case State.Recovery:
                case State.Stagger:
                case State.Guard:
                    if (stateTimer <= 0f) Enter(State.Chase, 0f);
                    break;
            }

            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                if (flashTimer <= 0f) RefreshColor();
                else SetColor(Color.white);
            }
        }

        void TickChase(float dt)
        {
            if (player == null) return;
            cooldown -= dt;
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            FaceTowards(player.transform.position, 540f * dt);

            if (distance > attackRange * 0.85f)
                controller.Move(to.normalized * (moveSpeed * dt));
            if (distance <= attackRange && cooldown <= 0f && !player.IsDefeated)
                Enter(State.Telegraph, telegraphTime);
        }

        void ResolveAttack()
        {
            AttackLaunched?.Invoke();
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            if (to.magnitude <= attackRange + 0.4f && Vector3.Angle(transform.forward, to) < 70f)
                player.ReceiveHit(attackDamage, this);
            cooldown = attackCooldown;
            if (state == State.Telegraph) Enter(State.Recovery, recoveryTime); // a parry may already have staggered us
        }

        void Enter(State next, float duration)
        {
            state = next;
            stateTimer = duration;
            if (guardVisual != null) guardVisual.SetActive(next == State.Guard);
            RefreshColor();
        }

        void RefreshColor()
        {
            SetColor(state switch
            {
                State.Telegraph => telegraphColor,
                State.Recovery or State.Stagger => vulnerableColor,
                State.Defeated or State.Inactive => baseColor * 0.7f,
                _ => baseColor
            });
        }

        void SetColor(Color c)
        {
            if (material != null) material.color = c;
        }

        void FaceTowards(Vector3 target, float maxDegrees)
        {
            Vector3 to = target - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to, Vector3.up), maxDegrees);
        }
    }
}
