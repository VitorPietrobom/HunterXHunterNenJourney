using UnityEngine;

namespace HunterAwakening
{
    /// <summary>Small IMGUI helpers shared by the HUD, dashboard and drill overlays (placeholder UI for the prototype).</summary>
    public static class HudGui
    {
        static GUIStyle label, center, header, small, wrap, title;

        public static GUIStyle Label => Ensure(ref label, () => new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true });
        public static GUIStyle Center => Ensure(ref center, () => new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, richText = true });
        public static GUIStyle Header => Ensure(ref header, () => new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, richText = true });
        public static GUIStyle Title => Ensure(ref title, () => new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true });
        public static GUIStyle Small => Ensure(ref small, () => new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, richText = true, normal = { textColor = new Color(0.75f, 0.78f, 0.82f) } });
        public static GUIStyle Wrap => Ensure(ref wrap, () => new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, richText = true });

        static GUIStyle Ensure(ref GUIStyle style, System.Func<GUIStyle> create) => style ??= create();

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static void Panel(Rect r) => Fill(r, new Color(0.05f, 0.06f, 0.08f, 0.82f));

        public static void Bar(Rect r, float fill, Color color, string text)
        {
            Fill(r, new Color(0f, 0f, 0f, 0.65f));
            Fill(new Rect(r.x + 2, r.y + 2, (r.width - 4) * ProgressionMath.Clean01(fill), r.height - 4), color);
            if (!string.IsNullOrEmpty(text)) GUI.Label(r, text, Center);
        }

        /// <summary>Standard top-centre panel used by every running drill. Returns the rect left for a custom meter.</summary>
        public static Rect DrillPanel(string heading, string body, float meterHeight = 0f)
        {
            float width = Mathf.Min(520f, Screen.width - 40f);
            float height = 110f + meterHeight;
            var r = new Rect((Screen.width - width) * 0.5f, 16f, width, height);
            Panel(r);
            GUI.Label(new Rect(r.x + 12, r.y + 6, r.width - 24, 26), heading, Header);
            GUI.Label(new Rect(r.x + 12, r.y + 32, r.width - 24, 70), body, Wrap);
            return new Rect(r.x + 12, r.y + 104, r.width - 24, meterHeight);
        }

        public static string Pct(float v) => $"{v * 100f:0}%";

        public static Color Good => new Color(0.35f, 0.85f, 0.45f);
        public static Color Warn => new Color(0.95f, 0.75f, 0.25f);
        public static Color Bad => new Color(0.95f, 0.35f, 0.3f);
    }
}
