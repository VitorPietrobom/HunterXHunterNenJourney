using UnityEngine;

namespace HunterAwakening
{
    /// <summary>
    /// Jajanken Paper: a ball of gathered aura that flies straight and bursts on the first opponent in its path.
    /// It sweeps its path each frame (no physics), and hands the hit back to the player's combat to resolve.
    /// </summary>
    public class AuraShot : MonoBehaviour
    {
        float speed = 20f;
        const float Radius = 0.45f;
        static readonly Color PaperColor = new Color(1f, 0.75f, 0.35f);

        PlayerCombat owner;
        /// <summary>Set for Hatsu shots: called instead of the Paper hit (target is null when it falls short).</summary>
        System.Action<EnemyController, Vector3> landed;
        Color Core = PaperColor;
        HitInfo hit;
        float charge, range, travelled;
        Vector3 direction;
        ParticleSystem trail;
        Light glow;
        float emitDebt;

        /// <summary>A Hatsu's aura shot: its own colour, size and speed, and its own handler for what it hits.</summary>
        public static void LaunchCustom(PlayerCombat owner, Vector3 origin, Vector3 direction, Color color, float size, float speed,
            float range, System.Action<EnemyController, Vector3> onLand)
        {
            Launch(owner, origin, direction, default, Mathf.Clamp01(size), range, color, Mathf.Clamp(speed, 4f, 60f), onLand);
        }

        public static void Launch(PlayerCombat owner, Vector3 origin, Vector3 direction, HitInfo hit, float charge, float range)
            => Launch(owner, origin, direction, hit, charge, range, PaperColor, 20f, null);

        static void Launch(PlayerCombat owner, Vector3 origin, Vector3 direction, HitInfo hit, float charge, float range,
            Color color, float speed, System.Action<EnemyController, Vector3> onLand)
        {
            var go = new GameObject("Paper (aura shot)");
            go.transform.position = origin;
            var shot = go.AddComponent<AuraShot>();
            shot.owner = owner;
            shot.hit = hit;
            shot.charge = charge;
            shot.range = range;
            shot.Core = color;
            shot.speed = speed;
            shot.landed = onLand;
            direction.y = 0f;
            shot.direction = direction.sqrMagnitude > 0.001f ? direction.normalized : owner.transform.forward;
            shot.Build();
        }

        void Build()
        {
            var shader = Resources.Load<Shader>("Shaders/AuraParticle");
            trail = new GameObject("Trail").AddComponent<ParticleSystem>();
            trail.transform.SetParent(transform, false);
            trail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = trail.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.maxParticles = 600;
            var emission = trail.emission;
            emission.enabled = false;
            var shape = trail.shape;
            shape.enabled = false;
            var size = trail.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var renderer = trail.GetComponent<ParticleSystemRenderer>();
            if (shader != null) renderer.sharedMaterial = new Material(shader);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.Play();

            glow = gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = Core;
            glow.range = 3f + 2f * charge;
            glow.intensity = 3f + 3f * charge;
        }

        void Update()
        {
            float step = speed * Time.deltaTime;
            Vector3 from = transform.position;
            Vector3 to = from + direction * step;

            // First opponent whose body the segment passes through.
            EnemyController struck = null;
            float best = float.MaxValue;
            foreach (var e in EnemyController.All)
            {
                if (!e.IsTargetable) continue;
                Vector3 center = e.transform.position;
                center.y = from.y;
                Vector3 seg = to - from;
                float t = Mathf.Clamp01(Vector3.Dot(center - from, seg) / Mathf.Max(seg.sqrMagnitude, 1e-6f));
                Vector3 closest = from + seg * t;
                if ((closest - center).magnitude > e.Radius + Radius) continue;
                float along = t * step;
                if (along < best)
                {
                    best = along;
                    struck = e;
                }
            }
            if (struck != null)
            {
                Vector3 point = from + direction * best;
                if (landed != null) landed(struck, point); else owner.OnPaperLanded(struck, hit, charge, point);
                Burst(point);
                Finish();
                return;
            }

            transform.position = to;
            travelled += step;
            EmitTrail(Time.deltaTime);
            if (travelled >= range)
            {
                if (landed != null) landed(null, to); else owner.OnPaperLanded(null, hit, charge, to);
                Burst(to);
                Finish();
            }
        }

        void EmitTrail(float dt)
        {
            emitDebt += (120f + 120f * charge) * dt;
            int count = (int)emitDebt;
            emitDebt -= count;
            var p = new ParticleSystem.EmitParams();
            float ball = 0.35f + 0.35f * charge;
            for (int i = 0; i < count; i++)
            {
                // A bright core at the head and a fading wake behind it.
                bool head = i % 3 == 0;
                p.position = transform.position + Random.insideUnitSphere * (head ? 0.05f : ball * 0.4f);
                p.velocity = -direction * Random.Range(0.5f, 2f) + Random.insideUnitSphere * 0.4f;
                p.startSize = head ? ball : ball * Random.Range(0.3f, 0.6f);
                p.startLifetime = head ? 0.08f : Random.Range(0.2f, 0.4f);
                p.startColor = head ? Color.Lerp(Core, Color.white, 0.5f) : Core;
                trail.Emit(p, 1);
            }
        }

        void Burst(Vector3 at)
        {
            var p = new ParticleSystem.EmitParams { startColor = Core };
            for (int i = 0; i < 50; i++)
            {
                p.position = at;
                p.velocity = Random.onUnitSphere * Random.Range(2f, 6f);
                p.startSize = Random.Range(0.1f, 0.3f);
                p.startLifetime = Random.Range(0.2f, 0.45f);
                trail.Emit(p, 1);
            }
        }

        /// <summary>Stops moving but lets the trail and burst fade before cleaning up.</summary>
        void Finish()
        {
            enabled = false;
            glow.enabled = false;
            Destroy(gameObject, 0.6f);
        }
    }
}
