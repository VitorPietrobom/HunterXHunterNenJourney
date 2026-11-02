using System;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// A conversation at the bottom of the screen: the speaker's name, their line typing out, and a prompt to go on.
    /// E, Space, Enter or a click finishes the line, then moves to the next; gameplay input is blocked meanwhile.
    /// </summary>
    public class DialogueBox : MonoBehaviour
    {
        const float CharactersPerSecond = 55f;

        static DialogueBox instance;

        string speaker;
        string[] lines;
        int index;
        float shown;
        Action onDone;

        public static bool IsOpen => instance != null && instance.lines != null;
        /// <summary>When the last conversation closed (so the key that closed it doesn't open the next one).</summary>
        public static float ClosedAt { get; private set; } = float.NegativeInfinity;

        void Awake() => instance = this;

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Play(string speaker, string[] lines, Action onDone = null)
        {
            if (instance == null || lines == null || lines.Length == 0) return;
            instance.speaker = speaker;
            instance.lines = lines;
            instance.index = 0;
            instance.shown = 0f;
            instance.onDone = onDone;
        }

        void Update()
        {
            if (lines != null) shown += Time.unscaledDeltaTime * CharactersPerSecond;
        }

        void Advance()
        {
            if (shown < lines[index].Length)
            {
                shown = lines[index].Length; // first press finishes the line
                return;
            }
            index++;
            shown = 0f;
            if (index < lines.Length) return;
            lines = null;
            ClosedAt = Time.unscaledTime;
            var done = onDone;
            onDone = null;
            done?.Invoke();
        }

        void OnGUI()
        {
            if (lines == null) return;
            var session = GameSession.Instance;
            if (session != null && (session.Paused || session.TitleOpen)) return;
            HudGui.Use();
            GUI.depth = -10;

            var e = Event.current;
            if ((e.type == EventType.KeyDown && (e.keyCode == KeyCode.E || e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
                || (e.type == EventType.MouseDown && e.button == 0))
            {
                Advance();
                e.Use();
                if (lines == null) return;
            }

            float w = Mathf.Min(820f, Screen.width - 40f), h = 150f;
            var box = new Rect((Screen.width - w) / 2f, Screen.height - h - 28f, w, h);
            HudGui.Panel(box);
            HudGui.Fill(new Rect(box.x + 1f, box.y + 10f, 3f, box.height - 20f), HudGui.Gold);

            // Name plate on the top edge.
            var nameSize = HudGui.Header.CalcSize(new GUIContent(speaker));
            var plate = new Rect(box.x + 20f, box.y - 16f, nameSize.x + 28f, 30f);
            HudGui.Panel(plate);
            GUI.Label(new Rect(plate.x + 14f, plate.y + 4f, nameSize.x + 4f, 24f), speaker, HudGui.Header);

            string line = lines[index];
            int count = Mathf.Min(line.Length, Mathf.FloorToInt(shown));
            // Don't cut a rich-text tag in half while typing.
            string visible = line.Substring(0, count);
            int open = visible.LastIndexOf('<');
            if (open >= 0 && visible.IndexOf('>', open) < 0) visible = visible.Substring(0, open);
            var text = new GUIStyle(HudGui.Wrap) { fontSize = 16 };
            GUI.Label(new Rect(box.x + 24f, box.y + 24f, box.width - 48f, box.height - 54f), visible, text);

            string more = index < lines.Length - 1 ? "next" : "close";
            GUI.Label(new Rect(box.xMax - 260f, box.yMax - 28f, 240f, 20f),
                $"<size=11><color=#9aa4b2>{index + 1}/{lines.Length}   <b>E</b> / <b>Space</b> / click to {more}</color></size>",
                new GUIStyle(HudGui.Small) { alignment = TextAnchor.MiddleRight });
        }
    }
}
