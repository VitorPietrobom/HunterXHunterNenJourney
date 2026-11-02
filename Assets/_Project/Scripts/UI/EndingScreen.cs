using System;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// The ending, after the King falls the first time: the screen fades, the story closes, the journey's numbers and
    /// the credits roll. "Keep training" returns to free play — nothing is lost, and the world stays open.
    /// </summary>
    public class EndingScreen : MonoBehaviour
    {
        GameSession session;
        float openedAt;
        bool open;

        void Awake() => session = GetComponent<GameSession>();

        /// <summary>Shows the ending after a pause, so the King's fall plays out first.</summary>
        public void ShowAfter(float seconds) => Invoke(nameof(Show), seconds);

        public void Show()
        {
            if (open || session == null) return;
            open = true;
            session.EndingOpen = true;
            openedAt = Time.unscaledTime;
            session.Progression.MarkStory("ending");
            session.Save();
        }

        void Close()
        {
            open = false;
            session.EndingOpen = false;
        }

        void OnGUI()
        {
            if (!open) return;
            HudGui.Use();
            GUI.depth = -20;
            float t = Time.unscaledTime - openedAt;
            float fade = Mathf.Clamp01(t / 2.5f);
            HudGui.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.01f, 0.01f, 0.03f, 0.92f * fade));
            if (t < 1.2f) return;

            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((t - 1.2f) / 1.5f));
            float k = Screen.height / 1080f;
            float cx = Screen.width / 2f, w = Mathf.Min(760f, Screen.width - 60f);
            float y = Screen.height * 0.12f;

            GUI.Label(new Rect(cx - w / 2, y, w, 20), "HUNTER: AWAKENING", HudGui.Eyebrow);
            y += 26;
            var title = new GUIStyle(HudGui.Title) { fontSize = Mathf.RoundToInt(48 * k) };
            title.normal.textColor = HudGui.Gold;
            HudGui.ShadowLabel(new Rect(cx - w / 2, y, w, 64 * k), "<b>The King Has Fallen</b>", title);
            y += 70 * k + 10;

            var body = new GUIStyle(HudGui.Wrap) { fontSize = 16, alignment = TextAnchor.UpperCenter };
            const string story =
                "The palace is silent. The aura that pressed down on the woods is gone, and the last of the Chimera Ants scatter " +
                "into the trees.\n\nYou came here with nothing but a strong body and a stubborn heart. You leave it an Enhancer, " +
                "a Nen user who stood in front of a King and did not step back. Kite would say you still have a long way to go. " +
                "He would also be smiling when he said it.";
            float bodyH = body.CalcHeight(new GUIContent(story), w);
            GUI.Label(new Rect(cx - w / 2, y, w, bodyH), story, body);
            y += bodyH + 24;

            var p = session.Progression;
            string stats =
                $"<color=#9aa4b2>Time</color> {TimeSpan.FromSeconds(p.State.activePlaySeconds):hh\\:mm\\:ss}    " +
                $"<color=#9aa4b2>Training sessions</color> {p.TrainingSessions}    " +
                $"<color=#9aa4b2>Hunter Level</color> {p.HunterLevel}\n" +
                $"<color=#9aa4b2>Strength</color> {p.GetAttribute(AttributeId.Strength):0}    " +
                $"<color=#9aa4b2>Speed</color> {p.GetAttribute(AttributeId.Speed):0}    " +
                $"<color=#9aa4b2>Durability</color> {p.GetAttribute(AttributeId.Durability):0}    " +
                $"<color=#9aa4b2>Max Output</color> {p.GetAttribute(AttributeId.MaxOutput):0}";
            GUI.Label(new Rect(cx - w / 2, y, w, 44), stats, new GUIStyle(HudGui.Center) { fontSize = 14 });
            y += 60;

            HudGui.Fill(new Rect(cx - 40, y, 80, 2), HudGui.Gold);
            y += 14;
            const string credits =
                "A Hunter x Hunter fan prototype\n" +
                "Hunter x Hunter created by Yoshihiro Togashi · 2011 anime by Madhouse · music by Yoshihisa Hirano\n" +
                "Character models by community creators (Sketchfab, VRChat) · built with Unity\n" +
                "Thank you for playing.";
            GUI.Label(new Rect(cx - w / 2, y, w, 80), $"<size=12>{credits}</size>", new GUIStyle(HudGui.Small) { alignment = TextAnchor.UpperCenter });
            y += 96;

            if (t > 3f && GUI.Button(new Rect(cx - 150, Mathf.Min(y, Screen.height - 70), 300, 44), "Keep training", HudGui.PrimaryButton))
                Close();
            GUI.color = old;
        }
    }
}
