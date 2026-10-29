using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Game-feel layer: hit-stop (brief freeze on impact), slow motion (parries, perfect dodges, knockouts) and
    /// camera shake. Owns Time.timeScale except while the game is paused. Purely cosmetic.
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        const float FreezeScale = 0.03f;

        static HitFeedback instance;
        float freezeUntil;
        float slowUntil;
        float slowScale = 1f;

        void Awake() => instance = this;

        void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            Time.timeScale = 1f;
        }

        /// <summary>Freeze the action for a few frames so the hit lands with weight.</summary>
        public static void Hitstop(float seconds)
        {
            if (instance == null || !(seconds > 0f) || !GameSettings.HitStop) return;
            instance.freezeUntil = Mathf.Max(instance.freezeUntil, Time.unscaledTime + seconds);
        }

        public static void SlowMotion(float scale, float seconds)
        {
            if (instance == null || !(seconds > 0f) || !GameSettings.HitStop) return;
            instance.slowScale = Mathf.Clamp(scale, 0.05f, 1f);
            instance.slowUntil = Mathf.Max(instance.slowUntil, Time.unscaledTime + seconds);
        }

        public static void Shake(float amount)
        {
            if (GameSettings.CameraShake) ThirdPersonCamera.AddShake(amount);
        }

        public static void Impact(float hitstop, float shake)
        {
            Hitstop(hitstop);
            Shake(shake);
        }

        void Update()
        {
            var session = GameSession.Instance;
            if (session != null && session.Paused) return; // the pause menu owns timeScale while open
            float now = Time.unscaledTime;
            float scale = now < slowUntil ? slowScale : 1f;
            if (now < freezeUntil) scale = FreezeScale;
            Time.timeScale = scale;
        }
    }
}
