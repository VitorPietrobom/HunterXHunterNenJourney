using UnityEngine;

namespace HunterAwakening
{
    /// <summary>Esc menu: stops time and edits GameSettings. Drawn by the HUD; input toggling lives in GameSession.</summary>
    public class PauseMenu
    {
        float sensitivity, sfxVolume;
        bool invertY, vsync, showFps, shake, hitStop;
        int fpsIndex;
        bool loaded;

        public void Open()
        {
            sensitivity = GameSettings.MouseSensitivity;
            invertY = GameSettings.InvertY;
            vsync = GameSettings.VSync;
            showFps = GameSettings.ShowFps;
            shake = GameSettings.CameraShake;
            hitStop = GameSettings.HitStop;
            sfxVolume = GameSettings.SfxVolume;
            fpsIndex = System.Array.IndexOf(GameSettings.FrameRateOptions, GameSettings.FrameRateCap);
            if (fpsIndex < 0) fpsIndex = System.Array.IndexOf(GameSettings.FrameRateOptions, FrameRateSettings.TargetFrameRate);
            loaded = true;
        }

        public void Draw(GameSession session)
        {
            if (!loaded) Open();
            HudGui.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));
            float w = Mathf.Min(460f, Screen.width - 40f), h = 480f;
            var area = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            HudGui.Panel(area);

            GUILayout.BeginArea(new Rect(area.x + 20, area.y + 14, area.width - 40, area.height - 28));
            GUILayout.Label("PAUSED", HudGui.Title);
            GUILayout.Space(8);

            GUILayout.Label($"Mouse sensitivity ×{sensitivity:0.00}", HudGui.Label);
            sensitivity = GUILayout.HorizontalSlider(sensitivity, 0.2f, 3f);
            GUILayout.Space(6);
            invertY = GUILayout.Toggle(invertY, " Invert camera Y");
            GUILayout.Space(6);

            vsync = GUILayout.Toggle(vsync, " VSync (matches your monitor; ignores the cap)");
            GUI.enabled = !vsync;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Frame-rate cap", HudGui.Label, GUILayout.Width(120));
            if (GUILayout.Button("<", GUILayout.Width(30))) fpsIndex = Mathf.Max(0, fpsIndex - 1);
            GUILayout.Label(GameSettings.FrameRateLabel(GameSettings.FrameRateOptions[fpsIndex]), HudGui.Center, GUILayout.Width(90));
            if (GUILayout.Button(">", GUILayout.Width(30))) fpsIndex = Mathf.Min(GameSettings.FrameRateOptions.Length - 1, fpsIndex + 1);
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUILayout.Space(6);

            showFps = GUILayout.Toggle(showFps, " Show FPS counter");
            shake = GUILayout.Toggle(shake, " Camera shake");
            hitStop = GUILayout.Toggle(hitStop, " Hit-stop and slow motion");
            GUILayout.Label($"Sound effects {sfxVolume * 100f:0}%", HudGui.Label);
            sfxVolume = GUILayout.HorizontalSlider(sfxVolume, 0f, 1f);

            GameSettings.Set(sensitivity, invertY, GameSettings.FrameRateOptions[fpsIndex], vsync, showFps, shake, hitStop, sfxVolume);

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Resume (Esc)", GUILayout.Height(32))) session.SetPaused(false);
            if (GUILayout.Button("Save", GUILayout.Height(32)) && session.Save()) session.PushNotice("Game saved.");
            if (GUILayout.Button("Quit", GUILayout.Height(32)))
            {
                session.Save();
                Application.Quit();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
