namespace HunterAwakening
{
    /// <summary>
    /// Default frame-rate target. Applied (with VSync off) before the first scene loads by <see cref="GameSettings"/>,
    /// which also lets the player change it from the pause menu.
    /// </summary>
    public static class FrameRateSettings
    {
        public const int TargetFrameRate = 144;
    }
}
