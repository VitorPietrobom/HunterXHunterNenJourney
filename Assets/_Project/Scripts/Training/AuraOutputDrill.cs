using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Ren drill: hold [Space/LMB] to flare aura output and keep it between a target line (which rises during the
    /// session) and the player's control ceiling. Pressure waves knock output down; staying above the ceiling builds
    /// instability, and full instability breaks Ren and ends the session. Trains Max Output and Sustained Output, and
    /// makes the gap to Control Precision visible: if the target climbs past the ceiling, control is the bottleneck.
    /// </summary>
    public class AuraOutputDrill : TrainingDrill
    {
        [SerializeField] Transform standPoint;
        [SerializeField] float[] durations = { 12f, 16f, 20f, 25f, 30f };
        [SerializeField] float[] targets = { 0.45f, 0.55f, 0.62f, 0.7f, 0.78f };
        [SerializeField] float riseRate = 0.7f;
        [SerializeField] float fallRate = 0.5f;
        [SerializeField] float instabilityGain = 0.6f;
        [SerializeField] float instabilityDecay = 0.3f;
        [Tooltip("Share of combat Ren's drain paid while drilling; at starting stats levels 1–3 fit in the reserve, 4–5 need Aura Capacity.")]
        [SerializeField] float auraCostScale = 0.5f;

        float output;
        float instability;
        float peakInstability;
        float inBandTime;
        float pulseTimer;
        float auraSpent;
        bool broke;
        bool depleted;
        string lastEvent = "";

        float Duration => durations[Mathf.Clamp(intensityLevel - 1, 0, durations.Length - 1)];
        float Target => targets[Mathf.Clamp(intensityLevel - 1, 0, targets.Length - 1)];
        /// <summary>The target line starts 0.1 below the level's target and climbs to it over the session.</summary>
        float CurrentTarget => Target - 0.1f * (1f - Mathf.Clamp01(elapsed / Duration));
        float Ceiling => StatEffects.RenControlCeiling(Attr(AttributeId.ControlPrecision))
                         + Mathf.Min(Progression.GetMastery(TechniqueId.Ren), 100f) / 2000f;

        protected override string IntensityLabel => $"Level {intensityLevel} · target {Target * 100:0}% · {Duration:0}s";
        protected override float MeterHeight => 44f;

        protected override void OnBegin()
        {
            player.Motor.SetLocked(true);
            if (standPoint != null) player.Motor.Teleport(standPoint.position, standPoint.eulerAngles.y);
            output = 0.2f;
            instability = 0f;
            peakInstability = 0f;
            inBandTime = 0f;
            auraSpent = 0f;
            broke = false;
            depleted = false;
            pulseTimer = NextPulseDelay();
            lastEvent = "Hold [Space / LMB] to flare Ren above the target line — but stay under your control ceiling.";
        }

        float NextPulseDelay() => Random.Range(2f, 3.5f) / (0.7f + 0.15f * intensityLevel);

        protected override void OnTick(float dt)
        {
            bool flaring = player.Input.DrillPrimaryHeld;
            float noise = (Mathf.PerlinNoise(elapsed * 1.3f, 7.1f) - 0.5f) * 0.25f;
            output = Mathf.Clamp01(output + ((flaring ? riseRate : -fallRate) + noise) * dt);

            pulseTimer -= dt;
            if (pulseTimer <= 0f)
            {
                output = Mathf.Clamp01(output - (0.1f + 0.03f * intensityLevel));
                lastEvent = "Pressure wave! Push your output back up.";
                pulseTimer = NextPulseDelay();
            }

            float ceiling = Ceiling;
            if (output > ceiling) instability += instabilityGain * (1f + (output - ceiling) * 4f) * dt;
            else instability -= instabilityDecay * dt;
            instability = Mathf.Clamp01(instability);
            peakInstability = Mathf.Max(peakInstability, instability);
            if (instability >= 1f)
            {
                broke = true;
                Finish(true, "Ren destabilised — you pushed past your control ceiling for too long. Ease off before the meter fills, or raise Control Precision at the Aura Mat.");
                return;
            }

            if (output >= CurrentTarget && output <= ceiling) inBandTime += dt;

            float efficiency = StatEffects.Efficiency(Attr(AttributeId.Efficiency));
            float cost = ProgressionMath.AuraCost(StatEffects.RenOutput(Attr(AttributeId.MaxOutput)) * output * auraCostScale, dt, efficiency);
            if (!player.Aura.TrySpend(cost))
            {
                depleted = true;
                Finish(true, "Aura depleted — your reserve ran dry. Rest first or pick a shorter level; Aura Capacity and Efficiency extend sessions.");
                return;
            }
            auraSpent += cost;
            player.Aura.DrillGlow = output;
            Progression.AddExertion(StatEffects.RenFatiguePerSecond(Attr(AttributeId.SustainedOutput)) * output * dt);

            if (elapsed >= Duration) Finish(false);
        }

        protected override string StatusText
        {
            get
            {
                float stability = elapsed > 0f ? inBandTime / elapsed : 0f;
                string warning = Ceiling < Target ? "  <color=#ff8855>Target is above your control ceiling!</color>" : "";
                return $"Time {elapsed:0.0}/{Duration:0}s · in band {HudGui.Pct(stability)} · aura {player.Aura.Current:0}/{player.Aura.Capacity:0}{warning}\n{lastEvent}";
            }
        }

        protected override void DrawMeter(Rect r)
        {
            var bar = new Rect(r.x, r.y, r.width, 28f);
            HudGui.Fill(bar, new Color(0, 0, 0, 0.6f));
            float target = CurrentTarget, ceiling = Ceiling;
            if (ceiling > target)
                HudGui.Fill(new Rect(bar.x + bar.width * target, bar.y, bar.width * (ceiling - target), bar.height), new Color(1f, 0.55f, 0.15f, 0.55f));
            HudGui.Fill(new Rect(bar.x + bar.width * target - 1f, bar.y, 2f, bar.height), Color.white);
            HudGui.Fill(new Rect(bar.x + bar.width * ceiling - 1f, bar.y, 3f, bar.height), HudGui.Bad);
            HudGui.Fill(new Rect(bar.x + bar.width * output - 4f, bar.y - 4f, 8f, bar.height + 8f), output > ceiling ? HudGui.Bad : Color.white);
            HudGui.Bar(new Rect(r.x, r.y + 32f, r.width, 12f), instability, instability > 0.6f ? HudGui.Bad : HudGui.Warn, null);
        }

        protected override ExerciseOutcome BuildOutcome(bool aborted)
        {
            var o = new ExerciseOutcome
            {
                completion = Mathf.Clamp01(elapsed / Duration),
                quality = elapsed > 0f ? inBandTime / elapsed : 0f,
                intensity = Mathf.Clamp(Target / StatEffects.RenComfortOutput(Attr(AttributeId.MaxOutput)), 0.3f, 1.4f)
            };
            o.primaryMetric = o.completion >= 0.999f ? o.quality * 100f : 0f;
            o.metrics.Add($"Held Ren in band {inBandTime:0.0}s of {elapsed:0.0}s ({HudGui.Pct(o.quality)})");
            o.metrics.Add($"Target {Target * 100:0}% · control ceiling {Ceiling * 100:0}% · peak instability {HudGui.Pct(peakInstability)}");
            o.metrics.Add($"Aura spent {auraSpent:0}");
            if (broke) o.metrics.Add("Ren broke");
            if (depleted) o.metrics.Add("Aura reserve depleted");
            return o;
        }

        protected override void OnEnd()
        {
            if (player == null) return;
            player.Aura.DrillGlow = 0f;
            player.Motor.SetLocked(false);
        }
    }
}
