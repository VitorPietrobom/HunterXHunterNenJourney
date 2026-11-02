using System.Collections.Generic;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// A new game's first choice: play as Gon, or create a Hunter. Creating one goes through a customization page
    /// (name, body, height, aura colour — previewed live on the title screen's orbiting camera), Kite's interview,
    /// and the result: the Nen type and an original Hatsu, written as a Lua script the player can read and edit.
    /// Opened by the title screen; it keeps the title's input block until the player starts.
    /// </summary>
    public class CharacterCreator : MonoBehaviour
    {
        enum Page { Closed, Choice, Customize, Interview, Result }

        static readonly Color[] Swatches =
        {
            new Color(1f, 0.55f, 0.15f), new Color(1f, 0.3f, 0.3f), new Color(1f, 0.85f, 0.35f), new Color(0.35f, 0.95f, 0.55f),
            new Color(0.35f, 0.6f, 1f), new Color(0.75f, 0.35f, 1f), new Color(0.95f, 0.95f, 1f), new Color(0.2f, 0.2f, 0.25f),
        };

        GameSession session;
        Page page;
        HunterProfile profile;
        readonly List<int> picks = new List<int>();
        int bodyIndex;
        bool autoColor = true;
        Vector2 codeScroll;
        System.Action onDone;

        public bool IsOpen => page != Page.Closed;

        void Awake() => session = GetComponent<GameSession>();

        /// <summary>Starts the new-game choice; <paramref name="done"/> runs when the player starts playing.</summary>
        public void Open(System.Action done)
        {
            onDone = done;
            profile = new HunterProfile { custom = true, name = "", body = "Gon" };
            picks.Clear();
            bodyIndex = 0;
            autoColor = true;
            page = Page.Choice;
        }

        PlayerBodies Bodies => session.Player != null ? session.Player.GetComponent<PlayerBodies>() : null;

        void Preview()
        {
            var bodies = Bodies;
            if (bodies != null) bodies.Show(profile.body, profile.height);
        }

        void Finish(HunterProfile chosen)
        {
            session.Progression.SetHunter(chosen);
            session.Progression.MarkStory("chosen");
            if (session.Player.TryGetComponent(out HatsuRuntime runtime)) runtime.Reload();
            var bodies = Bodies;
            if (bodies != null) bodies.Show(chosen.body, chosen.height);
            session.Save();
            page = Page.Closed;
            onDone?.Invoke();
        }

        void OnGUI()
        {
            if (page == Page.Closed) return;
            HudGui.Use();
            GUI.depth = -5;
            switch (page)
            {
                case Page.Choice: DrawChoice(); break;
                case Page.Customize: DrawCustomize(); break;
                case Page.Interview: DrawInterview(); break;
                case Page.Result: DrawResult(); break;
            }
        }

        static Rect Card(float w, float h, float yFrac = 0.5f)
        {
            var r = new Rect((Screen.width - w) / 2f, Mathf.Max(20f, Screen.height * yFrac - h / 2f), w, h);
            HudGui.Panel(r);
            return r;
        }

        void Header(Rect r, string eyebrow, string title)
        {
            GUI.Label(new Rect(r.x, r.y + 14, r.width, 18), eyebrow, HudGui.Eyebrow);
            GUI.Label(new Rect(r.x, r.y + 32, r.width, 36), title, HudGui.Title);
            HudGui.Fill(new Rect(r.center.x - 24f, r.y + 70, 48f, 2f), HudGui.Gold);
        }

        // ------------------------------------------------------------------ Choice

        void DrawChoice()
        {
            var r = Card(620f, 300f, 0.62f);
            Header(r, "A NEW HUNTER", "Who are you?");
            float w = (r.width - 60f) / 2f, y = r.y + 92f;
            var gon = new Rect(r.x + 20f, y, w, 150f);
            var mine = new Rect(gon.xMax + 20f, y, w, 150f);
            HudGui.Panel(gon);
            HudGui.Panel(mine);
            GUI.Label(new Rect(gon.x + 14, gon.y + 10, gon.width - 28, 24), "<b>Play as Gon</b>", HudGui.Header);
            GUI.Label(new Rect(gon.x + 14, gon.y + 36, gon.width - 28, 70), "Ging's son, an Enhancer. His story, his Hatsu: Jajanken.", HudGui.Wrap);
            GUI.Label(new Rect(mine.x + 14, mine.y + 10, mine.width - 28, 24), "<b>Create your Hunter</b>", HudGui.Header);
            GUI.Label(new Rect(mine.x + 14, mine.y + 36, mine.width - 28, 70),
                "Your name, your look. An interview reveals your Nen type and writes you an original Hatsu — in code you can change.", HudGui.Wrap);
            if (GUI.Button(new Rect(gon.x + 14, gon.yMax - 44, gon.width - 28, 32), "Play as Gon", HudGui.PrimaryButton))
            {
                var bodies = Bodies;
                if (bodies != null) bodies.Show("Gon", 1f);
                Finish(new HunterProfile());
            }
            if (GUI.Button(new Rect(mine.x + 14, mine.yMax - 44, mine.width - 28, 32), "Create a Hunter", HudGui.PrimaryButton))
            {
                page = Page.Customize;
                Preview();
            }
        }

        // ------------------------------------------------------------------ Customize

        void DrawCustomize()
        {
            // Left side, so the orbiting camera shows the body in the middle of the screen.
            float w = 380f, h = 470f;
            var r = new Rect(30f, (Screen.height - h) / 2f, w, h);
            HudGui.Panel(r);
            GUI.Label(new Rect(r.x, r.y + 14, r.width, 18), "CREATE YOUR HUNTER", HudGui.Eyebrow);
            float x = r.x + 22f, y = r.y + 46f, fw = r.width - 44f;

            GUI.Label(new Rect(x, y, fw, 18), "NAME", HudGui.Section);
            y += 20f;
            profile.name = GUI.TextField(new Rect(x, y, fw, 30), profile.name ?? "", 18);
            y += 44f;

            var names = Bodies != null ? Bodies.Names : new[] { "Gon" };
            GUI.Label(new Rect(x, y, fw, 18), "BODY", HudGui.Section);
            y += 20f;
            if (GUI.Button(new Rect(x, y, 40, 30), "<"))
            {
                bodyIndex = (bodyIndex + names.Length - 1) % names.Length;
                profile.body = names[bodyIndex];
                Preview();
            }
            GUI.Label(new Rect(x + 44, y, fw - 88, 30), $"<b>{profile.body}</b>", HudGui.Center);
            if (GUI.Button(new Rect(x + fw - 40, y, 40, 30), ">"))
            {
                bodyIndex = (bodyIndex + 1) % names.Length;
                profile.body = names[bodyIndex];
                Preview();
            }
            y += 44f;

            GUI.Label(new Rect(x, y, fw, 18), $"HEIGHT  <color=#9aa4b2>×{profile.height:0.00}</color>", HudGui.Section);
            y += 22f;
            float height = GUI.HorizontalSlider(new Rect(x, y, fw, 20), profile.height, 0.85f, 1.2f);
            if (!Mathf.Approximately(height, profile.height))
            {
                profile.height = height;
                Preview();
            }
            y += 34f;

            GUI.Label(new Rect(x, y, fw, 18), "AURA COLOUR", HudGui.Section);
            y += 22f;
            autoColor = GUI.Toggle(new Rect(x, y, fw, 22), autoColor, "Decided by my Nen type");
            y += 28f;
            GUI.enabled = !autoColor;
            float sw = (fw - 7 * 6f) / 8f;
            for (int i = 0; i < Swatches.Length; i++)
            {
                var cell = new Rect(x + i * (sw + 6f), y, sw, sw);
                if (GUI.Button(cell, GUIContent.none)) profile.auraColor = Swatches[i];
                HudGui.Fill(new Rect(cell.x + 3, cell.y + 3, cell.width - 6, cell.height - 6), Swatches[i]);
                if (!autoColor && profile.auraColor == Swatches[i]) HudGui.Fill(new Rect(cell.x, cell.yMax + 2, cell.width, 2), HudGui.Gold);
            }
            GUI.enabled = true;
            y += sw + 22f;

            GUI.Label(new Rect(x, y, fw, 40), "<size=12><color=#9aa4b2>Your Nen type and your Hatsu come from the interview with Kite.</color></size>", HudGui.Wrap);

            bool named = !string.IsNullOrWhiteSpace(profile.name);
            if (GUI.Button(new Rect(x, r.yMax - 50, 110, 34), "Back")) page = Page.Choice;
            GUI.enabled = named;
            if (GUI.Button(new Rect(x + fw - 180, r.yMax - 50, 180, 34), named ? "Meet Kite" : "Choose a name", HudGui.PrimaryButton))
            {
                profile.name = profile.name.Trim();
                picks.Clear();
                page = Page.Interview;
            }
            GUI.enabled = true;
        }

        // ------------------------------------------------------------------ Interview

        void DrawInterview()
        {
            int q = picks.Count;
            var question = HatsuLibrary.Interview[q];
            var r = Card(700f, 420f, 0.6f);
            GUI.Label(new Rect(r.x, r.y + 14, r.width, 18), $"KITE'S INTERVIEW  ·  {q + 1} / {HatsuLibrary.Interview.Length}", HudGui.Eyebrow);
            var nameStyle = new GUIStyle(HudGui.Header);
            GUI.Label(new Rect(r.x + 28, r.y + 44, 200, 24), "Kite", nameStyle);
            var text = new GUIStyle(HudGui.Wrap) { fontSize = 17 };
            GUI.Label(new Rect(r.x + 28, r.y + 72, r.width - 56, 60), question.text, text);
            float y = r.y + 140f;
            for (int i = 0; i < question.answers.Length; i++)
            {
                if (GUI.Button(new Rect(r.x + 28, y, r.width - 56, 44), question.answers[i].text))
                {
                    picks.Add(i);
                    if (picks.Count >= HatsuLibrary.Interview.Length) Conclude();
                }
                y += 52f;
            }
            if (GUI.Button(new Rect(r.x + 28, r.yMax - 46, 110, 30), "Back"))
            {
                if (picks.Count > 0) picks.RemoveAt(picks.Count - 1);
                else page = Page.Customize;
            }
        }

        void Conclude()
        {
            HatsuLibrary.Author(profile, picks);
            profile.interviewAnswers.Clear();
            for (int i = 0; i < picks.Count; i++) profile.interviewAnswers.Add(HatsuLibrary.Interview[i].answers[picks[i]].text);
            if (autoColor) profile.auraColor = HatsuLibrary.TypeColor(profile.nenType);
            codeScroll = Vector2.zero;
            page = Page.Result;
        }

        // ------------------------------------------------------------------ Result

        void DrawResult()
        {
            var r = Card(Mathf.Min(980f, Screen.width - 40f), Mathf.Min(640f, Screen.height - 40f));
            Header(r, $"{profile.name.ToUpperInvariant()}'S NEN", $"{profile.nenType}");
            float x = r.x + 26f, y = r.y + 84f, half = (r.width - 70f) / 2f;

            // Left: the reading and the Hatsu.
            GUI.Label(new Rect(x, y, half, 20), "WATER DIVINATION", HudGui.Section);
            GUI.Label(new Rect(x, y + 20, half, 40), $"<i>{HatsuLibrary.Divination(profile.nenType)}</i>  You are a{(profile.nenType == NenType.Enhancer ? "n" : "")} <b>{profile.nenType}</b>.", HudGui.Wrap);
            y += 74f;
            GUI.Label(new Rect(x, y, half, 20), "YOUR HATSU", HudGui.Section);
            var title = new GUIStyle(HudGui.Header) { fontSize = 22 };
            title.normal.textColor = profile.auraColor;
            GUI.Label(new Rect(x, y + 20, half, 32), profile.hatsuName, title);
            float descH = HudGui.Wrap.CalcHeight(new GUIContent(profile.hatsuDescription), half);
            GUI.Label(new Rect(x, y + 56, half, descH), profile.hatsuDescription, HudGui.Wrap);
            y += 66f + descH;
            GUI.Label(new Rect(x, y, half, 80),
                "<size=12><color=#9aa4b2>You'll learn to use it once you've mastered Ko and pushed your Ren far enough (Kite will tell you). " +
                "Then hold or press <b>V</b>. Its code is yours: change it any time in the dashboard (Tab → Hatsu).</color></size>", HudGui.Wrap);

            // Right: the code.
            float cx = x + half + 18f;
            GUI.Label(new Rect(cx, r.y + 84f, half, 20), "ITS CODE (LUA)", HudGui.Section);
            var area = new Rect(cx, r.y + 106f, half, r.height - 180f);
            HudGui.Fill(area, new Color(0f, 0f, 0f, 0.35f));
            var mono = HatsuPage.Mono;
            float codeH = mono.CalcHeight(new GUIContent(profile.hatsuSource), half - 24f);
            codeScroll = GUI.BeginScrollView(area, codeScroll, new Rect(0, 0, half - 20f, codeH + 10f));
            GUI.Label(new Rect(6, 4, half - 26f, codeH), profile.hatsuSource, mono);
            GUI.EndScrollView();

            if (GUI.Button(new Rect(x, r.yMax - 52, 180, 34), "Retake the interview"))
            {
                picks.Clear();
                page = Page.Interview;
            }
            if (GUI.Button(new Rect(x + 190, r.yMax - 52, 140, 34), "Change look")) page = Page.Customize;
            if (GUI.Button(new Rect(r.xMax - 26 - 220, r.yMax - 52, 220, 34), $"Begin as {profile.name}", HudGui.PrimaryButton))
                Finish(profile);
        }
    }
}
