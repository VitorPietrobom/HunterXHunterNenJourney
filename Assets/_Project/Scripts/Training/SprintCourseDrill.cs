using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Speed/Endurance drill: run N laps through checkpoints in order. Quality is the share of session time
    /// spent actually sprinting, so walking or idling is scored, not rewarded.
    /// </summary>
    public class SprintCourseDrill : TrainingDrill
    {
        [SerializeField] Transform[] checkpoints;
        [SerializeField] float checkpointRadius = 2.4f;
        [SerializeField] float secondsPerLapLimit = 45f;
        [SerializeField] Color idleColor = new Color(0.5f, 0.5f, 0.55f);
        [SerializeField] Color nextColor = new Color(1f, 0.85f, 0.2f);

        Renderer[] renderers;
        int nextIndex;
        int passed;
        float sprintTime;
        float completedTime;

        int Laps => intensityLevel;
        int TotalCheckpoints => Laps * checkpoints.Length;
        protected override string IntensityLabel => $"{Laps} lap{(Laps > 1 ? "s" : "")}";

        void Awake()
        {
            renderers = new Renderer[checkpoints.Length];
            for (int i = 0; i < checkpoints.Length; i++) renderers[i] = checkpoints[i].GetComponentInChildren<Renderer>();
            Highlight(-1);
        }

        protected override bool CanStart(PlayerContext ctx, out string reason)
        {
            reason = checkpoints == null || checkpoints.Length == 0 ? "Sprint course has no checkpoints." : null;
            return reason == null;
        }

        protected override void OnBegin()
        {
            nextIndex = 0;
            passed = 0;
            sprintTime = 0f;
            completedTime = 0f;
            maxSessionSeconds = Laps * secondsPerLapLimit;
            Highlight(nextIndex);
        }

        protected override void OnTick(float dt)
        {
            if (player.Motor.IsSprinting) sprintTime += dt;

            Vector3 delta = checkpoints[nextIndex].position - player.transform.position;
            delta.y = 0f;
            if (delta.magnitude > checkpointRadius) return;

            passed++;
            nextIndex = (nextIndex + 1) % checkpoints.Length;
            if (passed >= TotalCheckpoints)
            {
                completedTime = elapsed;
                Finish(false);
                return;
            }
            Highlight(nextIndex);
        }

        void Highlight(int index)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].material.color = i == index ? nextColor : idleColor;
        }

        protected override string StatusText
        {
            get
            {
                int lap = Mathf.Min(passed / checkpoints.Length + 1, Laps);
                float sprintShare = elapsed > 0f ? sprintTime / elapsed : 0f;
                return $"Lap {lap}/{Laps} · checkpoint {passed % checkpoints.Length + 1}/{checkpoints.Length} · {elapsed:0.0}s\n" +
                       $"Sprinting {HudGui.Pct(sprintShare)} of the time (hold Shift). Follow the yellow post.";
            }
        }

        protected override ExerciseOutcome BuildOutcome(bool aborted)
        {
            float total = Mathf.Max(1, TotalCheckpoints);
            var o = new ExerciseOutcome
            {
                completion = passed / total,
                quality = elapsed > 0f ? Mathf.Clamp01(sprintTime / elapsed) : 0f,
                intensity = Mathf.Clamp(0.45f + 0.55f * Laps / StatEffects.ComfortableLaps(Attr(AttributeId.Endurance)), 0.3f, 1.4f),
                primaryMetric = completedTime > 0f ? completedTime / Laps : 0f
            };
            o.metrics.Add($"Checkpoints {passed}/{TotalCheckpoints} in {elapsed:0.0}s");
            o.metrics.Add($"Sprinting {HudGui.Pct(o.quality)} of the session");
            if (completedTime > 0f) o.metrics.Add($"Average lap {completedTime / Laps:0.0}s");
            return o;
        }

        protected override void OnEnd() => Highlight(-1);
    }
}
