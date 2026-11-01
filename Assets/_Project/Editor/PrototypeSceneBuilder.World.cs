using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HunterAwakening.EditorTools
{
    /// <summary>
    /// The world beyond the training grounds: chimera ant squads camped in clearings in the woods, and the King's
    /// castle to the north, sealed until every squad is beaten, with Meruem waiting on his dais.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string AntDir = Root + "/Art/Characters/Ants";
        static readonly Vector3 WorldCenter = new Vector3(4f, 0f, -8f);
        static readonly Vector3 CastleCenter = new Vector3(4f, 0f, 96f);
        const float CastleHalf = 17f;

        // Bearing from the training grounds (degrees from +X towards +Z), distance, and how much tougher the squad is.
        static readonly (string id, float angle, float distance, float strength)[] Camps =
        {
            ("ants_south", 290f, 62f, 1f),
            ("ants_east", 20f, 64f, 1.25f),
            ("ants_west", 200f, 64f, 1.5f),
            ("ants_north", 115f, 66f, 1.8f),
        };

        static void BuildWorld(Materials m, ExerciseDefinition sparringDef, ExerciseDefinition gyoDef, Transform respawn)
        {
            var root = new GameObject("Chimera Ant Territory");
            var soldier = AntModel("Bloster");
            var grunt = AntModel("Koala");
            if (soldier == null || grunt == null)
                Debug.LogWarning($"[Hunter Awakening] Ant models missing in {AntDir}; squads use capsules. Run prepare_ants.py.txt in Blender.");

            var mound = Mat("AntMound", new Color(0.3f, 0.22f, 0.16f));
            foreach (var (id, angle, distance, strength) in Camps)
            {
                float a = angle * Mathf.Deg2Rad;
                var center = WorldCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * distance;
                ClearScenery(center, 10f);
                BuildCamp(root.transform, m, mound, id, center, strength, soldier, grunt, sparringDef, gyoDef, respawn);
            }
            BuildCastle(root.transform, m, sparringDef, gyoDef, respawn);
        }

        static void BuildCamp(Transform parent, Materials m, Material mound, string id, Vector3 center, float strength,
            GameObject soldier, GameObject grunt, ExerciseDefinition sparringDef, ExerciseDefinition gyoDef, Transform respawn)
        {
            // They face back towards the training grounds, waiting.
            Vector3 toHome = WorldCenter - center;
            toHome.y = 0f;
            float yaw = Quaternion.LookRotation(toHome).eulerAngles.y;
            var camp = Empty($"Ant Camp ({id})", parent, center, yaw);

            // A nest mound behind them, ringed with spikes of earth.
            var nest = Prim(PrimitiveType.Sphere, "Nest", camp.transform, new Vector3(0f, -0.6f, -4f), new Vector3(5f, 3.2f, 4.2f), mound);
            nest.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
            for (int i = 0; i < 6; i++)
            {
                float t = i / 6f * Mathf.PI * 2f;
                var spike = Prim(PrimitiveType.Capsule, "Spike", camp.transform,
                    new Vector3(Mathf.Cos(t) * 3.4f, 0.6f, -4f + Mathf.Sin(t) * 3f), new Vector3(0.35f, 1.1f, 0.35f), mound, false);
                spike.transform.localRotation = Quaternion.Euler(Mathf.Sin(t) * 18f, 0f, -Mathf.Cos(t) * 18f);
            }

            // One soldier in front, two grunts at its flanks.
            var members = new[]
            {
                Ant(camp.transform, m, "Chimera Ant Soldier", soldier, new Vector3(0f, 0f, 1.2f), 2.6f, 0.75f),
                Ant(camp.transform, m, "Chimera Ant", grunt, new Vector3(-2.6f, 0f, -0.8f), 1.7f, 0.45f),
                Ant(camp.transform, m, "Chimera Ant", grunt, new Vector3(2.6f, 0f, -0.8f), 1.7f, 0.45f),
            };
            var tiers = new[] { RivalTier.AntSoldier(strength), RivalTier.AntGrunt(strength), RivalTier.AntGrunt(strength) };
            var encounter = camp.AddComponent<Encounter>();
            encounter.Configure(id, "Chimera Ant squad", false, members, tiers, 12f, 34f, respawn, sparringDef, gyoDef);
        }

        /// <summary>A procedurally animated ant (an unrigged sculpt) on a CharacterController host standing on the ground.</summary>
        static EnemyController Ant(Transform parent, Materials m, string name, GameObject prefab, Vector3 local, float height, float radius)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.localPosition = local + Vector3.up * 0.02f;
            var cc = host.AddComponent<CharacterController>();
            cc.height = height;
            cc.radius = radius;
            cc.center = Vector3.up * (height * 0.5f + cc.skinWidth);
            cc.stepOffset = 0.3f;

            var cue = new GameObject("CueLight");
            cue.transform.SetParent(host.transform, false);
            cue.transform.localPosition = new Vector3(0f, height * 0.6f, 0.7f);
            var cueLight = cue.AddComponent<Light>();
            cueLight.type = LightType.Point;
            cueLight.range = 3.5f;
            cueLight.enabled = false;

            var controller = host.AddComponent<EnemyController>();
            SetRef(controller, "cueLight", cueLight);
            SetBool(controller, "tipOverOnDefeat", false);
            SetFloat(controller, "radius", radius + 0.1f);

            GameObject body;
            if (prefab != null)
            {
                // A plain pivot at the feet carries the procedural motion; the FBX keeps its own axis rotation inside it.
                body = new GameObject("Model");
                body.transform.SetParent(host.transform, false);
                var mesh = (GameObject)PrefabUtility.InstantiatePrefab(prefab, body.transform);
                mesh.transform.localPosition = Vector3.zero;
                // The sculpts come out facing the host's +X; turn them to look where the host looks.
                mesh.transform.localRotation = Quaternion.Euler(0f, -90f, 0f) * mesh.transform.localRotation;
                foreach (var r in mesh.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            else body = Prim(PrimitiveType.Capsule, "Model", host.transform, Vector3.up * (height * 0.5f), new Vector3(radius * 2f, height * 0.5f, radius * 2f), m.Enemy, false);
            var animator = host.AddComponent<CreatureAnimator>();
            animator.Configure(body.transform);
            EditorUtility.SetDirty(animator);
            return controller;
        }

        /// <summary>Imports a prepared ant sculpt (no rig) and gives it a toon material from its baked base colour.</summary>
        static GameObject AntModel(string name)
        {
            string path = $"{AntDir}/{name}/{name}.fbx";
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return null;
            if (importer.animationType != ModelImporterAnimationType.None || importer.importAnimation
                || importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }

            string texturePath = $"{AntDir}/{name}/{name}_BaseColor.png";
            if (AssetImporter.GetAtPath(texturePath) is TextureImporter tex && tex.maxTextureSize != 2048)
            {
                tex.maxTextureSize = 2048; // 4K atlases are wasted on a creature this size
                tex.SaveAndReimport();
            }
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Art/Shaders/Toon.shader");
            string matPath = $"{AntDir}/{name}/{name}_Toon.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);

            // A prefab carries the material, so every squad shares one asset.
            string prefabPath = $"{AntDir}/{name}/{name}.prefab";
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            var prefab = PrefabUtility.SaveAsPrefabAsset(model, prefabPath);
            Object.DestroyImmediate(model);
            return prefab;
        }

        // ------------------------------------------------------------------ The castle

        static void BuildCastle(Transform parent, Materials m, ExerciseDefinition sparringDef, ExerciseDefinition gyoDef, Transform respawn)
        {
            ClearScenery(CastleCenter, CastleHalf + 12f);
            // A road from the training grounds to the gate.
            for (float z = 40f; z < CastleCenter.z - CastleHalf; z += 6f) ClearScenery(new Vector3(CastleCenter.x, 0f, z), 6f);

            var root = Empty("The King's Castle", parent, CastleCenter, 0f);
            var t = root.transform;
            var wall = m.Wall;
            var dark = Mat("CastleDark", new Color(0.16f, 0.13f, 0.2f));
            var violet = Mat("CastleBanner", new Color(0.35f, 0.12f, 0.55f));
            var floor = Mat("CastleFloor", new Color(0.32f, 0.3f, 0.33f));

            const float h = 7f, thick = 1.5f, gate = 7f;
            float s = CastleHalf;
            Prim(PrimitiveType.Cube, "Courtyard", t, new Vector3(0f, 0.02f, 0f), new Vector3(s * 2f, 0.04f, s * 2f), floor, false);
            Prim(PrimitiveType.Cube, "Wall N", t, new Vector3(0f, h / 2f, s), new Vector3(s * 2f + thick, h, thick), wall);
            Prim(PrimitiveType.Cube, "Wall E", t, new Vector3(s, h / 2f, 0f), new Vector3(thick, h, s * 2f + thick), wall);
            Prim(PrimitiveType.Cube, "Wall W", t, new Vector3(-s, h / 2f, 0f), new Vector3(thick, h, s * 2f + thick), wall);
            float side = s - gate / 2f;
            Prim(PrimitiveType.Cube, "Wall S1", t, new Vector3(-(gate / 2f + side / 2f), h / 2f, -s), new Vector3(side + thick / 2f, h, thick), wall);
            Prim(PrimitiveType.Cube, "Wall S2", t, new Vector3(gate / 2f + side / 2f, h / 2f, -s), new Vector3(side + thick / 2f, h, thick), wall);
            Prim(PrimitiveType.Cube, "Gate Lintel", t, new Vector3(0f, h - 1f, -s), new Vector3(gate + 1f, 2f, thick + 0.4f), dark);

            // Battlements along the top of every wall.
            for (int i = 0; i < 4; i++)
            {
                for (float u = -s + 1f; u < s; u += 2.4f)
                {
                    if (i == 3 && Mathf.Abs(u) < gate / 2f + 0.5f) continue;
                    Vector3 pos = i switch
                    {
                        0 => new Vector3(u, h + 0.5f, s),
                        1 => new Vector3(s, h + 0.5f, u),
                        2 => new Vector3(-s, h + 0.5f, u),
                        _ => new Vector3(u, h + 0.5f, -s),
                    };
                    Prim(PrimitiveType.Cube, "Merlon", t, pos, new Vector3(1.1f, 1f, 1.1f) + (i < 1 || i == 3 ? new Vector3(0f, 0f, 0.4f) : new Vector3(0.4f, 0f, 0f)), wall, false);
                }
            }

            // Corner towers and the two gate towers, capped dark.
            var towers = new[] { new Vector3(-s, 0f, -s), new Vector3(s, 0f, -s), new Vector3(-s, 0f, s), new Vector3(s, 0f, s),
                                 new Vector3(-gate / 2f - 1.6f, 0f, -s - 0.6f), new Vector3(gate / 2f + 1.6f, 0f, -s - 0.6f) };
            for (int i = 0; i < towers.Length; i++)
            {
                bool gateTower = i >= 4;
                float r = gateTower ? 2.2f : 3.2f, th = gateTower ? 10f : 13f;
                Prim(PrimitiveType.Cylinder, "Tower", t, towers[i] + Vector3.up * (th / 2f), new Vector3(r * 2f, th / 2f, r * 2f), wall);
                Prim(PrimitiveType.Cylinder, "Tower Cap", t, towers[i] + Vector3.up * (th + 0.4f), new Vector3(r * 2.3f, 0.4f, r * 2.3f), dark, false);
                Prim(PrimitiveType.Cube, "Banner", t, towers[i] + new Vector3(0f, th - 3f, -r - 0.05f), new Vector3(1.2f, 3.2f, 0.1f), violet, false);
            }

            // The throne: steps up to a dais at the north wall, the throne itself, and violet light around it.
            // Each step is under the player's 0.3 m step height so the dais can be climbed.
            Prim(PrimitiveType.Cube, "Dais Step", t, new Vector3(0f, 0.14f, s - 6f), new Vector3(12f, 0.28f, 7f), dark);
            Prim(PrimitiveType.Cube, "Dais", t, new Vector3(0f, 0.42f, s - 4.5f), new Vector3(9f, 0.28f, 4.5f), dark);
            Prim(PrimitiveType.Cube, "Throne Seat", t, new Vector3(0f, 1.3f, s - 3f), new Vector3(2.4f, 1f, 1.6f), violet);
            Prim(PrimitiveType.Cube, "Throne Back", t, new Vector3(0f, 3f, s - 2.3f), new Vector3(2.6f, 4.4f, 0.4f), dark);
            for (int i = -1; i <= 1; i += 2)
            {
                Prim(PrimitiveType.Cylinder, "Pillar", t, new Vector3(i * 7f, 3.5f, s - 5f), new Vector3(1.2f, 3.5f, 1.2f), wall);
                var glow = Empty("Throne Light", t, new Vector3(i * 4f, 3f, s - 5f), 0f, local: true).AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = new Color(0.6f, 0.3f, 1f);
                glow.range = 9f;
                glow.intensity = 3f;
            }
            Prim(PrimitiveType.Cube, "Carpet", t, new Vector3(0f, 0.05f, -2f), new Vector3(3f, 0.02f, s * 2f - 10f), violet, false);

            // The seal across the gate, and the post that explains it.
            var barrierMat = BarrierMaterial();
            var barrier = Prim(PrimitiveType.Cube, "King's Seal", t, new Vector3(0f, h / 2f - 0.5f, -s), new Vector3(gate, h - 1f, 0.6f), barrierMat);
            barrier.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var post = Prim(PrimitiveType.Cube, "Castle Gate", t, new Vector3(-gate / 2f - 4.5f, 1f, -s - 3f), new Vector3(0.3f, 2f, 0.3f), m.Accent);
            StationProps.Signpost(post, m.Accent);
            var gateComp = post.AddComponent<CastleGate>();
            SetString(gateComp, "displayName", "The King's Castle");
            SetFloat(gateComp, "interactRadius", 5f);
            gateComp.Configure(barrier);
            EditorUtility.SetDirty(gateComp);

            BuildKing(t, m, sparringDef, gyoDef, respawn);
        }

        static void BuildKing(Transform castle, Materials m, ExerciseDefinition sparringDef, ExerciseDefinition gyoDef, Transform respawn)
        {
            const float height = 2.6f;
            var host = Prim(PrimitiveType.Capsule, "Meruem", castle, new Vector3(0f, 0.56f + height / 2f, CastleHalf - 6.5f), Vector3.one, m.Enemy);
            host.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Object.DestroyImmediate(host.GetComponent<CapsuleCollider>());
            var cc = host.AddComponent<CharacterController>();
            cc.height = height;
            cc.radius = 0.5f;
            cc.center = Vector3.zero;

            var cue = new GameObject("CueLight");
            cue.transform.SetParent(host.transform, false);
            cue.transform.localPosition = new Vector3(0f, 0.3f, 0.6f);
            var cueLight = cue.AddComponent<Light>();
            cueLight.type = LightType.Point;
            cueLight.range = 3.5f;
            cueLight.enabled = false;

            var controller = host.AddComponent<EnemyController>();
            SetRef(controller, "cueLight", cueLight);
            SetBool(controller, "tipOverOnDefeat", false);

            var avatar = characterAnimator != null ? RivalModelSetup.Configure("Meruem") : null;
            var model = avatar != null ? AttachModel(host, cc, RivalModelSetup.ModelPath("Meruem"), avatar, 2.75f, "Meruem") : null;
            if (model != null)
            {
                host.GetComponent<MeshRenderer>().enabled = false;
                ToonMaterials.Apply(model, "Meruem");
                var animation = host.AddComponent<EnemyAnimator>();
                SetRef(animation, "animator", model.GetComponent<Animator>());
                SetFloat(animation, "runClipSpeed", PlayerModelSetup.ClipSpeed("Run", 5.58f));
            }
            else Debug.LogWarning("[Hunter Awakening] Meruem's rigged model is missing; the King is a capsule.");

            var encounter = host.transform.parent.gameObject.AddComponent<Encounter>();
            encounter.Configure("king", "Meruem, the Chimera Ant King", true, new[] { controller }, new[] { RivalTier.King() },
                13f, CastleHalf + 7f, respawn, sparringDef, gyoDef);
        }

        static Material BarrierMaterial()
        {
            string path = $"{MaterialDir}/KingsSeal.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetColor("_BaseColor", new Color(0.55f, 0.2f, 1f, 0.35f));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Removes trees and boulders from a clearing (the scenery is scattered before the world is placed).</summary>
        static void ClearScenery(Vector3 center, float radius)
        {
            var scenery = GameObject.Find("Scenery");
            if (scenery == null) return;
            var doomed = new List<GameObject>();
            foreach (Transform child in scenery.transform)
            {
                if (child.name == "Hill") continue;
                Vector3 d = child.position - center;
                d.y = 0f;
                if (d.magnitude < radius) doomed.Add(child.gameObject);
            }
            foreach (var go in doomed) Object.DestroyImmediate(go);
        }
    }
}
