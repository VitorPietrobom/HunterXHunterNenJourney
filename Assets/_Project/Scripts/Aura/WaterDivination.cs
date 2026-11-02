using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Water Divination, the Hunter's Nen-type test: a glass filled to the brim with a leaf floating on top. Cup your
    /// hands around it and pour aura in with Ren — the water reacts to your type. Gon's rises and spills over the rim:
    /// an Enhancer. Learning it unlocks the Enhancer's affinity (StatEffects.Enhancer*).
    /// </summary>
    public class WaterDivination : Interactable
    {
        public const string EnhancerInsight = "nen:enhancer";

        [SerializeField] float pourSeconds = 6f;
        [Tooltip("The water column (scaled on Y as it rises) and the leaf riding on top of it.")]
        [SerializeField] Transform water;
        [SerializeField] Transform leaf;
        [SerializeField] float spillHeight = 0.12f;
        [SerializeField] ParticleSystem spill;

        PlayerContext player;
        bool pouring;
        float progress;
        Vector3 waterScale, waterPosition, leafPosition;

        public override bool IsBusy => pouring;

        public void Configure(Transform waterColumn, Transform floatingLeaf, ParticleSystem overflow)
        {
            water = waterColumn;
            leaf = floatingLeaf;
            spill = overflow;
        }

        void Awake()
        {
            if (water != null)
            {
                waterScale = water.localScale;
                waterPosition = water.localPosition;
            }
            if (leaf != null) leafPosition = leaf.localPosition;
        }

        void Start()
        {
            if (Progression.KnowsInsight(EnhancerInsight)) Show(1f);
        }

        public override string GetPrompt()
        {
            if (!Progression.IsAuraAwakened) return "Water Divination — a glass of water and a leaf. Awaken your aura first.";
            if (pouring) return "Hold <b>R</b> (Ren): pour your aura into the glass   [E] step back";
            if (Progression.KnowsInsight(EnhancerInsight)) return "Water Divination — the water overflowed: you are an <b>Enhancer</b>";
            return "[E] Water Divination: learn your Nen type";
        }

        public override void Interact(PlayerContext ctx)
        {
            if (!Progression.IsAuraAwakened)
            {
                Session.PushNotice("Nothing happens. You need to awaken your aura before the water can answer.", 3f);
                return;
            }
            if (pouring)
            {
                Stop();
                return;
            }
            player = ctx;
            pouring = true;
            progress = Progression.KnowsInsight(EnhancerInsight) ? 0f : progress;
            Session.PushNotice("Cup your hands around the glass… now hold Ren (R).", 3f);
        }

        void Update()
        {
            if (!pouring)
            {
                // Without aura the water settles back to the brim.
                if (!Progression.KnowsInsight(EnhancerInsight) && progress > 0f)
                {
                    progress = Mathf.Max(0f, progress - Time.deltaTime * 0.5f);
                    Show(progress / pourSeconds);
                }
                return;
            }
            Vector3 d = player.transform.position - transform.position;
            d.y = 0f;
            if (d.magnitude > interactRadius + 1f)
            {
                Stop();
                return;
            }

            if (player.Aura.RenActive) progress += Time.deltaTime;
            else progress = Mathf.Max(0f, progress - Time.deltaTime * 0.5f);
            float t = Mathf.Clamp01(progress / pourSeconds);
            Show(t);
            if (spill != null)
            {
                var emission = spill.emission;
                emission.rateOverTime = t > 0.75f ? Mathf.Lerp(10f, 60f, (t - 0.75f) / 0.25f) : 0f;
            }
            if (t < 1f) return;

            bool first = Progression.UnlockInsight(EnhancerInsight, "Nen type: Enhancer",
                "The water rose and spilled over the rim. Like Gon, you are an Enhancer: strikes powered by aura hit 10% harder " +
                "and your body heals 50% faster out of combat.", announce: false);
            Session.PushNotice(first
                ? "The water rises and spills over the rim — you are an ENHANCER! Aura-powered strikes +10%, natural healing +50%."
                : "The water spills over again. Enhancer, no doubt about it.", 7f);
            if (first)
            {
                ImpactEffects.RenRaise(2f);
                HitFeedback.SlowMotion(0.4f, 0.8f);
            }
            Stop();
        }

        void Show(float t)
        {
            if (water != null)
            {
                // The column rises past the rim; the leaf rides the surface.
                float rise = spillHeight * t;
                water.localScale = new Vector3(waterScale.x, waterScale.y + rise / 2f, waterScale.z);
                water.localPosition = waterPosition + Vector3.up * (rise / 2f);
            }
            if (leaf != null)
            {
                leaf.localPosition = leafPosition + Vector3.up * (spillHeight * t);
                leaf.localRotation = Quaternion.Euler(0f, Time.time * 20f * t, 3f * Mathf.Sin(Time.time * 3f) * t);
            }
        }

        void Stop()
        {
            pouring = false;
            player = null;
            if (spill != null)
            {
                var emission = spill.emission;
                emission.rateOverTime = 0f;
            }
        }

        protected override void OnDisable()
        {
            if (pouring) Stop();
            base.OnDisable();
        }

        void OnGUI()
        {
            if (!pouring || Session.InputBlocked) return;
            var meter = HudGui.DrillPanel("Water Divination", "Hold Ren (R) and pour your aura into the glass. Watch the water.", 20f);
            HudGui.Bar(meter, Mathf.Clamp01(progress / pourSeconds), new Color(0.35f, 0.65f, 1f),
                player != null && player.Aura.RenActive ? "The water is rising…" : "Hold R");
        }
    }
}
