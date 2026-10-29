using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Strength drill: choose a load, then time each rep while a form needle sweeps. Load relative to the
    /// estimated one-rep max sets intensity; hitting the form zone sets execution quality.
    /// </summary>
    public class LiftDrill : TrainingDrill
    {
        [SerializeField] Transform barbell;
        [SerializeField] Transform standPoint;
        [SerializeField] int targetReps = 8;
        [SerializeField] float repDuration = 1f;
        [SerializeField] float[] loadsKg = { 20f, 35f, 50f, 65f, 80f, 100f, 120f, 150f, 185f, 225f };

        float needle;
        float needleDirection = 1f;
        float repTimer;
        int reps;
        float qualitySum;
        int repCapacity;
        float load;
        float relativeLoad;
        string lastRep = "";
        bool failedRep;
        Vector3 barRest;

        float Load => loadsKg[Mathf.Clamp(intensityLevel - 1, 0, loadsKg.Length - 1)];
        protected override string IntensityLabel => $"{Load:0} kg";
        protected override float MeterHeight => 46f;

        void Start()
        {
            if (barbell != null) barRest = barbell.localPosition;
        }

        protected override void OnBegin()
        {
            player.Motor.SetLocked(true);
            player.SetPose(PlayerPose.Lift);
            if (standPoint != null) player.Motor.Teleport(standPoint.position, standPoint.eulerAngles.y);

            load = Load;
            float oneRepMax = StatEffects.OneRepMaxKg(Attr(AttributeId.Strength));
            relativeLoad = load / oneRepMax;
            // Above ~75% of 1RM the number of reps you can grind out drops quickly; fatigue lowers it further.
            float capacity = relativeLoad <= 0.75f ? targetReps + 4 : Mathf.Lerp(12f, 0f, (relativeLoad - 0.75f) / 0.5f);
            repCapacity = Mathf.FloorToInt(capacity * Mathf.Lerp(0.6f, 1f, Progression.Fatigue.Readiness));

            needle = 0f;
            needleDirection = 1f;
            reps = 0;
            qualitySum = 0f;
            repTimer = 0f;
            failedRep = false;
            lastRep = "Press [Space / LMB] when the needle is in the green zone.";
        }

        float ZoneHalfWidth
        {
            get
            {
                float mastery = Progression.GetMastery(TechniqueId.Lift);
                float masteryBonus = 1f + Mathf.Min(mastery, 100f) / 100f * 0.6f;
                float heavyPenalty = Mathf.Lerp(1.2f, 0.6f, Mathf.Clamp01(relativeLoad));
                if (mastery >= MasteryMilestones.LiftWideForm) heavyPenalty = Mathf.Lerp(heavyPenalty, 1f, 0.35f);
                float breakthrough = Progression.HasBreakthrough(Breakthroughs.PerfectForm) ? 1.2f : 1f;
                return 0.12f * masteryBonus * heavyPenalty * breakthrough * Mathf.Lerp(0.7f, 1f, Progression.Fatigue.Readiness);
            }
        }

        protected override void OnTick(float dt)
        {
            float speed = 0.9f + 1.2f * Mathf.Clamp(relativeLoad, 0f, 1.5f);
            needle += needleDirection * speed * dt;
            if (needle >= 1f) { needle = 1f; needleDirection = -1f; }
            if (needle <= 0f) { needle = 0f; needleDirection = 1f; }

            if (repTimer > 0f)
            {
                repTimer -= dt;
                AnimateBar(1f - Mathf.Abs(repTimer / repDuration * 2f - 1f));
                return;
            }
            AnimateBar(0f);

            if (!player.Input.DrillPrimaryPressed) return;

            if (reps >= repCapacity)
            {
                failedRep = true;
                Finish(false, reps == 0
                    ? $"Failed the first rep — {load:0} kg is beyond what you can move right now. Lower the load ([)."
                    : $"Failed rep {reps + 1}: the load exceeded your remaining capacity.");
                return;
            }

            float half = ZoneHalfWidth;
            float distance = Mathf.Abs(needle - 0.5f);
            float q = distance <= half
                ? Mathf.Lerp(1f, 0.7f, distance / half)
                : Mathf.Clamp(0.35f - (distance - half), 0.05f, 0.35f);
            reps++;
            qualitySum += q;
            repTimer = repDuration;
            player.PlayRep(repDuration);
            lastRep = q >= 0.7f ? $"Rep {reps}: clean ({HudGui.Pct(q)})" : $"Rep {reps}: poor form ({HudGui.Pct(q)}) — time it in the green";
            if (reps >= targetReps) Finish(false);
        }

        void AnimateBar(float t)
        {
            if (barbell != null) barbell.localPosition = barRest + Vector3.up * (0.6f * t);
        }

        protected override string StatusText =>
            $"Reps {reps}/{targetReps} · load {HudGui.Pct(relativeLoad)} of est. 1RM\n{lastRep}";

        protected override void DrawMeter(Rect r)
        {
            HudGui.Fill(r, new Color(0, 0, 0, 0.6f));
            float half = ZoneHalfWidth;
            HudGui.Fill(new Rect(r.x + r.width * (0.5f - half), r.y, r.width * half * 2f, r.height), new Color(0.2f, 0.7f, 0.3f, 0.8f));
            HudGui.Fill(new Rect(r.x + r.width * needle - 3f, r.y - 4f, 6f, r.height + 8f), Color.white);
        }

        protected override ExerciseOutcome BuildOutcome(bool aborted)
        {
            float oneRepMax = StatEffects.OneRepMaxKg(Attr(AttributeId.Strength));
            var o = new ExerciseOutcome
            {
                completion = (float)reps / targetReps,
                quality = reps > 0 ? qualitySum / reps : 0f,
                intensity = Mathf.Clamp(relativeLoad / 0.8f, 0f, 1.4f),
                primaryMetric = load,
                // Failing under the bar strains the upper body, more so the further past the 1RM.
                strain = failedRep ? 15f + 60f * Mathf.Max(0f, relativeLoad - 1f) : 0f
            };
            o.metrics.Add($"Load {load:0} kg = {HudGui.Pct(relativeLoad)} of est. 1RM ({oneRepMax:0} kg)");
            o.metrics.Add($"Reps {reps}/{targetReps}{(failedRep ? " (failed rep)" : "")}");
            o.metrics.Add($"Form quality {HudGui.Pct(o.quality)}");
            return o;
        }

        protected override void OnEnd()
        {
            AnimateBar(0f);
            if (player == null) return;
            player.Motor.SetLocked(false);
            player.SetPose(PlayerPose.None);
        }
    }
}
