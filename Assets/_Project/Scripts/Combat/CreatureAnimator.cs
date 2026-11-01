using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Procedural motion for unrigged creatures (the chimera ants are single sculpted meshes): a stalking bob and sway
    /// while moving, a slow breath at rest, rearing back through the wind-up so it reads as the cue to dodge, a lunge on
    /// the strike, a shudder when staggered, and a topple-and-sink when beaten. Moves only the model, not the host.
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    public class CreatureAnimator : MonoBehaviour
    {
        [SerializeField] Transform body;
        [Tooltip("Steps per metre travelled (bob frequency).")]
        [SerializeField] float stride = 0.9f;
        [SerializeField] float rearAngle = 22f;
        [SerializeField] float lungeAngle = 28f;

        EnemyController enemy;
        Vector3 basePosition;
        Quaternion baseRotation;
        Vector3 baseScale;
        Vector3 lastPosition;
        float speed, phase, lunge, shudder, defeatedFor, breath;
        Renderer[] renderers;

        public void Configure(Transform model) => body = model;

        void Awake()
        {
            enemy = GetComponent<EnemyController>();
            if (body == null)
            {
                enabled = false;
                return;
            }
            basePosition = body.localPosition;
            baseRotation = body.localRotation;
            baseScale = body.localScale;
            renderers = body.GetComponentsInChildren<Renderer>(true);
            breath = Random.value * 10f;
        }

        void OnEnable()
        {
            lastPosition = transform.position;
            enemy.AttackLaunched += OnStrike;
            enemy.Staggered += OnStaggered;
        }

        void OnDisable()
        {
            enemy.AttackLaunched -= OnStrike;
            enemy.Staggered -= OnStaggered;
        }

        void OnStrike() => lunge = 1f;
        void OnStaggered() => shudder = Mathf.Max(shudder, Mathf.Clamp(enemy.StaggerDuration, 0.3f, 1f));

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;
            float instant = dt > 0f ? delta.magnitude / dt : 0f;
            speed = Mathf.Lerp(speed, instant, 1f - Mathf.Exp(-10f * dt));

            if (enemy.IsDefeated)
            {
                Defeat(dt);
                return;
            }
            if (defeatedFor > 0f) Revive();

            phase += speed * stride * dt * Mathf.PI * 2f;
            breath += dt;
            float moving = Mathf.Clamp01(speed / 2f);

            // Stalking: a heavy bob, a side-to-side rock and a forward lean that grow with speed.
            float bob = Mathf.Abs(Mathf.Sin(phase)) * 0.12f * moving;
            float sway = Mathf.Sin(phase) * 6f * moving;
            float lean = 8f * moving;

            // Wind-up: rear back and rise; the strike throws the body forward, then it settles.
            float rear = enemy.IsTelegraphing ? Mathf.SmoothStep(0f, 1f, enemy.TelegraphProgress) : 0f;
            lunge = Mathf.MoveTowards(lunge, 0f, dt * 2.5f);
            float strike = lunge * lunge;
            float pitch = lean - rear * rearAngle + strike * lungeAngle;
            float rise = rear * 0.18f;

            // Guard: hunker down. Staggered: shudder.
            float crouch = enemy.IsGuarding ? 0.08f : 0f;
            shudder = Mathf.MoveTowards(shudder, 0f, dt);
            float shake = shudder > 0f ? Mathf.Sin(Time.time * 55f) * 7f * shudder : 0f;
            float breathe = 1f + Mathf.Sin(breath * 2.1f) * 0.012f * (1f - moving);

            body.localPosition = basePosition + new Vector3(0f, bob + rise - crouch, strike * 0.5f);
            body.localRotation = baseRotation * Quaternion.Euler(pitch, shake, sway + shake * 0.5f);
            body.localScale = new Vector3(baseScale.x, baseScale.y * breathe, baseScale.z);
        }

        void Defeat(float dt)
        {
            defeatedFor += dt;
            // Topple sideways, lie still, then sink into the ground and vanish.
            float fall = Mathf.SmoothStep(0f, 1f, defeatedFor / 0.5f);
            float sink = Mathf.Clamp01((defeatedFor - 2.5f) / 2f);
            body.localRotation = baseRotation * Quaternion.Euler(0f, 0f, 85f * fall);
            body.localPosition = basePosition + Vector3.down * (sink * 2f);
            if (sink >= 1f)
                foreach (var r in renderers) r.enabled = false;
        }

        void Revive()
        {
            defeatedFor = 0f;
            lunge = shudder = 0f;
            foreach (var r in renderers) r.enabled = true;
            body.localPosition = basePosition;
            body.localRotation = baseRotation;
        }
    }
}
