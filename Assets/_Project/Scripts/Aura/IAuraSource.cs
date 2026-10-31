using UnityEngine;

namespace HunterAwakening
{
    /// <summary>What an aura looks like this frame. <see cref="AuraVisual"/> draws it on whoever provides it.</summary>
    public interface IAuraSource
    {
        AuraLook Look { get; }
        Color LookColor { get; }
        /// <summary>0..1: how fully the look shows.</summary>
        float LookStrength { get; }
        /// <summary>How large Ren burns (1 = a beginner's).</summary>
        float RenPresence { get; }
        /// <summary>Raising Ren erupts; true while it is held.</summary>
        bool RenActive { get; }
    }
}
