using System;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Impact particles and synthesised combat sounds (generated at startup, so no audio assets are needed).
    /// Static entry points are safe to call when no instance exists. Purely cosmetic.
    /// </summary>
    public class ImpactEffects : MonoBehaviour
    {
        const int SampleRate = 44100;

        [Tooltip("Particle material (URP Particles/Unlit). Assigned by the scene builder; falls back to finding the shader.")]
        [SerializeField] Material particleMaterial;
        [SerializeField] int voices = 8;

        static ImpactEffects instance;
        ParticleSystem sparks;
        AudioSource[] sources;
        int nextSource;
        AudioClip thud, crack, clang, block, whoosh, hurt, boom, surge, drum;

        static readonly Color HitColor = new Color(1f, 0.9f, 0.6f);
        static readonly Color RenColor = new Color(1f, 0.55f, 0.15f);
        static readonly Color GuardColor = new Color(0.45f, 0.65f, 1f);
        static readonly Color ParryColor = new Color(0.8f, 0.95f, 1f);
        static readonly Color HurtColor = new Color(0.9f, 0.2f, 0.2f);
        static readonly Color KoColor = new Color(1f, 0.35f, 0.1f);

        void Awake()
        {
            instance = this;
            sparks = CreateSparks();
            sources = new AudioSource[Mathf.Max(1, voices)];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
            }
            var rng = new System.Random(1234);
            thud = Synth("Thud", 0.22f, t => Sweep(t, 150f, 55f, 0.22f) * Mathf.Exp(-t * 16f) + Noise(rng) * 0.35f * Mathf.Exp(-t * 70f));
            hurt = Synth("Hurt", 0.25f, t => Sweep(t, 110f, 40f, 0.25f) * Mathf.Exp(-t * 12f) + Noise(rng) * 0.25f * Mathf.Exp(-t * 50f));
            crack = Synth("Crack", 0.45f, t => Noise(rng) * 0.7f * Mathf.Exp(-t * 28f) + Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.8f * Mathf.Exp(-t * 7f));
            block = Synth("Block", 0.15f, t => Mathf.Sin(2f * Mathf.PI * 230f * t) * 0.6f * Mathf.Exp(-t * 30f) + Noise(rng) * 0.3f * Mathf.Exp(-t * 90f));
            clang = Synth("Clang", 0.8f, t => (Mathf.Sin(2f * Mathf.PI * 880f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 1331f * t)
                                              + 0.4f * Mathf.Sin(2f * Mathf.PI * 2417f * t)) * 0.3f * Mathf.Exp(-t * 6f));
            boom = Synth("Boom", 0.9f, t => Sweep(t, 90f, 30f, 0.9f) * Mathf.Exp(-t * 4f) + Noise(rng) * 0.6f * Mathf.Exp(-t * 18f));
            // Ren rising: a hum that climbs and swells, with breathy noise underneath.
            float surgeLow = 0f;
            surge = Synth("Surge", 0.7f, t =>
            {
                surgeLow += (Noise(rng) - surgeLow) * 0.05f;
                float swell = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.7f)) * Mathf.Clamp01(t / 0.08f);
                return (Sweep(t, 70f, 150f, 0.7f) * 0.6f + 0.35f * Sweep(t, 140f, 300f, 0.7f) + surgeLow * 2.5f) * swell * 0.7f;
            });
            // Jajanken chant beat: a taiko-like drum.
            drum = Synth("Drum", 0.4f, t => Sweep(t, 150f, 58f, 0.4f) * Mathf.Exp(-t * 9f) + Noise(rng) * 0.4f * Mathf.Exp(-t * 120f));
            float low = 0f;
            whoosh = Synth("Whoosh", 0.3f, t =>
            {
                low += (Noise(rng) - low) * 0.08f; // one-pole low-pass: breathy rather than hissy
                return low * 2.2f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.3f));
            });
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ------------------------------------------------------------------ Public API

        public static void Hit(Vector3 at, bool heavy, bool ren)
        {
            Burst(at, ren ? RenColor : HitColor, heavy ? 22 : 12, heavy ? 1.3f : 1f);
            Play(i => i.thud, heavy ? 1f : 0.7f, heavy ? 0.85f : 1.05f);
        }

        public static void GuardBreak(Vector3 at)
        {
            Burst(at, GuardColor, 26, 1.4f);
            Play(i => i.crack, 0.9f, 1.1f);
        }

        public static void Blocked(Vector3 at)
        {
            Burst(at, GuardColor, 8, 0.7f);
            Play(i => i.block, 0.6f, 1f);
        }

        public static void Parry(Vector3 at)
        {
            Burst(at, ParryColor, 30, 1.5f);
            Play(i => i.clang, 0.8f, 1f);
        }

        public static void PerfectDodge(Vector3 at)
        {
            Burst(at, ParryColor, 14, 0.8f);
            Play(i => i.whoosh, 0.7f, 1.3f);
        }

        public static void PlayerHurt(Vector3 at)
        {
            Burst(at, HurtColor, 10, 0.9f);
            Play(i => i.hurt, 0.8f, 1f);
        }

        public static void Ko(Vector3 at)
        {
            Burst(at, KoColor, 60, 2.2f);
            Play(i => i.boom, 1f, 1f);
            Play(i => i.crack, 0.8f, 0.8f);
        }

        /// <summary>Raising Ren: heavier and lower the more presence it has.</summary>
        public static void RenRaise(float presence) =>
            Play(i => i.surge, Mathf.Clamp(0.35f + 0.15f * presence, 0.35f, 0.85f), Mathf.Clamp(1.15f - 0.1f * presence, 0.8f, 1.1f));

        /// <summary>A rival's aura flares as it winds up: a lower, quieter surge that doubles as an audio cue.</summary>
        public static void RivalFlare(float presence) => Play(i => i.surge, 0.3f + 0.08f * presence, 0.75f);

        /// <summary>One beat per word of the Jajanken chant, climbing in pitch toward the release.</summary>
        public static void ChantBeat(int stage) => Play(i => i.drum, 0.55f + 0.15f * stage, 0.9f + 0.12f * stage);

        /// <summary>Rock leaves the fist.</summary>
        public static void RockThrow(float charge)
        {
            Play(i => i.whoosh, 0.6f + 0.4f * charge, 0.7f);
            Play(i => i.surge, 0.5f * charge, 1.4f);
        }

        public static void Swing(bool heavy) => Play(i => i.whoosh, heavy ? 0.35f : 0.2f, heavy ? 0.8f : 1.2f);
        public static void Dodge() => Play(i => i.whoosh, 0.45f, 1f);

        // ------------------------------------------------------------------ Internals

        static void Burst(Vector3 at, Color color, int count, float scale)
        {
            if (instance == null || instance.sparks == null) return;
            var main = instance.sparks.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f * scale, 7f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f * scale, 0.13f * scale);
            var p = new ParticleSystem.EmitParams { position = at, startColor = color, applyShapeToPosition = true };
            instance.sparks.Emit(p, count);
        }

        static void Play(Func<ImpactEffects, AudioClip> pick, float volume, float pitch)
        {
            if (instance == null || GameSettings.SfxVolume <= 0f) return;
            var clip = pick(instance);
            if (clip == null) return;
            var source = instance.sources[instance.nextSource];
            instance.nextSource = (instance.nextSource + 1) % instance.sources.Length;
            source.pitch = pitch * UnityEngine.Random.Range(0.93f, 1.07f);
            source.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        }

        ParticleSystem CreateSparks()
        {
            var go = new GameObject("ImpactSparks");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.gravityModifier = 0.8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true; // sparks keep flying through hit-stop
            main.maxParticles = 400;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (particleMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader != null) particleMaterial = new Material(shader);
            }
            if (particleMaterial != null) renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        static AudioClip Synth(string name, float seconds, Func<float, float> sample)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / SampleRate;
                float fadeOut = Mathf.Clamp01((count - i) / (SampleRate * 0.01f)); // avoid an end click
                data[i] = Mathf.Clamp(sample(t), -1f, 1f) * fadeOut;
            }
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Sine whose frequency glides from <paramref name="from"/> to <paramref name="to"/> over the clip.</summary>
        static float Sweep(float t, float from, float to, float length)
        {
            float k = (to - from) / length;
            return Mathf.Sin(2f * Mathf.PI * (from * t + 0.5f * k * t * t));
        }

        static float Noise(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);
    }
}
