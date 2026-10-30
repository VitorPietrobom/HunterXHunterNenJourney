using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HunterAwakening.EditorTools
{
    /// <summary>
    /// The sandbox's look: a warm sun over a procedural sky, distance fog, post-processing (filmic tonemapping, colour
    /// grading and bloom so aura glows), generated grass and sand textures, and scenery around the training grounds.
    /// Every asset is generated in code, so a rebuild reproduces the look.
    /// </summary>
    static class SceneLook
    {
        const string Dir = "Assets/_Project/Art/Environment";
        /// <summary>Middle of the stations; scenery stays outside <see cref="ClearRadius"/> of it.</summary>
        static readonly Vector3 PlayCenter = new Vector3(4f, 0f, -8f);
        const float ClearRadius = 48f;

        static readonly Color Horizon = new Color(0.72f, 0.80f, 0.88f);

        public static void Apply(Material ground, Material arenaFloor, Material wall, GameObject groundPlane)
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Project/Art", "Environment");
            var sun = Sun();
            Sky(sun);
            Atmosphere();
            PostProcessing();
            Surfaces(ground, arenaFloor, wall, groundPlane);
            Scenery();
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ Light, sky, fog

        static Light Sun()
        {
            Light sun = null;
            foreach (var light in Object.FindObjectsByType<Light>())
                if (light.type == LightType.Directional) sun = light;
            if (sun == null) sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.name = "Sun";
            // Late-afternoon sun: low enough for long shadows that show the shape of the stations and fighters.
            sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            return sun;
        }

        static void Sky(Light sun)
        {
            string path = $"{Dir}/Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, path);
            }
            sky.SetFloat("_SunDisk", 2f); // high quality
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            sky.SetFloat("_AtmosphereThickness", 0.8f);
            sky.SetColor("_SkyTint", new Color(0.42f, 0.55f, 0.78f));
            sky.SetColor("_GroundColor", new Color(0.42f, 0.45f, 0.40f));
            sky.SetFloat("_Exposure", 1.2f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
        }

        static void Atmosphere()
        {
            // Three-colour ambient needs no baking and keeps shadowed sides readable: cool sky, neutral horizon,
            // warm bounce from the grass.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.60f, 0.74f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.48f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.24f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Horizon;
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 260f;
        }

        static void PostProcessing()
        {
            string path = $"{Dir}/SandboxLook.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            T Get<T>() where T : VolumeComponent
            {
                if (!profile.TryGet(out T component))
                {
                    component = profile.Add<T>(true);
                    AssetDatabase.AddObjectToAsset(component, profile);
                }
                component.active = true;
                return component;
            }

            var tonemapping = Get<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);
            var color = Get<ColorAdjustments>();
            color.postExposure.Override(0.35f); // ACES darkens mid-tones
            color.contrast.Override(12f);
            color.saturation.Override(6f);
            var white = Get<WhiteBalance>();
            white.temperature.Override(6f);
            // Only bright, HDR surfaces bloom: the aura, the sun, impact sparks.
            var bloom = Get<Bloom>();
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.75f);
            bloom.scatter.Override(0.7f);
            var vignette = Get<Vignette>();
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);
            EditorUtility.SetDirty(profile);

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            var camera = Camera.main;
            if (camera != null)
            {
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                camera.allowHDR = true;
                camera.farClipPlane = 600f;
            }
        }

        // ------------------------------------------------------------------ Ground

        static void Surfaces(Material ground, Material arenaFloor, Material wall, GameObject groundPlane)
        {
            // A wider field, so the scenery and fog have room and the world doesn't end at the stations.
            groundPlane.transform.localScale = new Vector3(40f, 1f, 40f);
            var grass = Texture("Grass", 1024, GrassPixel);
            ground.SetTexture("_BaseMap", grass);
            ground.SetTextureScale("_BaseMap", new Vector2(66f, 66f)); // a 6 m tile across 400 m
            ground.color = Color.white;
            ground.SetFloat("_Smoothness", 0.08f);
            // Broad colour variation on a much larger tile hides the grass texture's repetition.
            var macro = Texture("GrassVariation", 256, GrassVariationPixel, linear: true);
            ground.SetTexture("_DetailAlbedoMap", macro);
            ground.SetTextureScale("_DetailAlbedoMap", new Vector2(6f, 6f));
            ground.SetFloat("_DetailAlbedoMapScale", 1f);
            ground.EnableKeyword("_DETAIL_MULX2");
            EditorUtility.SetDirty(ground);

            var sand = Texture("Sand", 512, SandPixel);
            arenaFloor.SetTexture("_BaseMap", sand);
            arenaFloor.SetTextureScale("_BaseMap", new Vector2(5f, 5f));
            arenaFloor.color = Color.white;
            arenaFloor.SetFloat("_Smoothness", 0.05f);
            EditorUtility.SetDirty(arenaFloor);

            var stone = Texture("StoneBlocks", 256, StonePixel);
            wall.SetTexture("_BaseMap", stone);
            wall.SetTextureScale("_BaseMap", new Vector2(9f, 1f)); // the long walls are ~18 m by 1.2 m
            wall.color = Color.white;
            wall.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(wall);
        }

        static Color GrassPixel(float u, float v)
        {
            float patches = Fbm(u, v, 2, 4, 11);
            float blades = Fbm(u, v, 64, 3, 23);
            float dirt = Fbm(u, v, 5, 3, 37);
            var dark = new Color(0.25f, 0.33f, 0.18f);
            var light = new Color(0.35f, 0.44f, 0.24f);
            var dry = new Color(0.46f, 0.46f, 0.30f);
            var soil = new Color(0.40f, 0.34f, 0.25f);
            var c = Color.Lerp(dark, light, blades);
            c = Color.Lerp(c, dry, Smooth(0.55f, 0.8f, patches) * 0.35f);
            c = Color.Lerp(c, soil, Smooth(0.7f, 0.85f, dirt) * 0.4f);
            return c;
        }

        /// <summary>Detail-map multiplier around 0.5 (neutral): lusher and drier regions tens of metres across.</summary>
        static Color GrassVariationPixel(float u, float v)
        {
            float lush = Fbm(u, v, 3, 3, 71);
            float bright = Fbm(u, v, 5, 2, 83);
            float b = 0.42f + 0.16f * bright;
            return new Color(b + 0.05f * (1f - lush), b + 0.04f * lush, b - 0.03f * (1f - lush));
        }

        static Color StonePixel(float u, float v)
        {
            // Two staggered rows of blocks per tile, with mortar lines and weathering.
            float rowF = v * 2f;
            int row = Mathf.FloorToInt(rowF);
            float colF = u * 2f + (row % 2 == 0 ? 0f : 0.5f);
            float fu = colF - Mathf.Floor(colF), fv = rowF - row;
            float mortar = Mathf.Min(Mathf.Min(fu, 1f - fu) * 28f, Mathf.Min(fv, 1f - fv) * 14f);
            float block = Hash(Mathf.FloorToInt(colF) % 2, row, 2, 91);
            float grain = Fbm(u, v, 32, 3, 97);
            var stone = Color.Lerp(new Color(0.46f, 0.43f, 0.39f), new Color(0.60f, 0.57f, 0.51f), 0.5f * block + 0.5f * grain);
            return Color.Lerp(new Color(0.28f, 0.26f, 0.24f), stone, Mathf.Clamp01(mortar));
        }

        static Color SandPixel(float u, float v)
        {
            float grain = Fbm(u, v, 96, 2, 51);
            float drifts = Fbm(u, v, 4, 3, 63);
            var low = new Color(0.56f, 0.48f, 0.37f);
            var high = new Color(0.70f, 0.62f, 0.49f);
            return Color.Lerp(low, high, 0.65f * grain + 0.35f * drifts);
        }

        internal delegate Color Pixel(float u, float v);

        /// <param name="linear">Detail maps multiply by 2× their value, so 0.5 must stay 0.5 (no sRGB decoding).</param>
        internal static Texture2D Texture(string name, int size, Pixel pixel, bool linear = false)
        {
            string path = $"{Dir}/{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = pixel((float)x / size, (float)y / size);
            tex.SetPixels(pixels);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.mipmapEnabled = true;
            importer.sRGBTexture = !linear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Tileable value noise: the lattice wraps every `period` cells, so the texture repeats seamlessly.
        internal static float Fbm(float u, float v, int period, int octaves, int seed)
        {
            float sum = 0f, amplitude = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amplitude * ValueNoise(u * period, v * period, period, seed + i * 101);
                norm += amplitude;
                amplitude *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }

        static float ValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(x0, y0, period, seed), b = Hash(x0 + 1, y0, period, seed);
            float c = Hash(x0, y0 + 1, period, seed), d = Hash(x0 + 1, y0 + 1, period, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        internal static float Hash(int x, int y, int period, int seed)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2246822519);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }

        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        // ------------------------------------------------------------------ Scenery

        static void Scenery()
        {
            var root = new GameObject("Scenery");
            var rng = new System.Random(1999);
            float R(float min, float max) => min + (float)rng.NextDouble() * (max - min);

            var trunk = Mat("Trunk", new Color(0.32f, 0.22f, 0.14f), 0.1f);
            var leaves = new[] { Mat("Leaves", new Color(0.18f, 0.36f, 0.14f), 0.15f), Mat("LeavesLight", new Color(0.30f, 0.46f, 0.18f), 0.15f) };
            var pine = Mat("Pine", new Color(0.12f, 0.28f, 0.16f), 0.1f);
            var rock = Mat("Rock", new Color(0.44f, 0.45f, 0.44f), 0.15f);
            var hill = Mat("Hill", new Color(0.24f, 0.34f, 0.22f), 0.05f);
            var cone = ConeMesh();
            var rockMesh = RockMesh();

            // Woods ringing the training grounds, thicker further out.
            for (int i = 0; i < 170; i++)
            {
                float angle = R(0f, Mathf.PI * 2f);
                float radius = ClearRadius + Mathf.Pow(R(0f, 1f), 0.7f) * 70f;
                var pos = PlayCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                float size = R(0.8f, 1.5f);
                if (rng.NextDouble() < 0.45) Pine(root.transform, pos, size, trunk, pine, cone, R(0f, 360f));
                else RoundTree(root.transform, pos, size, trunk, leaves[rng.Next(leaves.Length)], rng);
            }

            // Boulders scattered just outside the play area and among the trees.
            for (int i = 0; i < 45; i++)
            {
                float angle = R(0f, Mathf.PI * 2f);
                float radius = ClearRadius - 6f + R(0f, 50f);
                var pos = PlayCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                var go = Part(root.transform, "Rock", rockMesh, rock, pos + Vector3.up * R(-0.2f, 0.2f),
                    new Vector3(R(0.8f, 2.6f), R(0.6f, 1.8f), R(0.8f, 2.6f)), Quaternion.Euler(R(-10f, 10f), R(0f, 360f), R(-10f, 10f)));
                go.AddComponent<MeshCollider>().sharedMesh = rockMesh;
            }

            // Rolling hills on the horizon give the fog something to swallow.
            var sphere = BuiltinMesh(PrimitiveType.Sphere);
            for (int i = 0; i < 16; i++)
            {
                float angle = i / 16f * Mathf.PI * 2f + R(-0.15f, 0.15f);
                float radius = R(170f, 230f);
                var pos = PlayCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                float width = R(70f, 120f);
                Part(root.transform, "Hill", sphere, hill, pos + Vector3.down * R(8f, 16f), new Vector3(width, R(30f, 55f), width * R(0.6f, 1f)),
                    Quaternion.Euler(0f, R(0f, 360f), 0f), shadows: false);
            }
        }

        static void Pine(Transform parent, Vector3 pos, float size, Material trunk, Material needles, Mesh cone, float yaw)
        {
            var tree = new GameObject("Pine");
            tree.transform.SetParent(parent, false);
            tree.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            tree.isStatic = true;
            var stem = Part(tree.transform, "Trunk", BuiltinMesh(PrimitiveType.Cylinder), trunk,
                pos + Vector3.up * 1f * size, new Vector3(0.35f, 1f, 0.35f) * size, Quaternion.identity);
            stem.AddComponent<CapsuleCollider>();
            for (int k = 0; k < 3; k++)
            {
                float y = (1.6f + k * 1.5f) * size;
                float w = (3.2f - k * 0.8f) * size;
                Part(tree.transform, "Needles", cone, needles, pos + Vector3.up * y, new Vector3(w, 2.6f * size, w), Quaternion.Euler(0f, yaw + k * 40f, 0f));
            }
        }

        static void RoundTree(Transform parent, Vector3 pos, float size, Material trunk, Material leaves, System.Random rng)
        {
            var tree = new GameObject("Tree");
            tree.transform.SetParent(parent, false);
            tree.transform.position = pos;
            tree.isStatic = true;
            var stem = Part(tree.transform, "Trunk", BuiltinMesh(PrimitiveType.Cylinder), trunk,
                pos + Vector3.up * 1.4f * size, new Vector3(0.4f, 1.4f, 0.4f) * size, Quaternion.identity);
            stem.AddComponent<CapsuleCollider>();
            var sphere = BuiltinMesh(PrimitiveType.Sphere);
            for (int k = 0; k < 4; k++)
            {
                var offset = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() * 0.6f, (float)rng.NextDouble() - 0.5f) * 1.6f * size;
                float s = (2.2f + (float)rng.NextDouble() * 1.2f) * size;
                Part(tree.transform, "Leaves", sphere, leaves, pos + Vector3.up * 3.3f * size + offset, new Vector3(s, s * 0.85f, s), Quaternion.identity);
            }
        }

        static GameObject Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale, Quaternion rotation, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            go.isStatic = true;
            return go;
        }

        static Material Mat(string name, Color color, float smoothness)
        {
            string path = $"{Dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            mat.SetFloat("_Smoothness", smoothness);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A unit cone (base radius 0.5 at y=-0.5, tip at y=+0.5), flat-shaded for a low-poly look.</summary>
        static Mesh ConeMesh()
        {
            string path = $"{Dir}/Cone.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            const int sides = 9;
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            var tip = new Vector3(0f, 0.5f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, -0.5f, Mathf.Sin(a0) * 0.5f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, -0.5f, Mathf.Sin(a1) * 0.5f);
                int b = verts.Count;
                verts.AddRange(new[] { p0, tip, p1 });
                tris.AddRange(new[] { b, b + 1, b + 2 });
                b = verts.Count;
                verts.AddRange(new[] { p0, p1, new Vector3(0f, -0.5f, 0f) });
                tris.AddRange(new[] { b, b + 1, b + 2 });
            }
            var coneTris = tris.ToArray();
            OutwardWinding(verts.ToArray(), coneTris, Vector3.zero);
            var mesh = new Mesh { name = "Cone", vertices = verts.ToArray(), triangles = coneTris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Flips any triangle facing inward (Unity's front faces wind clockwise seen from outside).</summary>
        static void OutwardWinding(Vector3[] verts, int[] tris, Vector3 center)
        {
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - center) < 0f)
                    (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            }
        }

        static Mesh BuiltinMesh(PrimitiveType type)
        {
            var temp = GameObject.CreatePrimitive(type);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return mesh;
        }

        /// <summary>
        /// A boulder: an icosahedron subdivided twice, each vertex pushed in or out by layered noise for a lumpy,
        /// weathered outline, then flat-shaded and flattened on the bottom so it sits on the ground.
        /// </summary>
        internal static Mesh RockMesh()
        {
            string path = $"{Dir}/Boulder.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var points = new System.Collections.Generic.List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < points.Count; i++) points[i] = points[i].normalized;
            var faces = new System.Collections.Generic.List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int level = 0; level < 2; level++)
            {
                var midpoints = new System.Collections.Generic.Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (midpoints.TryGetValue(key, out int m)) return m;
                    points.Add(((points[a] + points[b]) * 0.5f).normalized);
                    return midpoints[key] = points.Count - 1;
                }
                var next = new System.Collections.Generic.List<int>();
                for (int i = 0; i < faces.Count; i += 3)
                {
                    int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                faces = next;
            }
            for (int i = 0; i < points.Count; i++)
            {
                var n = points[i];
                // Big lumps plus small chips; sampling the noise on the sphere keeps it seamless.
                float lumps = Fbm(n.x * 0.5f + 0.5f, n.z * 0.5f + 0.5f + n.y * 0.25f, 3, 2, 131);
                float chips = Fbm(n.y * 0.5f + 0.5f, n.x * 0.5f + 0.5f + n.z * 0.25f, 9, 2, 137);
                var q = n * (0.42f + 0.16f * lumps + 0.05f * chips);
                points[i] = new Vector3(q.x, Mathf.Max(q.y * 0.75f, -0.2f), q.z);
            }
            var verts = new Vector3[faces.Count];
            var tris = new int[faces.Count];
            for (int i = 0; i < faces.Count; i++)
            {
                verts[i] = points[faces[i]];
                tris[i] = i;
            }
            OutwardWinding(verts, tris, Vector3.zero);
            var mesh = new Mesh { name = "Boulder", vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
