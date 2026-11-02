using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Player-facing options, persisted per machine with PlayerPrefs (a display/input convenience, not progress —
    /// progress lives in the save file). Applied once before the first scene loads and again whenever changed.
    /// </summary>
    public static class GameSettings
    {
        public static readonly int[] FrameRateOptions = { 60, 120, 144, 240, -1 };

        public static float MouseSensitivity { get; private set; } = 1f;
        public static bool InvertY { get; private set; }
        public static int FrameRateCap { get; private set; } = FrameRateSettings.TargetFrameRate;
        public static bool VSync { get; private set; }
        public static bool ShowFps { get; private set; } = true;
        public static bool CameraShake { get; private set; } = true;
        public static bool HitStop { get; private set; } = true;
        public static float SfxVolume { get; private set; } = 0.8f;
        public static float MusicVolume { get; private set; } = 0.7f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat("ha.sensitivity", 1f), 0.2f, 3f);
            InvertY = PlayerPrefs.GetInt("ha.invertY", 0) == 1;
            FrameRateCap = PlayerPrefs.GetInt("ha.fpsCap", FrameRateSettings.TargetFrameRate);
            VSync = PlayerPrefs.GetInt("ha.vsync", 0) == 1;
            ShowFps = PlayerPrefs.GetInt("ha.showFps", 1) == 1;
            CameraShake = PlayerPrefs.GetInt("ha.shake", 1) == 1;
            HitStop = PlayerPrefs.GetInt("ha.hitstop", 1) == 1;
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("ha.sfx", 0.8f));
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("ha.music", 0.7f));
            Apply();
        }

        public static void Set(float sensitivity, bool invertY, int fpsCap, bool vsync, bool showFps, bool shake, bool hitStop, float sfxVolume, float musicVolume)
        {
            MouseSensitivity = Mathf.Clamp(sensitivity, 0.2f, 3f);
            InvertY = invertY;
            FrameRateCap = fpsCap;
            VSync = vsync;
            ShowFps = showFps;
            CameraShake = shake;
            HitStop = hitStop;
            SfxVolume = Mathf.Clamp01(sfxVolume);
            MusicVolume = Mathf.Clamp01(musicVolume);
            PlayerPrefs.SetFloat("ha.sensitivity", MouseSensitivity);
            PlayerPrefs.SetInt("ha.invertY", InvertY ? 1 : 0);
            PlayerPrefs.SetInt("ha.fpsCap", FrameRateCap);
            PlayerPrefs.SetInt("ha.vsync", VSync ? 1 : 0);
            PlayerPrefs.SetInt("ha.showFps", ShowFps ? 1 : 0);
            PlayerPrefs.SetInt("ha.shake", CameraShake ? 1 : 0);
            PlayerPrefs.SetInt("ha.hitstop", HitStop ? 1 : 0);
            PlayerPrefs.SetFloat("ha.sfx", SfxVolume);
            PlayerPrefs.SetFloat("ha.music", MusicVolume);
            PlayerPrefs.Save();
            Apply();
        }

        static void Apply()
        {
            ThirdPersonCamera.SensitivityScale = MouseSensitivity;
            ThirdPersonCamera.InvertY = InvertY;
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : FrameRateCap;
        }

        public static string FrameRateLabel(int cap) => cap < 0 ? "Unlimited" : cap.ToString();
    }
}
