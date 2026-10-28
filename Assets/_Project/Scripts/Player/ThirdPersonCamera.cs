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

        float yaw;
        float pitch = 15f;

        void Start()
        {
            if (target != null) yaw = target.eulerAngles.y;
        }

        void LateUpdate()
        {
            if (target == null) return;
            bool uiOpen = GameSession.Instance != null && GameSession.Instance.DashboardOpen;
            Cursor.lockState = uiOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = uiOpen;

            if (!uiOpen && input != null)
            {
                Vector2 look = input.Look;
                yaw += look.x * sensitivity;
                pitch = Mathf.Clamp(pitch - look.y * sensitivity, minPitch, maxPitch);
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            Vector3 back = rotation * Vector3.back;
            float d = distance;
            // Casts ignore the player's own CharacterController because they start inside it.
            if (Physics.SphereCast(pivot, collisionRadius, back, out var hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
                d = Mathf.Max(minDistance, hit.distance);

            transform.SetPositionAndRotation(pivot + back * d, rotation);
        }
    }
}
