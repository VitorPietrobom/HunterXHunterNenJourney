using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// The dashboard's Hatsu page, for a created Hunter: the ability's name, type and vow, its Lua source in an editor,
    /// and the nen API reference. "Check" validates without running; "Save & use" stores it in the save and reloads the
    /// runtime; "Restore" brings back what the interview wrote.
    /// </summary>
    public class HatsuPage
    {
        static GUIStyle mono, monoArea;

        /// <summary>A monospaced style for code (Consolas on Windows; falls back to the UI font elsewhere).</summary>
        public static GUIStyle Mono
        {
            get
            {
                if (mono != null) return mono;
                mono = new GUIStyle(HudGui.Wrap) { fontSize = 13, richText = false, wordWrap = true };
                var font = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "Menlo", "DejaVu Sans Mono" }, 13);
                if (font != null) mono.font = font;
                mono.normal.textColor = new Color(0.85f, 0.92f, 0.85f);
                return mono;
            }
        }

        static GUIStyle MonoArea
        {
            get
            {
                if (monoArea != null) return monoArea;
                monoArea = new GUIStyle(GUI.skin.textArea) { fontSize = 13, wordWrap = false, richText = false };
                monoArea.font = Mono.font;
                monoArea.normal.textColor = monoArea.focused.textColor = new Color(0.88f, 0.95f, 0.88f);
                return monoArea;
            }
        }

        string draft;
        string status;
        Vector2 codeScroll, refScroll;

        public void Draw(Rect area, GameSession session)
        {
            var p = session.Progression;
            var hunter = p.Hunter;
            var runtime = session.Player != null ? session.Player.GetComponent<HatsuRuntime>() : null;
            draft ??= hunter.hatsuSource;

            GUILayout.BeginArea(area);
            GUILayout.BeginHorizontal();

            // Left: about the ability and the reference.
            GUILayout.BeginVertical(HudGui.CardBox, GUILayout.Width(Mathf.Min(380f, area.width * 0.36f)), GUILayout.ExpandHeight(true));
            var title = new GUIStyle(HudGui.Header) { fontSize = 20 };
            title.normal.textColor = hunter.auraColor;
            GUILayout.Label(hunter.hatsuName, title);
            GUILayout.Label($"<color=#9aa4b2>{hunter.name} · {hunter.nenType}</color>", HudGui.Label);
            GUILayout.Space(4);
            GUILayout.Label(hunter.hatsuDescription, HudGui.Wrap);
            GUILayout.Space(6);
            bool learned = session.Player != null && session.Player.Aura.JajankenUnlocked;
            GUILayout.Label(learned
                ? "<color=#66dd77><b>Learned</b></color> — hold or press <b>V</b> to use it."
                : "<color=#ffc552><b>Not learned yet</b></color> — master Ko, then raise Ren mastery to 50. You can shape it already.", HudGui.Wrap);
            if (runtime != null)
            {
                string state = runtime.Loaded
                    ? $"<color=#66dd77>Running:</color> {runtime.Info.name} · cost {runtime.Info.cost:0} · cooldown {runtime.Info.cooldown:0.#}s · charge {runtime.Info.charge:0.#}s"
                    : $"<color=#ff7766>Not running:</color> {runtime.LastError ?? "no script."}";
                GUILayout.Label($"<size=12>{state}</size>", HudGui.Wrap);
            }
            HudGui.SectionHeader("The nen API");
            refScroll = GUILayout.BeginScrollView(refScroll);
            GUILayout.Label(HatsuLibrary.Reference, Mono);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Right: the code.
            GUILayout.BeginVertical(HudGui.CardBox, GUILayout.ExpandHeight(true));
            GUILayout.Label("Code (Lua) — it's yours: change the numbers, the targets, the whole idea.", HudGui.Section);
            codeScroll = GUILayout.BeginScrollView(codeScroll, GUILayout.ExpandHeight(true));
            draft = GUILayout.TextArea(draft ?? "", MonoArea, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            GUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(status)) GUILayout.Label($"<size=12>{status}</size>", HudGui.Wrap);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Check", GUILayout.Width(110), GUILayout.Height(30)))
                status = HatsuRuntime.Validate(draft, out var meta, out var error)
                    ? $"<color=#66dd77>Looks good:</color> {meta.name} · cost {meta.cost:0} · cooldown {meta.cooldown:0.#}s · charge {meta.charge:0.#}s"
                    : $"<color=#ff7766>Problem:</color> {error}";
            if (GUILayout.Button("Save & use", HudGui.PrimaryButton, GUILayout.Width(130), GUILayout.Height(30)))
            {
                if (HatsuRuntime.Validate(draft, out var meta, out var error))
                {
                    hunter.hatsuSource = draft;
                    hunter.hatsuName = meta.name;
                    runtime?.Reload();
                    session.Save();
                    status = $"<color=#66dd77>Saved.</color> {meta.name} is ready.";
                }
                else status = $"<color=#ff7766>Not saved — fix this first:</color> {error}";
            }
            if (GUILayout.Button("Discard changes", GUILayout.Width(140), GUILayout.Height(30)))
            {
                draft = hunter.hatsuSource;
                status = "Back to the saved version.";
            }
            if (GUILayout.Button("Restore the interview's", GUILayout.Width(190), GUILayout.Height(30)))
            {
                draft = hunter.generatedSource;
                status = "The interview's version is in the editor — Save & use to keep it.";
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
