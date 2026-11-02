using UnityEngine;

namespace HunterAwakening
{
    /// <summary>Esc menu: stops time and edits GameSettings. Drawn by the HUD; input toggling lives in GameSession.</summary>
    public class PauseMenu
    {
        const float LabelWidth = 170f;

        float sensitivity, sfxVolume, musicVolume;
        bool invertY, vsync, showFps, shake, hitStop;
        int fpsIndex;
        bool loaded;
        bool confirmReset;

        public void Open()
        {
            sensitivity = GameSettings.MouseSensitivity;
            invertY = GameSettings.InvertY;
            vsync = GameSettings.VSync;
            showFps = GameSettings.ShowFps;
            shake = GameSettings.CameraShake;
            hitStop = GameSettings.HitStop;
            sfxVolume = GameSettings.SfxVolume;
            musicVolume = GameSettings.MusicVolume;
            fpsIndex = System.Array.IndexOf(GameSettings.FrameRateOptions, GameSettings.FrameRateCap);
            if (fpsIndex < 0) fpsIndex = System.Array.IndexOf(GameSettings.FrameRateOptions, FrameRateSettings.TargetFrameRate);
            loaded = true;
            confirmReset = false;
        }

        public void Draw(GameSession session)
        {
            if (!loaded) Open();
            HudGui.Use();
            HudGui.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.01f, 0.02f, 0.04f, 0.62f));
            float w = Mathf.Min(520f, Screen.width - 40f), h = Mathf.Min(confirmReset ? 700f : 660f, Screen.height - 40f);
            var area = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            HudGui.Panel(area);

            GUILayout.BeginArea(new Rect(area.x + 26, area.y + 18, area.width - 52, area.height - 34));
            GUILayout.Label("HUNTER: AWAKENING", HudGui.Eyebrow);
            GUILayout.Label("Paused", HudGui.Title);
            var accent = GUILayoutUtility.GetRect(1f, 2f, GUILayout.ExpandWidth(true));
            HudGui.Fill(new Rect(accent.center.x - 24f, accent.y, 48f, 2f), HudGui.Gold);
            GUILayout.Space(4);

            HudGui.SectionHeader("Camera");
            Row($"Mouse sensitivity", () =>
            {
                sensitivity = GUILayout.HorizontalSlider(sensitivity, 0.2f, 3f);
                GUILayout.Label($"×{sensitivity:0.00}", HudGui.Label, GUILayout.Width(52));
            });
            invertY = GUILayout.Toggle(invertY, "Invert camera Y");

            HudGui.SectionHeader("Display");
            vsync = GUILayout.Toggle(vsync, "VSync (match your monitor; ignores the cap)");
            GUI.enabled = !vsync;
            Row("Frame-rate cap", () =>
            {
                if (GUILayout.Button("<", GUILayout.Width(34), GUILayout.Height(26))) fpsIndex = Mathf.Max(0, fpsIndex - 1);
                GUILayout.Label(GameSettings.FrameRateLabel(GameSettings.FrameRateOptions[fpsIndex]), HudGui.Center, GUILayout.Width(90), GUILayout.Height(26));
                if (GUILayout.Button(">", GUILayout.Width(34), GUILayout.Height(26))) fpsIndex = Mathf.Min(GameSettings.FrameRateOptions.Length - 1, fpsIndex + 1);
                GUILayout.FlexibleSpace();
            });
            GUI.enabled = true;
            showFps = GUILayout.Toggle(showFps, "Show FPS counter");

            HudGui.SectionHeader("Game feel");
            shake = GUILayout.Toggle(shake, "Camera shake");
            hitStop = GUILayout.Toggle(hitStop, "Hit-stop and slow motion");

            HudGui.SectionHeader("Audio");
            Row("Sound effects", () =>
            {
                sfxVolume = GUILayout.HorizontalSlider(sfxVolume, 0f, 1f);
                GUILayout.Label(HudGui.Pct(sfxVolume), HudGui.Label, GUILayout.Width(52));
            });
            Row("Music", () =>
            {
                musicVolume = GUILayout.HorizontalSlider(musicVolume, 0f, 1f);
                GUILayout.Label(HudGui.Pct(musicVolume), HudGui.Label, GUILayout.Width(52));
            });

            // OnGUI runs several times a frame: only persist when something actually changed.
            int fpsCap = GameSettings.FrameRateOptions[fpsIndex];
            bool changed = !Mathf.Approximately(sensitivity, GameSettings.MouseSensitivity) || invertY != GameSettings.InvertY
                           || fpsCap != GameSettings.FrameRateCap || vsync != GameSettings.VSync || showFps != GameSettings.ShowFps
                           || shake != GameSettings.CameraShake || hitStop != GameSettings.HitStop
                           || !Mathf.Approximately(sfxVolume, GameSettings.SfxVolume)
                           || !Mathf.Approximately(musicVolume, GameSettings.MusicVolume);
            if (changed) GameSettings.Set(sensitivity, invertY, fpsCap, vsync, showFps, shake, hitStop, sfxVolume, musicVolume);

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Resume", HudGui.PrimaryButton, GUILayout.Height(38))) session.SetPaused(false);
            if (GUILayout.Button("Save", GUILayout.Width(96), GUILayout.Height(38)) && session.Save()) session.PushNotice("Game saved.");
            if (GUILayout.Button("Quit", GUILayout.Width(96), GUILayout.Height(38)))
            {
                session.Save();
                Application.Quit();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            // Starting over takes two clicks: the second, explicit one deletes the save.
            if (confirmReset)
            {
                var box = GUILayoutUtility.GetRect(1f, 78f, GUILayout.ExpandWidth(true));
                HudGui.Card(box, HudGui.Bad);
                GUILayout.BeginArea(new Rect(box.x + 14, box.y + 8, box.width - 24, box.height - 12));
                GUILayout.Label("Delete all progress and start a new Hunter? Settings are kept.", HudGui.Wrap);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Yes, start over", HudGui.DangerButton, GUILayout.Height(30))) session.DeleteSaveAndRestart();
                if (GUILayout.Button("Cancel", GUILayout.Width(110), GUILayout.Height(30))) confirmReset = false;
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
            else
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset progress…", HudGui.DangerButton, GUILayout.Width(160), GUILayout.Height(28))) confirmReset = true;
                GUILayout.FlexibleSpace();
                GUILayout.Label("Esc to resume", HudGui.Small, GUILayout.Width(110));
                GUILayout.EndHorizontal();
            }
            if (Application.isEditor) GUILayout.Label("In the Editor, Quit just saves — stop Play mode to exit.", HudGui.Small);
            GUILayout.EndArea();
        }

        static void Row(string label, System.Action control)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, HudGui.Label, GUILayout.Width(LabelWidth));
            control();
            GUILayout.EndHorizontal();
        }
    }
}
