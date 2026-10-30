using System.Collections.Generic;
using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Draws the aura flowing off the body: a flame shell on a copy of the character's skinned meshes, flames
    /// streaming up from every limb, and a burst when Ren is raised. Ten is a calm, even shroud; Ren roars and burns
    /// larger as Max Output and Ren mastery grow (<see cref="AuraController.RenPresence"/>); Ken is dense and tight;
    /// Gyo shows only at the eyes; a broken Ren sputters out in red.
    /// </summary>
    [RequireComponent(typeof(AuraController))]
    public class AuraVisual : MonoBehaviour
    {
        struct Look
        {
            // Shell
            public float alpha, intensity, thickness, flameHeight, rise, turbulence;
            // Flames streaming off the body
            public float rate, speed, size, life, streakRate;

            public static Look Lerp(Look a, Look b, float t) => new Look
            {
                alpha = Mathf.Lerp(a.alpha, b.alpha, t),
                intensity = Mathf.Lerp(a.intensity, b.intensity, t),
                thickness = Mathf.Lerp(a.thickness, b.thickness, t),
                flameHeight = Mathf.Lerp(a.flameHeight, b.flameHeight, t),
                rise = Mathf.Lerp(a.rise, b.rise, t),
                turbulence = Mathf.Lerp(a.turbulence, b.turbulence, t),
                rate = Mathf.Lerp(a.rate, b.rate, t),
                speed = Mathf.Lerp(a.speed, b.speed, t),
                size = Mathf.Lerp(a.size, b.size, t),
                life = Mathf.Lerp(a.life, b.life, t),
                streakRate = Mathf.Lerp(a.streakRate, b.streakRate, t),
            };
        }

        static readonly Look Ten = new Look { alpha = 0.55f, intensity = 1.1f, thickness = 0.012f, flameHeight = 0.012f, rise = 0.5f, turbulence = 0.25f, rate = 70f, speed = 0.3f, size = 0.06f, life = 0.5f };
        static readonly Look Ren = new Look { alpha = 1f, intensity = 1.8f, thickness = 0.02f, flameHeight = 0.06f, rise = 1.6f, turbulence = 0.7f, rate = 220f, speed = 1.2f, size = 0.1f, life = 0.55f, streakRate = 25f };
        static readonly Look Ken = new Look { alpha = 1f, intensity = 1.7f, thickness = 0.035f, flameHeight = 0.025f, rise = 0.9f, turbulence = 0.35f, rate = 200f, speed = 0.55f, size = 0.09f, life = 0.45f, streakRate = 8f };
        static readonly Look Broken = new Look { alpha = 1f, intensity = 2f, thickness = 0.02f, flameHeight = 0.08f, rise = 3f, turbulence = 1f, rate = 90f, speed = 2f, size = 0.07f, life = 0.3f };

        /// <summary>A stretch of the body that emits aura: from bone a to bone b (or a fixed tip above a), this thick.</summary>
        struct Segment
        {
            public Transform a, b;
            public Vector3 tip;
            public float radius;
            public float weight;
        }

        [SerializeField] float blendIn = 10f;
        [SerializeField] float blendOut = 5f;
        [Tooltip("Smaller skinned meshes (eyes, lashes, brows) get no aura shell: overlapping shells add up and blow out.")]
        [SerializeField] float minShellSize = 0.3f;
        [Tooltip("Denser meshes (inner hair layers) get no shell: skinning them twice costs too much for what shows.")]
        [SerializeField] int maxShellVertices = 60000;

        AuraController aura;
        readonly List<SkinnedMeshRenderer> shells = new List<SkinnedMeshRenderer>();
        readonly List<Segment> segments = new List<Segment>();
        float totalWeight;
        Transform head;
        Material shellMaterial;
        ParticleSystem flames, streaks;
        Look current;
        AuraLook lastLook;
        bool lastRenActive;
        float flameDebt, streakDebt, eyeDebt, flash;
        bool ready;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        static readonly int FlameHeightId = Shader.PropertyToID("_FlameHeight");
        static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");
        static readonly int TurbulenceId = Shader.PropertyToID("_Turbulence");
        static readonly int FlashId = Shader.PropertyToID("_Flash");

        void Awake() => aura = GetComponent<AuraController>();

        void Start()
        {
            var shellShader = Resources.Load<Shader>("Shaders/AuraShell");
            var particleShader = Resources.Load<Shader>("Shaders/AuraParticle");
            if (shellShader == null || particleShader == null)
            {
                Debug.LogWarning("AuraVisual: aura shaders not found in Resources/Shaders; the aura is shown by its light only.", this);
                enabled = false;
                return;
            }
            shellMaterial = new Material(shellShader);
            var particleMaterial = new Material(particleShader);

            var animator = GetComponent<PlayerAnimator>() is PlayerAnimator pa ? pa.Animator : null;
            if (animator != null && animator.isHuman) BuildFromModel(animator);
            else AddSegment(transform, null, transform.position + Vector3.up * 1.6f, 0.3f); // capsule fallback

            flames = CreateSystem("AuraFlames", particleMaterial, false);
            streaks = CreateSystem("AuraStreaks", particleMaterial, true);
            ready = true;
        }

        void BuildFromModel(Animator animator)
        {
            foreach (var source in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (source.sharedMesh == null || source.sharedMesh.vertexCount > maxShellVertices
                    || source.localBounds.size.magnitude < minShellSize) continue;
                var go = new GameObject(source.name + " (Aura)");
                go.transform.SetParent(source.transform.parent, false);
                go.transform.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
                go.transform.localScale = source.transform.localScale;
                var shell = go.AddComponent<SkinnedMeshRenderer>();
                shell.sharedMesh = source.sharedMesh;
                shell.bones = source.bones;
                shell.rootBone = source.rootBone;
                var bounds = source.localBounds;
                bounds.Expand(1f); // flame tongues reach past the body
                shell.localBounds = bounds;
                var materials = new Material[source.sharedMesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++) materials[i] = shellMaterial;
                shell.sharedMaterials = materials;
                shell.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                shell.receiveShadows = false;
                shell.enabled = false;
                shells.Add(shell);
            }

            Transform B(HumanBodyBones bone) => animator.GetBoneTransform(bone);
            head = B(HumanBodyBones.Head);
            Transform chest = B(HumanBodyBones.UpperChest) ?? B(HumanBodyBones.Chest);
            AddSegment(B(HumanBodyBones.Hips), B(HumanBodyBones.Spine), Vector3.zero, 0.13f);
            AddSegment(B(HumanBodyBones.Spine), chest, Vector3.zero, 0.14f);
            AddSegment(chest, B(HumanBodyBones.Neck), Vector3.zero, 0.15f);
            AddSegment(head, null, Vector3.up * 0.22f, 0.11f);
            foreach (var (upper, lower, end, radius) in new[]
                     {
                         (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.05f),
                         (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.05f),
                         (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.07f),
                         (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.07f),
                     })
            {
                AddSegment(B(upper), B(lower), Vector3.zero, radius);
                AddSegment(B(lower), B(end), Vector3.zero, radius * 0.85f);
            }
        }

        /// <param name="tip">Used when <paramref name="b"/> is null: an offset from a (world axes).</param>
        void AddSegment(Transform a, Transform b, Vector3 tip, float radius)
        {
            if (a == null) return;
            Vector3 end = b != null ? b.position : a.position + tip;
            float weight = (Vector3.Distance(a.position, end) + radius) * radius;
            segments.Add(new Segment { a = a, b = b, tip = tip, radius = radius, weight = weight });
            totalWeight += weight;
        }

        ParticleSystem CreateSystem(string systemName, Material material, bool stretched)
        {
            var go = new GameObject(systemName);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // flames trail behind a moving body
            main.maxParticles = 2000;
            main.startSpeed = 0f;
            main.gravityModifier = stretched ? -0.3f : -0.25f; // aura rises
            var emission = ps.emission;
            emission.enabled = false; // emitted from the body by script
            var shape = ps.shape;
            shape.enabled = false;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = stretched ? 0.4f : 0.7f;
            noise.frequency = 1.4f;
            noise.scrollSpeed = 0.8f;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 1f;
            // Stretched along their motion, rising particles read as tongues of flame rather than glowing orbs.
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = stretched ? 0.15f : 0.12f;
            renderer.lengthScale = stretched ? 1.6f : 1.3f;
            ps.Play();
            return ps;
        }

        void LateUpdate()
        {
            if (!ready) return;
            float dt = Time.deltaTime;
            AuraLook look = aura.Look;
            float presence = aura.RenPresence;

            // Raising Ren erupts; a broken Ren bursts apart.
            if (aura.RenActive && !lastRenActive) Erupt(presence);
            if (look == AuraLook.Broken && lastLook != AuraLook.Broken) Shatter();
            lastRenActive = aura.RenActive;
            lastLook = look;

            Look target = TargetLook(look, presence, aura.LookStrength);
            float blend = 1f - Mathf.Exp(-(target.alpha > current.alpha ? blendIn : blendOut) * dt);
            current = Look.Lerp(current, target, blend);
            flash = Mathf.MoveTowards(flash, 0f, dt * 3f);

            Color color = aura.LookColor;
            bool visible = current.alpha > 0.01f || flash > 0f;
            foreach (var shell in shells) shell.enabled = visible;
            if (visible)
            {
                // A broken Ren sputters: the shell flickers as it dies.
                float sputter = look == AuraLook.Broken ? (Random.value < 0.5f ? 0.3f : 1f) : 1f;
                shellMaterial.SetColor(ColorId, color);
                shellMaterial.SetFloat(AlphaId, Mathf.Clamp01(current.alpha * sputter + flash * 0.6f));
                shellMaterial.SetFloat(IntensityId, current.intensity);
                shellMaterial.SetFloat(ThicknessId, current.thickness + flash * 0.03f);
                shellMaterial.SetFloat(FlameHeightId, current.flameHeight);
                shellMaterial.SetFloat(ScrollSpeedId, current.rise);
                shellMaterial.SetFloat(TurbulenceId, current.turbulence);
                shellMaterial.SetFloat(FlashId, flash);
            }

            Color hot = Color.Lerp(color, Color.white, 0.2f);
            hot.a = 0.55f; // many overlapping particles build one body of flame
            EmitOverTime(flames, ref flameDebt, current.rate, dt, current.speed, current.size, current.life, hot, 0.25f);
            EmitOverTime(streaks, ref streakDebt, current.streakRate, dt, current.speed * 1.6f, current.size * 0.6f, current.life * 0.8f, hot, 0.1f);
            if (look == AuraLook.Gyo) EmitEyes(dt, color);
        }

        static Look TargetLook(AuraLook look, float presence, float strength)
        {
            Look l;
            switch (look)
            {
                case AuraLook.Ten: l = Ten; break;
                case AuraLook.Broken: l = Broken; break;
                case AuraLook.Ken:
                    l = Ken;
                    l.thickness *= 0.7f + 0.3f * presence;
                    l.rate *= presence;
                    l.size *= 0.7f + 0.3f * presence;
                    break;
                case AuraLook.Ren:
                    // The flames grow taller, thicker and denser as Ren's presence grows with training.
                    l = Ren;
                    l.thickness *= 0.7f + 0.3f * presence;
                    l.flameHeight *= presence;
                    l.intensity *= 0.8f + 0.2f * presence;
                    l.rate *= presence;
                    l.streakRate *= presence;
                    l.speed *= presence;
                    l.size *= 0.6f + 0.4f * presence;
                    l.life *= 0.8f + 0.2f * presence;
                    break;
                default: return default; // None and Gyo: no body shroud
            }
            l.alpha *= strength;
            l.rate *= strength;
            l.streakRate *= strength;
            return l;
        }

        void EmitOverTime(ParticleSystem ps, ref float debt, float rate, float dt, float speed, float size, float life, Color color, float outward)
        {
            debt += rate * dt;
            int count = (int)debt;
            debt -= count;
            if (count > 0) EmitFromBody(ps, count, speed, size, life, color, outward);
        }

        void EmitFromBody(ParticleSystem ps, int count, float speed, float size, float life, Color color, float outward)
        {
            if (segments.Count == 0) return;
            var p = new ParticleSystem.EmitParams { startColor = color };
            for (int i = 0; i < count; i++)
            {
                SamplePoint(out Vector3 position, out Vector3 normal);
                p.position = position;
                p.velocity = Vector3.up * (speed * Random.Range(0.6f, 1.2f)) + normal * (outward * Random.Range(0.5f, 1f));
                p.startSize = size * Random.Range(0.7f, 1.3f);
                p.startLifetime = life * Random.Range(0.7f, 1.2f);
                ps.Emit(p, 1);
            }
        }

        void SamplePoint(out Vector3 position, out Vector3 normal)
        {
            float pick = Random.value * totalWeight;
            var s = segments[segments.Count - 1];
            foreach (var candidate in segments)
            {
                if (pick < candidate.weight) { s = candidate; break; }
                pick -= candidate.weight;
            }
            Vector3 end = s.b != null ? s.b.position : s.a.position + s.tip;
            normal = Random.onUnitSphere;
            position = Vector3.Lerp(s.a.position, end, Random.value) + normal * s.radius;
        }

        /// <summary>Raising Ren: the aura bursts outward from the body and rolls out along the ground.</summary>
        void Erupt(float presence)
        {
            flash = 1f;
            Color hot = Color.Lerp(aura.LookColor, Color.white, 0.35f);
            EmitFromBody(flames, Mathf.RoundToInt(35 * presence), 0.8f, 0.14f * (0.6f + 0.4f * presence), 0.4f, hot, 3f * presence);
            var p = new ParticleSystem.EmitParams { startColor = hot };
            int ring = Mathf.RoundToInt(28 * presence);
            for (int i = 0; i < ring; i++)
            {
                float angle = i * Mathf.PI * 2f / ring;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                p.position = transform.position + dir * 0.3f + Vector3.up * 0.05f;
                p.velocity = dir * (4f * presence);
                p.startSize = 0.25f;
                p.startLifetime = 0.4f;
                streaks.Emit(p, 1);
            }
            HitFeedback.Shake(0.08f * presence);
        }

        /// <summary>A broken Ren: red aura scatters off the body.</summary>
        void Shatter() => EmitFromBody(streaks, 40, 0.5f, 0.1f, 0.35f, aura.LookColor, 3.5f);

        /// <summary>Gyo: aura gathers in the eyes.</summary>
        void EmitEyes(float dt, Color color)
        {
            if (head == null) return;
            eyeDebt += 30f * dt;
            int count = (int)eyeDebt;
            eyeDebt -= count;
            var p = new ParticleSystem.EmitParams { startColor = Color.Lerp(color, Color.white, 0.4f) };
            for (int i = 0; i < count; i++)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                p.position = head.position + transform.forward * 0.09f + Vector3.up * 0.06f + transform.right * (0.035f * side);
                p.velocity = Vector3.up * 0.15f + transform.forward * 0.05f;
                p.startSize = 0.05f;
                p.startLifetime = 0.35f;
                flames.Emit(p, 1);
            }
        }
    }
}
