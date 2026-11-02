using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// The launch screen: the title over the training grounds while the camera slowly circles Gon. Continue (or begin),
    /// start a new game, or quit. Shown once per launch; gameplay input is blocked until it is dismissed.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        static bool shownThisLaunch;
        /// <summary>A new game was chosen: after the reload, go straight to the character choice.</summary>
        static bool creatorPending;
        static Texture2D shade;

        GameSession session;
        bool confirmNewGame;
        float openedAt;

        void Awake()
        {
            session = GetComponent<GameSession>();
            if ((shownThisLaunch && !creatorPending) || session == null)
            {
                enabled = false;
                return;
            }
            session.TitleOpen = true;
            openedAt = Time.unscaledTime;
            creator = GetComponent<CharacterCreator>();
            if (creator == null) creator = gameObject.AddComponent<CharacterCreator>();
            if (creatorPending)
            {
                creatorPending = false;
                BeginNewHunter();
            }
        }

        CharacterCreator creator;

        /// <summary>A fresh save: choose Gon or create a Hunter, then play.</summary>
        void BeginNewHunter() => creator.Open(Dismiss);

        bool FreshSave => session.Progression.TrainingSessions == 0 && !session.Progression.IsAuraAwakened
                          && !session.Progression.HasHeardStory("chosen");

        // The editor skips reloading scripts between Plays (Enter Play Mode Options), so statics survive from the last
        // run: reset them, or the title would only ever show once per editor session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            shownThisLaunch = false;
            creatorPending = false;
        }

        /// <summary>
        /// Starting over (from the title, the pause menu or the dashboard): the reloaded scene opens on "Who are you?"
        /// instead of dropping straight into the game.
        /// </summary>
        public static void RequestNewGame()
        {
            shownThisLaunch = true;
            creatorPending = true;
        }

        /// <summary>Skips the title (tests, or anything that starts play programmatically).</summary>
        public static void SkipThisLaunch() => shownThisLaunch = true;

        public void Dismiss()
        {
            shownThisLaunch = true;
            session.TitleOpen = false;
            enabled = false;
        }

        static Texture2D Shade()
        {
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
            {
                float towardBottom = 1f - y / 63f; // texture row 0 is the bottom of the screen
                tex.SetPixel(0, y, new Color(0.01f, 0.02f, 0.04f, Mathf.Lerp(0.2f, 0.82f, towardBottom * towardBottom)));
            }
            tex.Apply();
            return tex;
        }

        void OnGUI()
        {
            HudGui.Use();
            if (creator != null && creator.IsOpen)
            {
                if (shade == null) shade = Shade();
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), shade, ScaleMode.StretchToFill);
                return;
            }
            var e = Event.current;
            if (e.type == EventType.KeyDown && !confirmNewGame && FreshSave
                && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Space))
            {
                BeginNewHunter();
                e.Use();
                return;
            }
            if (e.type == EventType.KeyDown && !confirmNewGame
                && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Space))
            {
                Dismiss();
                e.Use();
                return;
            }

            // Darken towards the bottom so the title and buttons read over the scenery.
            if (shade == null) shade = Shade();
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), shade, ScaleMode.StretchToFill);

            float fade = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.8f);
            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, fade);

            // The title block scales as one unit with the screen height.
            float k = Screen.height / 1080f;
            float cx = Screen.width * 0.5f;
            float top = Screen.height * 0.18f;
            var big = new GUIStyle(HudGui.Title) { fontSize = Mathf.RoundToInt(72 * k) };
            var bigGold = new GUIStyle(big);
            bigGold.normal.textColor = HudGui.Gold;
            HudGui.ShadowLabel(new Rect(cx - 500, top, 1000, 90 * k), "<b>HUNTER</b>", big);
            HudGui.ShadowLabel(new Rect(cx - 500, top + 82 * k, 1000, 90 * k), "<b>AWAKENING</b>", bigGold);
            float lineY = top + 186 * k;
            HudGui.Fill(new Rect(cx - 60, lineY, 120, 2), HudGui.Gold);
            HudGui.ShadowLabel(new Rect(cx - 300, lineY + 12, 600, 24), "Train your body. Awaken your aura. Earn your strength.", HudGui.Center);

            const float w = 300f;
            float y = Screen.height * 0.58f;
            bool hasSave = session.Progression.TrainingSessions > 0 || session.Progression.IsAuraAwakened;
            if (!confirmNewGame)
            {
                if (GUI.Button(new Rect(cx - w / 2, y, w, 46), hasSave ? "Continue" : "Begin your training", HudGui.PrimaryButton))
                {
                    if (FreshSave) BeginNewHunter();
                    else Dismiss();
                }
                y += 54f;
                if (hasSave && GUI.Button(new Rect(cx - w / 2, y, w, 40), "New game")) confirmNewGame = true;
                if (hasSave) y += 48f;
                if (GUI.Button(new Rect(cx - w / 2, y, w, 40), "Quit")) Application.Quit();
                y += 56f;
                GUI.Label(new Rect(cx - w / 2, y, w, 20), "<color=#9aa4b2>Press Enter to start · F1 shows the controls</color>", HudGui.Center);
            }
            else
            {
                var box = new Rect(cx - 190, y, 380, 104);
                HudGui.Panel(box);
                GUI.Label(new Rect(box.x + 16, box.y + 12, box.width - 32, 40),
                    $"Start over? Your Hunter's progress ({session.Progression.TrainingSessions} sessions) will be deleted.", HudGui.Wrap);
                if (GUI.Button(new Rect(box.x + 16, box.y + 58, 200, 32), "Yes, start over", HudGui.DangerButton))
                {
                    session.DeleteSaveAndRestart(); // reopens on "Who are you?"
                }
                if (GUI.Button(new Rect(box.x + 228, box.y + 58, 136, 32), "Cancel")) confirmNewGame = false;
            }
            GUI.Label(new Rect(16, Screen.height - 28, 400, 20), "<size=11><color=#9aa4b2>Prototype · a Hunter x Hunter fan project</color></size>", HudGui.Small);
            GUI.color = oldColor;
        }
    }
}
