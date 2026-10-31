using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HunterAwakening.EditorTools
{
    /// <summary>
    /// Dresses the training stations: the gameplay primitives keep their colliders and interaction points, while
    /// visual-only props (planks, steel, stone, glowing seals) replace or decorate what the player sees.
    /// </summary>
    static class StationProps
    {
        const string Dir = "Assets/_Project/Art/Environment";

        static Material Wood => Mat("PropWood", new Color(0.45f, 0.30f, 0.18f), 0.15f);
        static Material DarkWood => Mat("PropDarkWood", new Color(0.30f, 0.20f, 0.12f), 0.15f);
        static Material Steel => Mat("PropSteel", new Color(0.18f, 0.19f, 0.21f), 0.55f, metallic: 0.8f);
        static Material Chrome => Mat("PropChrome", new Color(0.75f, 0.76f, 0.78f), 0.8f, metallic: 1f);
        static Material Rubber => Mat("PropRubber", new Color(0.2f, 0.2f, 0.22f), 0.2f);
        static Material Iron => Mat("PropIronPlate", new Color(0.13f, 0.13f, 0.14f), 0.35f, metallic: 0.5f);
        static Material Mossy => Mat("PropMossyRock", new Color(0.42f, 0.45f, 0.42f), 0.1f);
        static Material Cloth => Mat("PropBanner", new Color(0.55f, 0.10f, 0.12f), 0.05f);
        static Material Flag => Mat("PropFlag", new Color(0.92f, 0.90f, 0.85f), 0.05f);
        static Material PadStone => Mat("PropPadStone", new Color(0.34f, 0.35f, 0.38f), 0.2f);

        // ------------------------------------------------------------------ Stations

        /// <summary>A wooden bench with a backrest (the box keeps its collider and the seat point).</summary>
        public static void Bench(GameObject bench)
        {
            Hide(bench);
            var t = Holder("Bench", bench.transform);
            for (int i = 0; i < 3; i++)
                Box(t, "Plank", new Vector3(0f, 0.42f, -0.24f + i * 0.24f), new Vector3(2.2f, 0.06f, 0.2f), Wood);
            foreach (float x in new[] { -0.95f, 0.95f })
            {
                Box(t, "Leg", new Vector3(x, 0.19f, -0.25f), new Vector3(0.09f, 0.38f, 0.09f), DarkWood);
                Box(t, "Leg", new Vector3(x, 0.19f, 0.25f), new Vector3(0.09f, 0.38f, 0.09f), DarkWood);
                Box(t, "Rail", new Vector3(x, 0.3f, 0f), new Vector3(0.07f, 0.07f, 0.6f), DarkWood);
                Box(t, "Back Post", new Vector3(x, 0.72f, 0.31f), new Vector3(0.08f, 0.6f, 0.08f), DarkWood, Quaternion.Euler(-12f, 0f, 0f));
            }
            Box(t, "Backrest", new Vector3(0f, 0.85f, 0.34f), new Vector3(2.1f, 0.22f, 0.05f), Wood, Quaternion.Euler(-12f, 0f, 0f));
        }

        /// <summary>A squat rack: steel uprights with J-hooks on a rubber platform, plus a plate tree.</summary>
        public static void LiftRack(Transform root, Transform barbell)
        {
            foreach (Transform child in root)
                if (child.name.StartsWith("Post")) Hide(child.gameObject);
            Box(root, "Platform", new Vector3(0f, 0.02f, 0.3f), new Vector3(3.2f, 0.04f, 2.4f), Rubber);
            foreach (float x in new[] { -1.3f, 1.3f })
            {
                Box(root, "Upright", new Vector3(x, 1.15f, 0.6f), new Vector3(0.1f, 2.3f, 0.1f), Steel);
                Box(root, "Upright", new Vector3(x, 1.15f, 1.3f), new Vector3(0.1f, 2.3f, 0.1f), Steel);
                Box(root, "Foot", new Vector3(x, 0.06f, 0.95f), new Vector3(0.12f, 0.08f, 1.0f), Steel);
                Box(root, "J-Hook", new Vector3(x, 1.12f, 0.52f), new Vector3(0.1f, 0.06f, 0.16f), Steel);
                Box(root, "J-Hook Lip", new Vector3(x, 1.17f, 0.45f), new Vector3(0.1f, 0.1f, 0.03f), Steel);
                for (int k = 0; k < 9; k++) // adjustment holes read as notches on the front uprights
                    Box(root, "Notch", new Vector3(x, 0.5f + k * 0.15f, 0.548f), new Vector3(0.04f, 0.04f, 0.005f), Rubber);
            }
            Box(root, "Top Bar", new Vector3(0f, 2.28f, 1.3f), new Vector3(2.7f, 0.08f, 0.08f), Steel);
            Box(root, "Top Bar", new Vector3(0f, 2.28f, 0.6f), new Vector3(2.7f, 0.08f, 0.08f), Steel);

            // Chrome bar, bigger stacked plates and collars on the barbell (they ride with it onto the shoulders).
            foreach (Transform child in barbell)
            {
                if (child.name == "Bar") child.GetComponent<Renderer>().sharedMaterial = Chrome;
                else if (child.name == "Plate") Hide(child.gameObject);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                Disc(barbell, "Plate", new Vector3(side * 1.02f, 0f, 0f), 0.46f, 0.05f, Iron, Quaternion.Euler(0f, 0f, 90f));
                Disc(barbell, "Plate", new Vector3(side * 1.09f, 0f, 0f), 0.38f, 0.045f, Iron, Quaternion.Euler(0f, 0f, 90f));
                Disc(barbell, "Plate", new Vector3(side * 1.15f, 0f, 0f), 0.28f, 0.035f, Iron, Quaternion.Euler(0f, 0f, 90f));
                Disc(barbell, "Collar", new Vector3(side * 1.2f, 0f, 0f), 0.06f, 0.03f, Chrome, Quaternion.Euler(0f, 0f, 90f));
            }

            // Plate tree beside the rack.
            var tree = new GameObject("Plate Tree").transform;
            tree.SetParent(root, false);
            tree.localPosition = new Vector3(2.4f, 0f, 1.1f);
            Box(tree, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(0.7f, 0.1f, 0.5f), Steel);
            Disc(tree, "Pole", new Vector3(0f, 0.6f, 0f), 0.06f, 1.0f, Steel, Quaternion.identity);
            // Plates stand on edge on the base, largest at the back.
            for (int k = 0; k < 4; k++)
            {
                float r = 0.42f - k * 0.07f;
                Disc(tree, "Stored Plate", new Vector3(0f, 0.1f + r, 0.16f - k * 0.08f), r, 0.04f, Iron, Quaternion.Euler(90f, 0f, 0f));
            }
        }

        /// <summary>A wooden signpost with a coloured board in place of a bare console box.</summary>
        public static void Signpost(GameObject console, Material board)
        {
            Hide(console);
            var root = Holder("Signpost", console.transform);
            Box(root, "Post", new Vector3(0f, 0.65f, 0f), new Vector3(0.12f, 1.3f, 0.12f), DarkWood);
            Box(root, "Board", new Vector3(0f, 1.25f, -0.06f), new Vector3(0.9f, 0.5f, 0.06f), Wood, Quaternion.Euler(-12f, 0f, 0f));
            Box(root, "Face", new Vector3(0f, 1.25f, -0.095f), new Vector3(0.78f, 0.38f, 0.01f), board, Quaternion.Euler(-12f, 0f, 0f));
        }

        /// <summary>A sprint checkpoint gets a stone footing and a pennant on top (the post itself still lights up).</summary>
        public static void SprintPost(Transform post)
        {
            var pos = post.position;
            var root = new GameObject("Pennant").transform;
            root.SetParent(post.parent, false);
            root.position = new Vector3(pos.x, 0f, pos.z);
            Disc(root, "Footing", new Vector3(0f, 0.08f, 0f), 0.45f, 0.16f, PadStone, Quaternion.identity);
            Disc(root, "Mast", new Vector3(0f, 3.5f, 0f), 0.04f, 1.0f, DarkWood, Quaternion.identity);
            Box(root, "Flag", new Vector3(0.32f, 3.75f, 0f), new Vector3(0.6f, 0.36f, 0.02f), Flag);
        }

        /// <summary>A stone plinth under each precision pad, so raised pads read as steps rather than floating discs.</summary>
        public static void PadPlinth(Transform pad)
        {
            float top = pad.position.y + pad.lossyScale.y; // cylinders are 2 units tall
            var root = new GameObject("Plinth").transform;
            root.SetParent(pad.parent, false);
            root.position = new Vector3(pad.position.x, 0f, pad.position.z);
            float height = Mathf.Max(0.06f, top - 0.03f);
            Disc(root, "Plinth", new Vector3(0f, height / 2f, 0f), pad.lossyScale.x * 0.5f + 0.12f, height, PadStone, Quaternion.identity);
        }

        /// <summary>The meditation stone becomes a mossy boulder (its box keeps the collider and kneel point).</summary>
        public static void MeditationStone(GameObject stone)
        {
            Hide(stone);
            var root = Holder("Boulder", stone.transform);
            var rock = SceneLook.RockMesh();
            Mesh(root, "Boulder", rock, Mossy, new Vector3(0f, 1.0f, 0f), new Vector3(2.2f, 3.2f, 2.0f), Quaternion.Euler(0f, 25f, 4f));
            Mesh(root, "Stone", rock, Mossy, new Vector3(1.3f, 0.2f, 0.6f), new Vector3(0.7f, 0.5f, 0.6f), Quaternion.Euler(0f, 70f, 0f));
            Mesh(root, "Stone", rock, Mossy, new Vector3(-1.2f, 0.15f, -0.4f), new Vector3(0.5f, 0.4f, 0.5f), Quaternion.Euler(0f, 140f, 0f));
        }

        /// <summary>
        /// Paints a glowing seal (rings, a six-pointed star and runes) onto an aura floor disc's material: the top cap
        /// of a cylinder maps its UVs straight across, so the seal lies flat on the disc.
        /// </summary>
        public static void Seal(Material material, Color glow)
        {
            var seal = SceneLook.Texture("AuraSeal", 512, SealPixel);
            material.SetTexture("_BaseMap", seal);
            material.SetTexture("_EmissionMap", seal);
            material.SetColor("_EmissionColor", glow * 1.6f);
            material.EnableKeyword("_EMISSION");
            // Not "None": URP re-validates materials and switches emission off when the GI flag is None.
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(material);
        }

        /// <summary>Stone pillars at the arena's corners and banners on its walls.</summary>
        public static void Arena(Transform root, float halfSize, Material stone)
        {
            foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 1) })
            {
                var pos = new Vector3(corner.x * halfSize, 0f, corner.y * halfSize);
                Box(root, "Pillar", pos + Vector3.up * 1.2f, new Vector3(0.9f, 2.4f, 0.9f), stone);
                Box(root, "Pillar Cap", pos + Vector3.up * 2.48f, new Vector3(1.1f, 0.16f, 1.1f), stone);
            }
            // Banners hang on the inner faces of the far walls (walls are 0.5 m thick and 1.2 m high).
            foreach (var (pos, yaw) in new[] { (new Vector3(0f, 0f, halfSize - 0.27f), 0f), (new Vector3(halfSize - 0.27f, 0f, 0f), 90f) })
            {
                var rot = Quaternion.Euler(0f, yaw, 0f);
                for (int k = -1; k <= 1; k += 2)
                {
                    var at = pos + rot * new Vector3(k * 4.5f, 0f, 0f);
                    Box(root, "Banner Rod", at + Vector3.up * 1.12f, new Vector3(1.0f, 0.05f, 0.05f), DarkWood, rot);
                    Box(root, "Banner", at + Vector3.up * 0.68f, new Vector3(0.85f, 0.85f, 0.02f), Cloth, rot);
                }
            }
        }

        // ------------------------------------------------------------------ Seal texture

        static Color SealPixel(float u, float v)
        {
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(y, x);
            float line = 0f;
            line = Mathf.Max(line, Ring(r, 0.93f, 0.025f));
            line = Mathf.Max(line, Ring(r, 0.86f, 0.008f));
            line = Mathf.Max(line, Ring(r, 0.68f, 0.012f));
            line = Mathf.Max(line, Ring(r, 0.14f, 0.02f));
            // Runes: short glyph strokes between the outer rings.
            const int glyphs = 28;
            float seg = (a / (Mathf.PI * 2f) + 0.5f) * glyphs;
            int index = Mathf.FloorToInt(seg);
            float f = seg - index;
            if (r > 0.72f && r < 0.83f && f > 0.2f && f < 0.8f)
            {
                float h = SceneLook.Hash(index, 0, glyphs, 7);
                float radial = (r - 0.72f) / 0.11f;
                bool stroke = (h > 0.5f && Mathf.Abs(f - 0.5f) < 0.06f)
                              || (Mathf.Abs(radial - (0.25f + 0.5f * h)) < 0.09f)
                              || (h < 0.3f && Mathf.Abs(f - 0.3f - radial * 0.4f) < 0.05f);
                if (stroke) line = Mathf.Max(line, 0.9f);
            }
            // Six-pointed star: two triangles inscribed in the inner ring.
            for (int t = 0; t < 2; t++)
            {
                for (int k = 0; k < 3; k++)
                {
                    float a0 = t * Mathf.PI / 3f + k * Mathf.PI * 2f / 3f + Mathf.PI / 2f;
                    float a1 = a0 + Mathf.PI * 2f / 3f;
                    var p0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * 0.68f;
                    var p1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * 0.68f;
                    float d = SegmentDistance(new Vector2(x, y), p0, p1);
                    line = Mathf.Max(line, 1f - Mathf.Clamp01((d - 0.006f) / 0.008f));
                }
            }
            var stoneBase = Color.Lerp(new Color(0.10f, 0.10f, 0.12f), new Color(0.16f, 0.16f, 0.18f), SceneLook.Fbm(u, v, 16, 3, 5));
            return Color.Lerp(stoneBase, Color.white, line);
        }

        static float Ring(float r, float radius, float halfWidth) => 1f - Mathf.Clamp01((Mathf.Abs(r - radius) - halfWidth) / 0.006f);

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (a + ab * t - p).magnitude;
        }

        // ------------------------------------------------------------------ Helpers

        /// <summary>An unscaled holder at a gameplay primitive's footprint (ground level, same yaw), for its props.</summary>
        static Transform Holder(string name, Transform target)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(target.parent, false);
            var p = target.position;
            holder.SetPositionAndRotation(new Vector3(p.x, 0f, p.z), Quaternion.Euler(0f, target.eulerAngles.y, 0f));
            holder.gameObject.isStatic = true;
            return holder;
        }

        static void Hide(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }

        /// <summary>A visual-only cube, sized in world units regardless of the parent's scale.</summary>
        static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material, Quaternion? rotation = null)
            => Mesh(parent, name, Builtin(PrimitiveType.Cube), material, localPosition, size, rotation ?? Quaternion.identity);

        /// <summary>A visual-only cylinder of the given radius and height along its local Y.</summary>
        static GameObject Disc(Transform parent, string name, Vector3 localPosition, float radius, float height, Material material, Quaternion rotation)
            => Mesh(parent, name, Builtin(PrimitiveType.Cylinder), material, localPosition, new Vector3(radius * 2f, height / 2f, radius * 2f), rotation);

        static GameObject Mesh(Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition, Vector3 size, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            // Undo the parent's scale so sizes are in metres even under a scaled gameplay primitive.
            var s = parent.lossyScale;
            var inv = new Vector3(1f / Mathf.Max(1e-4f, s.x), 1f / Mathf.Max(1e-4f, s.y), 1f / Mathf.Max(1e-4f, s.z));
            go.transform.localPosition = Vector3.Scale(localPosition, inv);
            go.transform.localRotation = rotation;
            go.transform.localScale = Vector3.Scale(size, inv);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
            go.isStatic = parent.gameObject.isStatic;
            return go;
        }

        static Mesh Builtin(PrimitiveType type)
        {
            var temp = GameObject.CreatePrimitive(type);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return mesh;
        }

        static Material Mat(string name, Color color, float smoothness, float metallic = 0f)
        {
            string path = $"{Dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.color != color)
            {
                mat.color = color;
                mat.SetFloat("_Smoothness", smoothness);
                mat.SetFloat("_Metallic", metallic);
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }
    }
}
