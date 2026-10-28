using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Coordination/Mobility drill: reach the lit pad before its window closes. Some pads are raised and need a jump.
    /// Faster arrivals score higher; misses score zero.
    /// </summary>
    public class PrecisionCourseDrill : TrainingDrill
    {
        [SerializeField] Transform[] pads;
        [SerializeField] int targetsPerSession = 12;
        [SerializeField] float padRadius = 0.8f;
        [SerializeField] float[] windows = { 3.2f, 2.6f, 2.1f, 1.7f, 1.4f, 1.15f, 0.95f };
        [SerializeField] Color idleColor = new Color(0.35f, 0.4f, 0.5f);
        [SerializeField] Color activeColor = new Color(0.2f, 0.9f, 1f);

        Renderer[] renderers;
        int current = -1;
        int done;
        int hits;
        float qualitySum;
        float windowTimer;
        string lastResult = "";

        float Window => windows[Mathf.Clamp(intensityLevel - 1, 0, windows.Length - 1)];
        protected override string IntensityLabel => $"{Window:0.00}s window";

        void Awake()
        {
            renderers = new Renderer[pads.Length];
            for (int i = 0; i < pads.Length; i++) renderers[i] = pads[i].GetComponent<Renderer>();
            Highlight(-1);
        }

        protected override bool CanStart(PlayerContext ctx, out string reason)
        {
            reason = pads == null || pads.Length < 2 ? "Precision course needs at least two pads." : null;
            return reason == null;
        }

        protected override void OnBegin()
        {
            done = 0;
            hits = 0;
            qualitySum = 0f;
            current = -1;
            lastResult = "Step onto the glowing pad. Raised pads need a jump.";
            NextTarget();
        }

        void NextTarget()
        {
            int next;
            do next = Random.Range(0, pads.Length); while (next == current);
            current = next;
            windowTimer = Window;
            Highlight(current);
        }

        protected override void OnTick(float dt)
        {
            windowTimer -= dt;
            Transform pad = pads[current];
            Vector3 feet = player.Motor.FeetPosition;
            Vector3 delta = pad.position - feet;
            float padTop = pad.position.y + pad.lossyScale.y; // Unity cylinders are 2 units tall
            bool onPad = new Vector2(delta.x, delta.z).magnitude <= padRadius && player.Motor.IsGrounded && Mathf.Abs(feet.y - padTop) < 0.35f;

            if (onPad)
            {
                float q = 0.55f + 0.45f * Mathf.Clamp01(windowTimer / Window);
                hits++;
                qualitySum += q;
                lastResult = $"Hit! ({HudGui.Pct(q)})";
                Advance();
            }
            else if (windowTimer <= 0f)
            {
                lastResult = "Missed — the window closed.";
                Advance();
            }
        }

        void Advance()
        {
            done++;
            if (done >= targetsPerSession) Finish(false);
            else NextTarget();
        }

        void Highlight(int index)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].material.color = i == index ? activeColor : idleColor;
        }

        protected override float MeterHeight => 18f;

        protected override void DrawMeter(Rect r) =>
            HudGui.Bar(r, windowTimer / Window, windowTimer / Window > 0.35f ? HudGui.Good : HudGui.Bad, $"{Mathf.Max(0f, windowTimer):0.00}s");

        protected override string StatusText => $"Target {Mathf.Min(done + 1, targetsPerSession)}/{targetsPerSession} · hits {hits}\n{lastResult}";

        protected override ExerciseOutcome BuildOutcome(bool aborted)
        {
            float comfort = StatEffects.ComfortWindow(Attr(AttributeId.Coordination), Attr(AttributeId.Speed));
            var o = new ExerciseOutcome
            {
                completion = (float)done / targetsPerSession,
                quality = done > 0 ? qualitySum / done : 0f,
                intensity = Mathf.Clamp(comfort / Window, 0.3f, 1.4f),
                primaryMetric = done >= targetsPerSession ? (float)hits / done * 100f : 0f
            };
            o.metrics.Add($"Hits {hits}/{done} at a {Window:0.00}s window (your comfortable reach: {comfort:0.00}s)");
            o.metrics.Add($"Average arrival quality {HudGui.Pct(o.quality)}");
            return o;
        }

        protected override void OnEnd() => Highlight(-1);
    }
}
