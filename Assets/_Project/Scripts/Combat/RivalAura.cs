using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// A rival's visible aura. During a match it wears a calm shroud in its own colour, and flares into Ren for every
    /// wind-up (so the flare doubles as an attack cue); staggered or beaten, it goes out. Stronger tiers burn larger.
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    public class RivalAura : MonoBehaviour, IAuraSource
    {
        [SerializeField] Color defaultColor = new Color(0.9f, 0.3f, 0.35f);

        EnemyController enemy;
        Color color;
        float presence = 1f;
        Light glow;

        public AuraLook Look { get; private set; }
        public Color LookColor => color;
        public float LookStrength { get; private set; }
        public float RenPresence => presence;
        public bool RenActive => Look == AuraLook.Ren;

        void Awake()
        {
            enemy = GetComponent<EnemyController>();
            color = defaultColor;
            if (GetComponent<AuraVisual>() == null) gameObject.AddComponent<AuraVisual>();
            var lightGo = new GameObject("RivalAuraLight");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = Vector3.up * 0.2f;
            glow = lightGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.enabled = false;
        }

        /// <summary>Each fighter's aura colour, and a size from the tier's presence (the Ren output it stands up to).</summary>
        public void SetTier(RivalTier tier)
        {
            color = tier.id switch
            {
                "rookie" => new Color(0.75f, 0.88f, 1f),  // Killua: pale, crackling white-blue
                "rival" => new Color(0.4f, 0.6f, 1f),     // Kurapika: cool blue
                "veteran" => new Color(1f, 0.35f, 0.75f), // Hisoka: pink-violet
                "elite" => new Color(0.55f, 0.2f, 0.95f), // Meruem: deep violet
                _ => defaultColor,
            };
            presence = Mathf.Clamp(1f + (tier.presence - 10f) / 15f, 1f, 3f);
        }

        void Update()
        {
            if (!enemy.IsActive || enemy.IsDefeated || enemy.IsStaggered) Look = AuraLook.None;
            else if (enemy.IsTelegraphing) Look = AuraLook.Ren;
            else Look = AuraLook.Ten;
            LookStrength = 1f;

            glow.enabled = Look != AuraLook.None;
            if (!glow.enabled) return;
            glow.color = color;
            glow.range = 3f * Mathf.Sqrt(presence);
            glow.intensity = (Look == AuraLook.Ren ? 3f * Mathf.Sqrt(presence) : 1.2f) * (0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 8f, 1.7f));
        }
    }
}
