using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// The game's IMGUI theme and drawing helpers, shared by the HUD, menus, dashboard and drill overlays: rounded ink
    /// panels with a gold edge, a warm Hunter-gold accent, aura cyan, rounded bars and themed controls. Every texture is
    /// generated at runtime, so the look needs no assets.
    /// </summary>
    public static class HudGui
    {
        // ------------------------------------------------------------------ Palette

        public static readonly Color Ink = new Color(0.05f, 0.06f, 0.09f, 0.9f);
        public static readonly Color InkLight = new Color(0.11f, 0.13f, 0.18f, 0.95f);
        public static readonly Color Edge = new Color(1f, 0.78f, 0.35f, 0.35f);
        public static readonly Color Gold = new Color(1f, 0.77f, 0.32f);
        public static readonly Color AuraCyan = new Color(0.4f, 0.82f, 1f);
        public static readonly Color Text = new Color(0.92f, 0.94f, 0.97f);
        public static readonly Color Muted = new Color(0.62f, 0.67f, 0.74f);

        public static Color Good => new Color(0.38f, 0.86f, 0.5f);
        public static Color Warn => new Color(0.96f, 0.76f, 0.28f);
        public static Color Bad => new Color(0.95f, 0.36f, 0.32f);

        // ------------------------------------------------------------------ Text styles

        static GUIStyle label, center, header, small, wrap, title, section, eyebrow;

        public static GUIStyle Label => Ensure(ref label, () => Style(14));
        public static GUIStyle Center => Ensure(ref center, () => Style(13, TextAnchor.MiddleCenter));
        public static GUIStyle Header => Ensure(ref header, () => Style(16, bold: true, color: Gold));
        public static GUIStyle Title => Ensure(ref title, () => Style(26, TextAnchor.MiddleCenter, bold: true));
        public static GUIStyle Small => Ensure(ref small, () => Style(12, wrap: true, color: Muted));
        public static GUIStyle Wrap => Ensure(ref wrap, () => Style(13, wrap: true));
        /// <summary>Small gold capitals above a section.</summary>
        public static GUIStyle Section => Ensure(ref section, () => Style(11, bold: true, color: Gold));
        /// <summary>Tiny muted capitals above a title ("HUNTER: AWAKENING").</summary>
        public static GUIStyle Eyebrow => Ensure(ref eyebrow, () => Style(11, TextAnchor.MiddleCenter, bold: true, color: Gold));

        static GUIStyle Style(int size, TextAnchor anchor = TextAnchor.UpperLeft, bool bold = false, bool wrap = false, Color? color = null)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, richText = true, wordWrap = wrap };
            if (bold) s.fontStyle = FontStyle.Bold;
            s.normal.textColor = color ?? Text;
            return s;
        }

        static GUIStyle Ensure(ref GUIStyle style, System.Func<GUIStyle> create) => style ??= create();

        // ------------------------------------------------------------------ Skin (themed controls)

        static GUISkin skin;
        static Texture2D panelTex, cardTex, chipTex, barFillTex, buttonTex, buttonHoverTex, buttonDownTex, primaryTex,
            primaryHoverTex, dangerTex, dangerHoverTex, toggleOffTex, toggleOnTex, toggleOffHoverTex, toggleOnHoverTex,
            sliderTrackTex, thumbTex, thumbHoverTex, scrollTrackTex, scrollThumbTex, stripeTex;
        static GUIStyle panelStyle, cardStyle, cardBox, primaryButton, dangerButton, chipStyle;

        /// <summary>Call at the top of an OnGUI that draws standard controls (buttons, toggles, sliders, scroll views).</summary>
        public static void Use()
        {
            if (skin == null) BuildSkin();
            GUI.skin = skin;
        }

        public static GUIStyle PrimaryButton { get { Use(); return primaryButton; } }
        public static GUIStyle DangerButton { get { Use(); return dangerButton; } }
        /// <summary>A padded inset card for GUILayout.BeginVertical (dashboard columns).</summary>
        public static GUIStyle CardBox { get { Use(); return cardBox; } }

        static void BuildSkin()
        {
            panelTex = Rounded(32, 9, Ink, Edge, 1);
            cardTex = Rounded(32, 8, InkLight, new Color(1f, 1f, 1f, 0.06f), 1);
            barFillTex = Gradient(16);
            chipTex = Rounded(16, 6, Color.white, Color.clear, 0, highlight: true);
            buttonTex = Rounded(32, 7, new Color(0.14f, 0.17f, 0.23f, 0.95f), new Color(1f, 1f, 1f, 0.12f), 1);
            buttonHoverTex = Rounded(32, 7, new Color(0.19f, 0.22f, 0.3f, 0.98f), Gold, 1);
            buttonDownTex = Rounded(32, 7, new Color(0.3f, 0.25f, 0.14f, 1f), Gold, 1);
            primaryTex = Rounded(32, 7, new Color(0.93f, 0.68f, 0.24f), new Color(1f, 0.9f, 0.6f, 0.8f), 1, highlight: true);
            primaryHoverTex = Rounded(32, 7, new Color(1f, 0.8f, 0.38f), Color.white, 1, highlight: true);
            dangerTex = Rounded(32, 7, new Color(0.38f, 0.1f, 0.1f, 0.95f), new Color(1f, 0.45f, 0.4f, 0.6f), 1);
            dangerHoverTex = Rounded(32, 7, new Color(0.55f, 0.14f, 0.13f, 1f), new Color(1f, 0.55f, 0.5f), 1);
            toggleOffTex = ToggleBox(Rounded(20, 5, new Color(0.08f, 0.09f, 0.12f), new Color(1f, 1f, 1f, 0.3f), 1));
            toggleOffHoverTex = ToggleBox(Rounded(20, 5, new Color(0.12f, 0.13f, 0.17f), Gold, 1));
            toggleOnTex = ToggleBox(Check(new Color(0.93f, 0.68f, 0.24f), new Color(0.1f, 0.08f, 0.05f)));
            toggleOnHoverTex = ToggleBox(Check(new Color(1f, 0.8f, 0.38f), new Color(0.1f, 0.08f, 0.05f)));
            sliderTrackTex = Rounded(12, 3, new Color(0f, 0f, 0f, 0.55f), new Color(1f, 1f, 1f, 0.15f), 1);
            thumbTex = Circle(18, Gold, new Color(0.25f, 0.18f, 0.05f));
            thumbHoverTex = Circle(18, new Color(1f, 0.88f, 0.55f), new Color(0.25f, 0.18f, 0.05f));
            scrollTrackTex = Rounded(10, 4, new Color(0f, 0f, 0f, 0.3f), Color.clear, 0);
            scrollThumbTex = Rounded(10, 4, new Color(1f, 0.77f, 0.32f, 0.55f), Color.clear, 0);
            stripeTex = Texture2D.whiteTexture;

            skin = Object.Instantiate(GUI.skin);
            skin.hideFlags = HideFlags.HideAndDontSave;

            var button = skin.button;
            button.normal.background = buttonTex;
            button.hover.background = buttonHoverTex;
            button.active.background = buttonDownTex;
            button.focused.background = buttonTex;
            button.onNormal.background = buttonDownTex;
            button.normal.textColor = button.focused.textColor = Text;
            button.hover.textColor = Gold;
            button.active.textColor = Gold;
            button.border = new RectOffset(8, 8, 8, 8);
            button.padding = new RectOffset(12, 12, 6, 6);
            button.fontSize = 14;
            button.fontStyle = FontStyle.Bold;

            primaryButton = new GUIStyle(button);
            primaryButton.normal.background = primaryButton.focused.background = primaryTex;
            primaryButton.hover.background = primaryHoverTex;
            primaryButton.active.background = primaryTex;
            primaryButton.normal.textColor = primaryButton.hover.textColor = primaryButton.active.textColor = primaryButton.focused.textColor = new Color(0.12f, 0.08f, 0.03f);

            dangerButton = new GUIStyle(button);
            dangerButton.normal.background = dangerButton.focused.background = dangerTex;
            dangerButton.hover.background = dangerHoverTex;
            dangerButton.active.background = dangerHoverTex;
            dangerButton.normal.textColor = new Color(1f, 0.82f, 0.8f);
            dangerButton.hover.textColor = dangerButton.active.textColor = Color.white;

            var toggle = skin.toggle;
            toggle.normal.background = toggleOffTex;
            toggle.hover.background = toggleOffHoverTex;
            toggle.active.background = toggleOffHoverTex;
            toggle.onNormal.background = toggleOnTex;
            toggle.onHover.background = toggleOnHoverTex;
            toggle.onActive.background = toggleOnHoverTex;
            toggle.focused.background = toggleOffTex;
            toggle.onFocused.background = toggleOnTex;
            toggle.normal.textColor = toggle.onNormal.textColor = Text;
            toggle.hover.textColor = toggle.onHover.textColor = Gold;
            // The box is the left 20 px; the transparent last column stretches under the label.
            toggle.border = new RectOffset(24, 3, 0, 0);
            toggle.overflow = new RectOffset(0, 0, -1, 1);
            toggle.padding = new RectOffset(28, 0, 2, 0);
            toggle.fixedHeight = 20f;
            toggle.fontSize = 14;

            var slider = skin.horizontalSlider;
            slider.normal.background = sliderTrackTex;
            slider.border = new RectOffset(4, 4, 4, 4);
            slider.fixedHeight = 8f;
            slider.margin = new RectOffset(4, 4, 10, 10);
            var thumb = skin.horizontalSliderThumb;
            thumb.normal.background = thumb.focused.background = thumbTex;
            thumb.hover.background = thumb.active.background = thumbHoverTex;
            // The thumb sits on the 8 px track; the overflow grows it to an 18 px circle.
            thumb.fixedWidth = 18f;
            thumb.fixedHeight = 8f;
            thumb.overflow = new RectOffset(0, 0, 5, 5);

            var scroll = skin.verticalScrollbar;
            scroll.normal.background = scrollTrackTex;
            scroll.border = new RectOffset(4, 4, 4, 4);
            scroll.fixedWidth = 8f;
            var scrollThumb = skin.verticalScrollbarThumb;
            scrollThumb.normal.background = scrollThumbTex;
            scrollThumb.border = new RectOffset(4, 4, 4, 4);
            scrollThumb.fixedWidth = 8f;
            skin.verticalScrollbarUpButton.fixedHeight = skin.verticalScrollbarDownButton.fixedHeight = 0f;
            skin.scrollView.normal.background = null;

            panelStyle = new GUIStyle { normal = { background = panelTex }, border = new RectOffset(10, 10, 10, 10) };
            cardStyle = new GUIStyle { normal = { background = cardTex }, border = new RectOffset(9, 9, 9, 9) };
            cardBox = new GUIStyle(cardStyle) { padding = new RectOffset(14, 12, 10, 12), margin = new RectOffset(5, 5, 4, 4) };
            chipStyle = new GUIStyle(Style(11, TextAnchor.MiddleCenter, bold: true))
            {
                normal = { background = chipTex, textColor = new Color(0.08f, 0.06f, 0.04f) },
                border = new RectOffset(5, 5, 5, 5),
                padding = new RectOffset(7, 7, 2, 2)
            };
        }

        // ------------------------------------------------------------------ Generated textures

        /// <summary>A rounded rectangle for 9-slicing: fill, an optional border, and an optional top highlight.</summary>
        static Texture2D Rounded(int size, int radius, Color fill, Color border, int borderWidth, bool highlight = false)
        {
            var tex = NewTexture(size, size);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedDistance(x + 0.5f, y + 0.5f, size, size, radius); // < 0 inside
                float coverage = Mathf.Clamp01(0.5f - d);
                if (coverage <= 0f) { pixels[y * size + x] = Color.clear; continue; }
                Color c = fill;
                if (highlight) c = Color.Lerp(c, Color.white, 0.18f * Mathf.Clamp01((y - size * 0.5f) / (size * 0.5f)));
                if (borderWidth > 0 && d > -borderWidth) c = Color.Lerp(c, border, border.a);
                c.a = (borderWidth > 0 && d > -borderWidth ? Mathf.Max(fill.a, border.a) : fill.a) * coverage;
                pixels[y * size + x] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static float RoundedDistance(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r);
            float qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static Texture2D Circle(int size, Color fill, Color rim)
        {
            var tex = NewTexture(size, size);
            var pixels = new Color[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - c, y + 0.5f - c).magnitude - (c - 1f);
                float coverage = Mathf.Clamp01(0.5f - d);
                var col = d > -2f ? rim : fill;
                col.a = coverage;
                pixels[y * size + x] = col;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// A toggle background: the 20 px box plus a transparent margin. Only a column well inside the margin is
        /// stretched under the label, so bilinear filtering can't smear the box's edge across the text.
        /// </summary>
        static Texture2D ToggleBox(Texture2D box)
        {
            const int width = 28;
            var tex = NewTexture(width, 20);
            var pixels = new Color[width * 20];
            for (int y = 0; y < 20; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = x < 20 ? box.GetPixel(x, y) : Color.clear;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>A white vertical gradient (lighter at the top) for bar fills, tinted at draw time.</summary>
        static Texture2D Gradient(int height)
        {
            var tex = NewTexture(1, height);
            for (int y = 0; y < height; y++)
                tex.SetPixel(0, y, Color.Lerp(new Color(0.82f, 0.82f, 0.82f), Color.white, (float)y / (height - 1)));
            tex.Apply();
            return tex;
        }

        /// <summary>A filled rounded box with a check mark.</summary>
        static Texture2D Check(Color fill, Color mark)
        {
            var tex = Rounded(20, 5, fill, new Color(1f, 0.92f, 0.7f, 0.9f), 1, highlight: true);
            var pixels = tex.GetPixels();
            var a = new Vector2(5f, 10f);
            var b = new Vector2(8.5f, 6.5f);
            var c = new Vector2(15f, 14f);
            for (int y = 0; y < 20; y++)
            for (int x = 0; x < 20; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Min(SegmentDistance(p, a, b), SegmentDistance(p, b, c));
                float ink = Mathf.Clamp01(1.9f - d);
                if (ink > 0f) pixels[y * 20 + x] = Color.Lerp(pixels[y * 20 + x], mark, ink);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (a + ab * t - p).magnitude;
        }

        static Texture2D NewTexture(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        // ------------------------------------------------------------------ Drawing helpers

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        /// <summary>The main container: a rounded ink panel with a thin gold edge.</summary>
        public static void Panel(Rect r)
        {
            if (Event.current.type != EventType.Repaint) return;
            Use();
            panelStyle.Draw(r, false, false, false, false);
        }

        /// <summary>A lighter inset card inside a panel, with an optional coloured stripe on its left edge.</summary>
        public static void Card(Rect r, Color? stripe = null)
        {
            if (Event.current.type != EventType.Repaint) return;
            Use();
            cardStyle.Draw(r, false, false, false, false);
            if (stripe is Color s) Fill(new Rect(r.x + 1f, r.y + 5f, 3f, r.height - 10f), s);
        }

        /// <summary>A rounded meter with a highlight; the label is drawn with a shadow so it reads on any fill.</summary>
        public static void Bar(Rect r, float fill, Color color, string text)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Use();
                float radius = Mathf.Min(5f, r.height * 0.5f);
                GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0f, 0f, 0f, 0.6f), Vector4.zero, radius);
                float w = (r.width - 4f) * ProgressionMath.Clean01(fill);
                if (w > 2f)
                    GUI.DrawTexture(new Rect(r.x + 2f, r.y + 2f, w, r.height - 4f), barFillTex, ScaleMode.StretchToFill, true, 0f, color,
                        Vector4.zero, Mathf.Max(1f, radius - 1f));
                GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(1f, 1f, 1f, 0.1f), Vector4.one, radius);
            }
            if (!string.IsNullOrEmpty(text)) ShadowLabel(r, text, Center);
        }

        /// <summary>Text with a soft drop shadow (labels drawn over the world or over bars).</summary>
        public static void ShadowLabel(Rect r, string text, GUIStyle style)
        {
            var color = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), StripColor(text), style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        static string StripColor(string text)
        {
            // Rich-text colour tags would tint the shadow too; drop them for the shadow pass.
            return System.Text.RegularExpressions.Regex.Replace(text, "</?color[^>]*>", "");
        }

        /// <summary>A small rounded tag ("REN", "LOCKED") in a colour.</summary>
        public static float Chip(Vector2 at, string text, Color color)
        {
            Use();
            var size = chipStyle.CalcSize(new GUIContent(text));
            var r = new Rect(at.x, at.y, size.x, Mathf.Max(18f, size.y));
            var old = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUI.Label(r, text, chipStyle);
            GUI.backgroundColor = old;
            return r.width;
        }

        /// <summary>A gold small-caps section heading with a hairline under it (for GUILayout sections).</summary>
        public static void SectionHeader(string text)
        {
            GUILayout.Space(6);
            GUILayout.Label(text.ToUpperInvariant(), Section);
            var line = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            Fill(line, new Color(1f, 0.77f, 0.32f, 0.25f));
            GUILayout.Space(4);
        }

        /// <summary>Turns "[E]" style key hints into gold key labels.</summary>
        public static string Keys(string text) =>
            System.Text.RegularExpressions.Regex.Replace(text, @"\[([^\]]{1,9})\]", "<color=#ffc552><b>[$1]</b></color>");

        /// <summary>Standard top-centre panel used by every running drill. Returns the rect left for a custom meter.</summary>
        public static Rect DrillPanel(string heading, string body, float meterHeight = 0f)
        {
            float width = Mathf.Min(540f, Screen.width - 40f);
            float height = 112f + meterHeight;
            var r = new Rect((Screen.width - width) * 0.5f, 16f, width, height);
            Panel(r);
            Fill(new Rect(r.x + 14f, r.y + 34f, 40f, 2f), Gold);
            GUI.Label(new Rect(r.x + 14, r.y + 8, r.width - 28, 26), heading, Header);
            GUI.Label(new Rect(r.x + 14, r.y + 40, r.width - 28, 64), Keys(body), Wrap);
            return new Rect(r.x + 14, r.y + 104, r.width - 28, meterHeight);
        }

        public static string Pct(float v) => $"{v * 100f:0}%";
    }
}
