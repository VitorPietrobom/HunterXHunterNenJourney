using System.Collections.Generic;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// A white impact frame on a struck fighter's toon materials. The flash rides a property block set only for its
    /// few frames, so the renderers batch normally the rest of the time. Purely cosmetic.
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        static readonly int FlashId = Shader.PropertyToID("_HitFlash");

        readonly List<Renderer> flashing = new List<Renderer>();
        MaterialPropertyBlock block;
        float until;

        public void Flash(float seconds = 0.09f)
        {
            block ??= new MaterialPropertyBlock();
            Clear();
            // Collected per flash: rival tiers swap models, and aura shells (another shader) are left alone.
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (r.name.EndsWith("(Aura)")) continue;
                block.SetFloat(FlashId, 1f);
                r.SetPropertyBlock(block);
                flashing.Add(r);
            }
            until = Time.unscaledTime + seconds; // hit-stop freezes scaled time
        }

        void LateUpdate()
        {
            if (flashing.Count > 0 && Time.unscaledTime >= until) Clear();
        }

        void OnDisable() => Clear();

        void Clear()
        {
            foreach (var r in flashing)
                if (r != null) r.SetPropertyBlock(null);
            flashing.Clear();
        }
    }
}
