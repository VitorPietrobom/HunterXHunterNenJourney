using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Caps rendering at a fixed target before the first scene loads. VSync is disabled so the cap,
    /// not the monitor refresh rate, decides the frame rate.
    /// </summary>
    public static class FrameRateSettings
    {
        public const int TargetFrameRate = 144;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
        }
    }
}
