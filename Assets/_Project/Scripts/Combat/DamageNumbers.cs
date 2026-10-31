using System.Collections.Generic;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Floating combat text: damage numbers that pop above whoever was hit, rise and fade. Colour and size say what
    /// kind of hit it was (plain, Ren, guard break, Rock/Ko, blocked, taken), so the effect of each technique reads at
    /// a glance. Purely cosmetic.
    /// </summary>
    public class DamageNumbers : MonoBehaviour
    {
        public enum Kind { Hit, Ren, GuardBreak, Finisher, Blocked, Taken, Callout }

        struct Number
        {
            public Vector3 world;
            public string text;
            public Kind kind;
            public float born;
            public float drift;
        }

        const float Lifetime = 0.9f;
        static DamageNumbers instance;
        readonly List<Number> numbers = new List<Number>();
        GUIStyle style;

        void Awake() => instance = this;

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Show(Vector3 world, float amount, Kind kind)
        {
            if (amount < 0.5f) return;
            Add(world, Mathf.RoundToInt(amount).ToString(), kind);
        }

        /// <summary>A word instead of a number ("PARRY", "DODGE").</summary>
        public static void Callout(Vector3 world, string text) => Add(world, text, Kind.Callout);

        static void Add(Vector3 world, string text, Kind kind)
        {
            if (instance == null) return;
            instance.numbers.Add(new Number { world = world, text = text, kind = kind, born = Time.unscaledTime, drift = Random.Range(-18f, 18f) });
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || numbers.Count == 0) return;
            var session = GameSession.Instance;
            if (session == null || session.Paused || session.DashboardOpen) return;
            var cam = Camera.main;
            if (cam == null) return;
            style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

            float now = Time.unscaledTime;
            numbers.RemoveAll(n => now - n.born > Lifetime);
            foreach (var n in numbers)
            {
                Vector3 screen = cam.WorldToScreenPoint(n.world);
                if (screen.z <= 0f) continue;
                float t = (now - n.born) / Lifetime;
                // Pop in large, settle, then rise and fade.
                float pop = t < 0.12f ? Mathf.Lerp(1.6f, 1f, t / 0.12f) : 1f;
                float rise = Mathf.Lerp(0f, 60f, 1f - (1f - t) * (1f - t));
                float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                var (color, size) = Look(n.kind);
                style.fontSize = Mathf.RoundToInt(size * pop * Screen.height / 1080f);
                var center = new Vector2(screen.x + n.drift * t, Screen.height - screen.y - rise);
                var rect = new Rect(center.x - 120f, center.y - 30f, 240f, 60f);

                // A dark outline keeps the numbers readable over sand, grass and aura alike.
                style.normal.textColor = new Color(0f, 0f, 0f, 0.8f * alpha);
                for (int dx = -2; dx <= 2; dx += 2)
                    for (int dy = -2; dy <= 2; dy += 2)
                        if (dx != 0 || dy != 0) GUI.Label(new Rect(rect.x + dx, rect.y + dy, rect.width, rect.height), n.text, style);
                color.a = alpha;
                style.normal.textColor = color;
                GUI.Label(rect, n.text, style);
            }
        }

        static (Color color, float size) Look(Kind kind) => kind switch
        {
            Kind.Ren => (new Color(1f, 0.62f, 0.2f), 34f),
            Kind.GuardBreak => (new Color(1f, 0.45f, 0.15f), 36f),
            Kind.Finisher => (new Color(1f, 0.85f, 0.25f), 54f),
            Kind.Blocked => (new Color(0.7f, 0.72f, 0.76f), 24f),
            Kind.Taken => (new Color(1f, 0.3f, 0.3f), 30f),
            Kind.Callout => (new Color(0.55f, 0.9f, 1f), 32f),
            _ => (Color.white, 28f),
        };
    }
}
