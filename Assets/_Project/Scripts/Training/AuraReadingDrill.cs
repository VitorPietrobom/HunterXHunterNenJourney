using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Gyo drill: each round one of several stones carries a hidden aura mark that is only visible through Gyo
    /// (hold G). Reach the marked stone before the window closes. Trains Perception and Gyo mastery; Gyo's drain
    /// makes reserve management part of the exercise.
    /// </summary>
    public class AuraReadingDrill : TrainingDrill
    {
        [SerializeField] Transform[] stones;
        [SerializeField] Light markLight;
        [SerializeField] int rounds = 10;
        [SerializeField] float reachRadius = 1.3f;
        [SerializeField] float[] windows = { 6f, 5f, 4f, 3.2f, 2.6f };

        int marked = -1;
        int ignoredStone = -1;
        int done;
        int hits;
        float qualitySum;
        float windowTimer;
        float gyoTime;
        string lastResult = "";

        float Window => windows[Mathf.Clamp(intensityLevel - 1, 0, windows.Length - 1)];
        protected override string IntensityLabel => $"{Window:0.0}s window";
        protected override float MeterHeight => 18f;

        void Awake()
        {
            if (markLight != null) markLight.enabled = false;
        }

        protected override bool CanStart(PlayerContext ctx, out string reason)
        {
            reason = stones == null || stones.Length < 2 ? "Gyo Stones needs at least two stones." : null;
            return reason == null;
        }

        protected override void OnBegin()
        {
            done = hits = 0;
            qualitySum = 0f;
            gyoTime = 0f;
            marked = ignoredStone = -1;
            lastResult = "Hold Gyo (G) to see which stone carries the aura mark, then touch it.";
            NextRound();
        }

        void NextRound()
        {
            // The player is usually still beside the stone they just touched: never mark it (a free point) and
            // don't count it as a wrong touch until they step away.
            ignoredStone = StoneInReach(-1);
            int candidates = 0;
            for (int i = 0; i < stones.Length; i++)
                if (i != marked && i != ignoredStone) candidates++;
            int pick = Random.Range(0, Mathf.Max(1, candidates));
            int next = marked < 0 ? 0 : marked;
            for (int i = 0; i < stones.Length && candidates > 0; i++)
            {
                if (i == marked || i == ignoredStone) continue;
                if (pick-- == 0)
                {
                    next = i;
                    break;
                }
            }
            marked = next;
            windowTimer = Window;
            if (markLight != null) markLight.transform.position = stones[marked].position + Vector3.up * 1.4f;
        }

        protected override void OnTick(float dt)
        {
            bool gyo = player.Aura.GyoActive;
            if (gyo) gyoTime += dt;
            if (markLight != null) markLight.enabled = gyo; // the mark only exists for eyes full of aura
            windowTimer -= dt;

            Vector3 delta = stones[marked].position - player.transform.position;
            delta.y = 0f;
            if (delta.magnitude <= reachRadius)
            {
                float q = 0.5f + 0.5f * Mathf.Clamp01(windowTimer / Window);
                hits++;
                qualitySum += q;
                lastResult = $"Found it! ({HudGui.Pct(q)})";
                Advance();
                return;
            }
            // Touching a wrong stone costs the round.
            if (ignoredStone >= 0 && !InReach(ignoredStone)) ignoredStone = -1;
            int touched = StoneInReach(marked);
            if (touched >= 0 && touched != ignoredStone)
            {
                lastResult = "Wrong stone — read it with Gyo first.";
                Advance();
                return;
            }
            if (windowTimer <= 0f)
            {
                lastResult = "Too slow — the mark faded.";
                Advance();
            }
        }

        bool InReach(int i)
        {
            Vector3 d = stones[i].position - player.transform.position;
            d.y = 0f;
            return d.magnitude <= reachRadius;
        }

        /// <summary>Index of a stone within reach (other than <paramref name="except"/>), or -1.</summary>
        int StoneInReach(int except)
        {
            for (int i = 0; i < stones.Length; i++)
                if (i != except && InReach(i)) return i;
            return -1;
        }

        void Advance()
        {
            done++;
            if (done >= rounds) Finish(false);
            else NextRound();
        }

        protected override void DrawMeter(Rect r)
        {
            HudGui.Bar(r, windowTimer / Window, windowTimer / Window > 0.35f ? HudGui.Good : HudGui.Bad, $"{Mathf.Max(0f, windowTimer):0.0}s");
            // World marker over the marked stone, visible only through Gyo.
            var cam = Camera.main;
            if (cam == null || marked < 0 || !player.Aura.GyoActive) return;
            Vector3 world = stones[marked].position + Vector3.up * 1.8f;
            Vector3 s = cam.WorldToScreenPoint(world);
            if (s.z <= 0f) return;
            GUI.Label(new Rect(s.x - 60, Screen.height - s.y - 12, 120, 24), "<color=#ccddff><b>◆ aura mark ◆</b></color>", HudGui.Center);
        }

        protected override string StatusText => $"Round {Mathf.Min(done + 1, rounds)}/{rounds} · found {hits}\n{lastResult}";

        protected override ExerciseOutcome BuildOutcome(bool aborted)
        {
            double perception = Attr(AttributeId.Perception);
            float comfort = 4.5f / (1f + 0.4f * ProgressionMath.Gain(perception));
            var o = new ExerciseOutcome
            {
                completion = (float)done / rounds,
                quality = done > 0 ? qualitySum / done : 0f,
                intensity = Mathf.Clamp(comfort / Window, 0.3f, 1.4f),
                primaryMetric = done >= rounds ? (float)hits / done * 100f : 0f
            };
            o.metrics.Add($"Found {hits}/{done} marks at a {Window:0.0}s window · Gyo held {gyoTime:0}s");
            if (gyoTime < elapsed * 0.3f) o.metrics.Add("You barely used Gyo — the marks are invisible without it.");
            return o;
        }

        protected override void OnEnd()
        {
            if (markLight != null) markLight.enabled = false;
        }
    }
}
