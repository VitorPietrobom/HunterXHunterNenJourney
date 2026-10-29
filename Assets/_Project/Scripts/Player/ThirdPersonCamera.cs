using UnityEngine;

namespace HunterAwakening
{
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] InputReader input;
        [SerializeField] float distance = 5.5f;
        [SerializeField] float minDistance = 1f;
        [SerializeField] float pivotHeight = 0.7f;
        [SerializeField] float sensitivity = 0.12f;
        [SerializeField] float minPitch = -30f;
        [SerializeField] float maxPitch = 70f;
        [SerializeField] float collisionRadius = 0.25f;
        [SerializeField] LayerMask collisionMask = ~0;

        [SerializeField] float shakeMagnitude = 0.35f;
        [SerializeField] float shakeDecay = 2.2f;

        static float trauma;
        float yaw;
        float pitch = 15f;

        /// <summary>Adds camera trauma (0..1). Shake strength is trauma², so small hits stay subtle.</summary>
        public static void AddShake(float amount) => trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, amount));

        /// <summary>Mouse sensitivity multiplier from the settings menu.</summary>
        public static float SensitivityScale { get; set; } = 1f;
        public static bool InvertY { get; set; }

        void Start()
        {
            if (target != null) yaw = target.eulerAngles.y;
        }

        void LateUpdate()
        {
            if (target == null) return;
            bool uiOpen = GameSession.Instance != null && GameSession.Instance.InputBlocked;
            Cursor.lockState = uiOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = uiOpen;

            if (!uiOpen && input != null)
            {
                Vector2 look = input.Look;
                float s = sensitivity * SensitivityScale;
                yaw += look.x * s;
                pitch = Mathf.Clamp(pitch - look.y * s * (InvertY ? -1f : 1f), minPitch, maxPitch);
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            Vector3 back = rotation * Vector3.back;
            float d = distance;
            // Casts ignore the player's own CharacterController because they start inside it.
            if (Physics.SphereCast(pivot, collisionRadius, back, out var hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
                d = Mathf.Max(minDistance, hit.distance);

            Vector3 shake = Vector3.zero;
            if (trauma > 0f)
            {
                float t = Time.unscaledTime * 25f;
                float strength = trauma * trauma * shakeMagnitude;
                shake = new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f) * (2f * strength);
                trauma = Mathf.Max(0f, trauma - shakeDecay * Time.unscaledDeltaTime);
            }

            transform.SetPositionAndRotation(pivot + back * d + rotation * shake, rotation);
        }
    }
}
