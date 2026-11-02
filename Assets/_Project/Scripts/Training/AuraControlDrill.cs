using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Ten control drill: hold [Space/LMB] to raise aura flow, release to let it settle, and keep it inside the
    /// stable band despite drift and periodic disturbances. Spends real aura and builds fatigue.
    /// </summary>
    public class AuraControlDrill : TrainingDrill
    {
        [SerializeField] Transform standPoint;
        [SerializeField] float[] durations = { 15f, 20f, 25f, 30f, 40f };
        [SerializeField] float bandCenter = 0.55f;

        float flow;
        float inBandTime;
        float pulseTimer;
        float noiseSeed;
        bool depleted;
        string disturbance = "";

        float Duration => durations[Mathf.Clamp(intensityLevel - 1, 0, durations.Length - 1)];
        protected override string IntensityLabel => $"Level {intensityLevel} · {Duration:0}s";
        protected override float MeterHeight => 30f;

        float BandHalf => StatEffects.TenBand(Attr(AttributeId.ControlPrecision)) * (1f + Mathf.Min(Progression.GetMastery(TechniqueId.Ten), 100f) / 200f);

        protected override void OnBegin()
        {
            player.Motor.SetLocked(true);
            player.SetPose(PlayerPose.Meditate);
            if (standPoint != null) player.Motor.Teleport(standPoint.position, standPoint.eulerAngles.y);
            flow = 0.3f;
            inBandTime = 0f;
            depleted = false;
            noiseSeed = Random.value * 100f;
            pulseTimer = NextPulseDelay();
            disturbance = "Hold [Space / LMB] to raise output; release to let it settle.";
        }

        float NextPulseDelay() => Random.Range(2.5f, 4.5f) / (0.6f + 0.2f * intensityLevel);

        protected override void OnTick(float dt)
        {
            float noiseAmp = 0.15f + 0.08f * intensityLevel;
            float noise = (Mathf.PerlinNoise(elapsed * 0.8f + noiseSeed, 0f) - 0.5f) * 2f * noiseAmp;
            float push = player.Input.DrillPrimaryHeld ? 0.55f : -0.4f;
            flow = Mathf.Clamp01(flow + (push + noise) * dt);

            pulseTimer -= dt;
            if (pulseTimer <= 0f)
            {
                float kick = (Random.value < 0.5f ? -1f : 1f) * (0.12f + 0.03f * intensityLevel);
                flow = Mathf.Clamp01(flow + kick);
                disturbance = kick > 0f ? "Disturbance: aura surged!" : "Disturbance: aura flow dropped!";
                pulseTimer = NextPulseDelay();
            }

            if (Mathf.Abs(flow - bandCenter) <= BandHalf) inBandTime += dt;

            float efficiency = StatEffects.Efficiency(Attr(AttributeId.Efficiency));
            float cost = ProgressionMath.AuraCost((4f + 2f * intensityLevel * flow) * StatEffects.AuraDrainScale, dt, efficiency);
            if (!player.Aura.TrySpend(cost))
            {
                depleted = true;
                Finish(true, "Aura depleted — the reserve ran dry before the session ended. Train with a shorter level or rest first.");
                return;
            }
            Progression.AddExertion(0.15f * intensityLevel * dt);

            if (elapsed >= Duration) Finish(false);
        }

        protected override string StatusText
        {
            get
            {
                float stability = elapsed > 0f ? inBandTime / elapsed : 0f;
                return $"Time {elapsed:0.0}/{Duration:0}s · stability {HudGui.Pct(stability)} · aura {player.Aura.Current:0}/{player.Aura.Capacity:0}\n{disturbance}";
            }
        }

        protected override void DrawMeter(Rect r)
        {
            HudGui.Fill(r, new Color(0, 0, 0, 0.6f));
            float half = BandHalf;
            HudGui.Fill(new Rect(r.x + r.width * (bandCenter - half), r.y, r.width * half * 2f, r.height), new Color(0.2f, 0.75f, 0.9f, 0.6f));
            bool inBand = Mathf.Abs(flow - bandCenter) <= half;
            HudGui.Fill(new Rect(r.x + r.width * flow - 4f, r.y - 4f, 8f, r.height + 8f), inBand ? Color.white : HudGui.Bad);
        }

        protected override ExerciseOutcome BuildOutcome(bool aborted)
        {
            float control = ProgressionMath.Gain(Attr(AttributeId.ControlPrecision));
            var o = new ExerciseOutcome
            {
                completion = Mathf.Clamp01(elapsed / Duration),
                quality = elapsed > 0f ? inBandTime / elapsed : 0f,
                intensity = Mathf.Clamp((0.6f + 0.2f * intensityLevel) / (1f + control), 0.3f, 1.4f)
            };
            o.primaryMetric = o.completion >= 0.999f ? o.quality * 100f : 0f;
            o.metrics.Add($"Stable {inBandTime:0.0}s of {elapsed:0.0}s ({HudGui.Pct(o.quality)})");
            if (depleted) o.metrics.Add("Aura reserve depleted");
            return o;
        }

        protected override void OnEnd()
        {
            if (player == null) return;
            player.Motor.SetLocked(false);
            player.SetPose(PlayerPose.None);
        }
    }
}
