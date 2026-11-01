using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HunterAwakening.EditorTools
{
    /// <summary>
    /// Generates the training sandbox (placeholder primitives), its data assets and materials.
    /// Re-running it rebuilds the scene and updates existing assets in place.
    /// </summary>
    public static partial class PrototypeSceneBuilder
    {
        const string Root = "Assets/_Project";
        const string DataDir = Root + "/Data";
        const string MaterialDir = Root + "/Materials";
        const string SceneDir = Root + "/Scenes";
        public const string ScenePath = SceneDir + "/TrainingSandbox.unity";

        [MenuItem("Hunter Awakening/Build Prototype Scene", priority = 0)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        [MenuItem("Hunter Awakening/Open Save Folder", priority = 20)]
        public static void OpenSaveFolder() => EditorUtility.RevealInFinder(Application.persistentDataPath);

        [MenuItem("Hunter Awakening/Delete Save File", priority = 21)]
        public static void DeleteSave()
        {
            var saves = new SaveService();
            if (!saves.Exists) { Debug.Log("[Hunter Awakening] No save file to delete."); return; }
            if (EditorUtility.DisplayDialog("Delete save", $"Delete {saves.FilePath}?", "Delete", "Cancel"))
                saves.Delete();
        }

        public static void Build()
        {
            EnsureFolder(Root);
            EnsureFolder(DataDir);
            EnsureFolder(MaterialDir);
            EnsureFolder(SceneDir);

            var config = CreateConfig();
            var liftDef = Exercise("lift_rack", "Lift Rack", ExerciseCategory.Physical,
                "Pick a load and time each rep inside the form window.",
                new[] { (AttributeId.Strength, 1f), (AttributeId.Durability, 0.2f) }, TechniqueId.Lift,
                fatigue: 22f, maxLevel: 10, requiresAura: false,
                qualityHint: "press when the needle is inside the green zone; heavier loads narrow it.",
                lowHint: "Strength progress reduced because the selected load is now too easy. Raise it with ].",
                metric: "heaviest completed set (kg)", lowerIsBetter: false, stressedArea: BodyArea.Upper);
            var sprintDef = Exercise("sprint_course", "Sprint Course", ExerciseCategory.Physical,
                "Run laps through the posts in order. Hold sprint the whole way.",
                new[] { (AttributeId.Speed, 1f), (AttributeId.Endurance, 0.7f) }, null,
                fatigue: 24f, maxLevel: 6, requiresAura: false,
                qualityHint: "you spent too long walking or standing — hold Shift and manage stamina.",
                lowHint: "this lap count is comfortable for your Endurance now. Add laps with ].",
                metric: "average lap time (s)", lowerIsBetter: true, stressedArea: BodyArea.Lower);
            var precisionDef = Exercise("precision_pads", "Precision Pads", ExerciseCategory.Physical,
                "Reach each glowing pad before its window closes. Raised pads need a jump.",
                new[] { (AttributeId.Coordination, 1f), (AttributeId.Mobility, 0.6f), (AttributeId.Speed, 0.2f) }, TechniqueId.Footwork,
                fatigue: 14f, maxLevel: 7, requiresAura: false,
                qualityHint: "pads were reached late or missed — plan your route and jump early for raised pads.",
                lowHint: "the window is generous for your Coordination now. Shorten it with ].",
                metric: "hit rate (%)", lowerIsBetter: false, stressedArea: BodyArea.Lower);
            var tenDef = Exercise("aura_ten_control", "Aura Mat", ExerciseCategory.Aura,
                "Hold aura flow inside the stable band despite drift and disturbances.",
                new[] { (AttributeId.ControlPrecision, 1f), (AttributeId.SustainedOutput, 0.6f), (AttributeId.AuraCapacity, 0.4f), (AttributeId.Efficiency, 0.3f) },
                TechniqueId.Ten, fatigue: 16f, maxLevel: 5, requiresAura: true,
                qualityHint: "aura control was unstable — make small corrections instead of holding the key.",
                lowHint: "this level no longer challenges your control. Raise it with ].",
                metric: "stability (%)", lowerIsBetter: false);
            var renDef = Exercise("aura_ren_output", "Ren Circle", ExerciseCategory.Aura,
                "Flare Ren above a rising target line through pressure waves without crossing your control ceiling.",
                new[] { (AttributeId.MaxOutput, 1f), (AttributeId.SustainedOutput, 0.7f), (AttributeId.AuraCapacity, 0.3f) },
                TechniqueId.Ren, fatigue: 20f, maxLevel: 5, requiresAura: true,
                qualityHint: "output kept dropping below the target or crossing the ceiling — hold steady and re-push right after each pressure wave.",
                lowHint: "this output target is easy for you now. Raise it with ].",
                metric: "stability (%)", lowerIsBetter: false);
            var gyoStonesDef = Exercise("gyo_stones", "Gyo Stones", ExerciseCategory.Aura,
                "Find the stone carrying a hidden aura mark — visible only through Gyo.",
                new[] { (AttributeId.Perception, 1f), (AttributeId.ControlPrecision, 0.3f) }, TechniqueId.Gyo,
                fatigue: 10f, maxLevel: 5, requiresAura: true,
                qualityHint: "marks were missed or found late — keep Gyo up while you move, and head for the mark as soon as you see it.",
                lowHint: "the window is generous for your Perception now. Shorten it with ].",
                metric: "marks found (%)", lowerIsBetter: false);
            var sparringDef = Exercise("sparring", "Sparring", ExerciseCategory.Combat,
                "A real fight is training: absorbing pressure builds Durability, avoiding it builds Coordination.",
                new[] { (AttributeId.Durability, 0.7f), (AttributeId.Coordination, 0.5f), (AttributeId.Endurance, 0.3f) }, null,
                fatigue: 12f, maxLevel: 1, requiresAura: false,
                qualityHint: "you were hit often and landed little — read the wind-up, dodge or parry, then punish the opening.",
                lowHint: "this opponent no longer pushes you. Pick a stronger one at the arena post ([ / ]).",
                metric: "", lowerIsBetter: false);
            var gyoDef = Exercise("gyo_reading", "Gyo Reading", ExerciseCategory.Aura,
                "Reading wind-ups with Gyo trains Perception.",
                new[] { (AttributeId.Perception, 1f), (AttributeId.ControlPrecision, 0.2f) }, TechniqueId.Gyo,
                fatigue: 5f, maxLevel: 1, requiresAura: true,
                qualityHint: "you read the attacks but still got hit — act on what Gyo shows you (dodge or parry on the countdown).",
                lowHint: "this opponent's wind-ups are slow for your eyes now. Face a faster opponent.",
                metric: "", lowerIsBetter: false);

            var m = new Materials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var ground = Prim(PrimitiveType.Plane, "Ground", null, Vector3.zero, new Vector3(10f, 1f, 10f), m.Ground);
            ground.isStatic = true;
            SceneLook.Apply(m.Ground, m.ArenaFloor, m.Wall, ground);

            // Player
            var player = Prim(PrimitiveType.Capsule, "Player", null, new Vector3(0f, 1f, 0f), Vector3.one, m.Player);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.tag = "Player";
            var cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.4f;
            cc.center = Vector3.zero;
            cc.stepOffset = 0.3f;
            PrepareCharacterModel();
            var model = AttachPlayerModel(player, cc, out string playerCharacter);
            if (model != null)
            {
                player.GetComponent<MeshRenderer>().enabled = false; // capsule stays as a fallback visual
                ToonMaterials.Apply(model, playerCharacter);
                var animation = player.AddComponent<PlayerAnimator>();
                SetRef(animation, "animator", model.GetComponent<Animator>());
                SetFloat(animation, "runClipSpeed", PlayerModelSetup.ClipSpeed("Run", 5.58f));
                SetFloat(animation, "sprintClipSpeed", PlayerModelSetup.ClipSpeed("Sprint", 5.05f));
            }
            else Prim(PrimitiveType.Cube, "Visor", player.transform, new Vector3(0f, 0.5f, 0.35f), new Vector3(0.6f, 0.2f, 0.3f), m.Dark, false);
            var auraLightGo = new GameObject("AuraLight");
            auraLightGo.transform.SetParent(player.transform, false);
            var auraLight = auraLightGo.AddComponent<Light>();
            auraLight.type = LightType.Point;
            auraLight.range = 4f;
            auraLight.enabled = false;

            var input = player.AddComponent<InputReader>();
            player.AddComponent<PlayerVitals>();
            player.AddComponent<Health>();
            var motor = player.AddComponent<PlayerMotor>();
            player.AddComponent<PlayerCombat>();
            var aura = player.AddComponent<AuraController>();
            player.AddComponent<PlayerInteractor>();
            var context = player.AddComponent<PlayerContext>();
            SetRef(aura, "auraLight", auraLight);

            // Camera
            var camera = Camera.main;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 3f, -6f), Quaternion.Euler(15f, 0f, 0f));
            var tpc = camera.gameObject.AddComponent<ThirdPersonCamera>();
            SetRef(tpc, "target", player.transform);
            SetRef(tpc, "input", input);
            SetRef(motor, "cameraTransform", camera.transform);

            // Systems
            var systems = new GameObject("GameSession");
            var session = systems.AddComponent<GameSession>();
            SetRef(session, "config", config);
            SetRef(session, "player", context);
            var effects = systems.AddComponent<ImpactEffects>();
            SetRef(effects, "particleMaterial", ParticleMaterial());
            var hud = systems.AddComponent<PrototypeHUD>();
            SetRef(hud, "player", context);
            SetRef(hud, "worldCamera", camera);

            BuildRestBench(m);
            BuildLiftRack(m, liftDef);
            BuildSprintCourse(m, sprintDef);
            BuildPrecisionPads(m, precisionDef);
            BuildAuraArea(m, tenDef, renDef);
            BuildGyoStones(m, gyoStonesDef);
            BuildArena(m, context, sparringDef, gyoDef);
            // Where a beaten player wakes up: beside the rest bench.
            var respawn = Empty("RespawnPoint", null, new Vector3(0f, 1f, 4.5f), 180f);
            BuildWorld(m, sparringDef, gyoDef, respawn.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Hunter Awakening] Built {ScenePath}. Press Play to start.");
        }

        // ------------------------------------------------------------------ Areas

        static void BuildRestBench(Materials m)
        {
            var bench = Prim(PrimitiveType.Cube, "Rest Bench", null, new Vector3(0f, 0.225f, 7f), new Vector3(2.2f, 0.45f, 0.7f), m.Wood);
            var rest = bench.AddComponent<RestBench>();
            var seat = Empty("SeatPoint", null, new Vector3(0f, 1f, 6.1f), 180f);
            seat.transform.SetParent(bench.transform, true);
            SetString(rest, "displayName", "Rest Bench");
            SetRef(rest, "seatPoint", seat.transform);
            StationProps.Bench(bench);
        }

        static void BuildLiftRack(Materials m, ExerciseDefinition def)
        {
            var root = Empty("Lift Rack", null, new Vector3(-8f, 0f, 5f), 0f);
            Prim(PrimitiveType.Cube, "Post L", root.transform, new Vector3(-1.3f, 1f, 0.6f), new Vector3(0.2f, 2f, 0.2f), m.Metal);
            Prim(PrimitiveType.Cube, "Post R", root.transform, new Vector3(1.3f, 1f, 0.6f), new Vector3(0.2f, 2f, 0.2f), m.Metal);
            var barbell = Empty("Barbell", root.transform, new Vector3(0f, 1.2f, 0.6f), 0f, local: true);
            var bar = Prim(PrimitiveType.Cylinder, "Bar", barbell.transform, Vector3.zero, new Vector3(0.07f, 1.5f, 0.07f), m.Metal, false);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            foreach (float x in new[] { -1.1f, 1.1f })
            {
                var plate = Prim(PrimitiveType.Cylinder, "Plate", barbell.transform, new Vector3(x, 0f, 0f), new Vector3(0.55f, 0.05f, 0.55f), m.Dark, false);
                plate.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            var stand = Empty("StandPoint", root.transform, new Vector3(0f, 1f, 0f), 0f, local: true);

            var drill = root.AddComponent<LiftDrill>();
            SetString(drill, "displayName", "Lift Rack");
            SetFloat(drill, "interactRadius", 3f);
            SetRef(drill, "definition", def);
            SetRef(drill, "barbell", barbell.transform);
            SetRef(drill, "standPoint", stand.transform);
            StationProps.LiftRack(root.transform, barbell.transform);
        }

        static void BuildSprintCourse(Materials m, ExerciseDefinition def)
        {
            var root = Empty("Sprint Course", null, new Vector3(0f, 0f, -24f), 0f);
            const int count = 8;
            var checkpoints = new List<Object>();
            for (int i = 1; i <= count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                var post = Prim(PrimitiveType.Cylinder, $"Checkpoint {i}", root.transform,
                    new Vector3(Mathf.Cos(a) * 14f, 1.5f, Mathf.Sin(a) * 8f), new Vector3(0.5f, 1.5f, 0.5f), m.Checkpoint, false);
                checkpoints.Add(post.transform);
                StationProps.SprintPost(post.transform);
            }
            var start = Prim(PrimitiveType.Cube, "Sprint Start", root.transform, new Vector3(14f, 0.05f, 0f), new Vector3(3f, 0.1f, 1f), m.Accent, false);
            var drill = start.AddComponent<SprintCourseDrill>();
            SetString(drill, "displayName", "Sprint Course");
            SetFloat(drill, "interactRadius", 3f);
            SetInt(drill, "intensityLevel", 1);
            SetRef(drill, "definition", def);
            SetRefs(drill, "checkpoints", checkpoints);
        }

        static void BuildPrecisionPads(Materials m, ExerciseDefinition def)
        {
            var root = Empty("Precision Pads", null, new Vector3(10f, 0f, 8f), 0f);
            float[] heights = { 0.05f, 0.05f, 0.25f, 0.05f, 0.4f, 0.05f, 0.25f, 0.05f, 0.05f };
            var pads = new List<Object>();
            for (int i = 0; i < 9; i++)
            {
                float x = (i % 3 - 1) * 2.6f;
                float z = (i / 3 - 1) * 2.6f;
                var pad = Prim(PrimitiveType.Cylinder, $"Pad {i + 1}", root.transform, new Vector3(x, heights[i], z), new Vector3(1.6f, heights[i], 1.6f), m.Pad);
                pads.Add(pad.transform);
                StationProps.PadPlinth(pad.transform);
            }
            var console = Prim(PrimitiveType.Cube, "Precision Console", root.transform, new Vector3(0f, 0.5f, -5f), new Vector3(0.8f, 1f, 0.4f), m.Accent);
            var drill = console.AddComponent<PrecisionCourseDrill>();
            SetString(drill, "displayName", "Precision Pads");
            SetRef(drill, "definition", def);
            SetRefs(drill, "pads", pads);
            StationProps.Signpost(console, m.Accent);
        }

        static void BuildAuraArea(Materials m, ExerciseDefinition def, ExerciseDefinition renDef)
        {
            var circle = Prim(PrimitiveType.Cylinder, "Ren Circle", null, new Vector3(-20f, 0.03f, 3f), new Vector3(2.4f, 0.03f, 2.4f), m.RenMat);
            var circleStand = Empty("StandPoint", null, new Vector3(-20f, 1.07f, 3f), 90f);
            circleStand.transform.SetParent(circle.transform, true);
            var renDrill = circle.AddComponent<AuraOutputDrill>();
            SetString(renDrill, "displayName", "Ren Circle");
            SetInt(renDrill, "intensityLevel", 1);
            SetRef(renDrill, "definition", renDef);
            SetRef(renDrill, "standPoint", circleStand.transform);
            StationProps.Seal(m.RenMat, new Color(1f, 0.55f, 0.2f));

            var stone = Prim(PrimitiveType.Cube, "Meditation Stone", null, new Vector3(-14f, 1.2f, -4f), new Vector3(1.4f, 2.4f, 1.4f), m.Stone);
            var kneel = Empty("KneelPoint", null, new Vector3(-14f, 1f, -5.8f), 0f);
            kneel.transform.SetParent(stone.transform, true);
            var awakening = stone.AddComponent<AwakeningStone>();
            SetString(awakening, "displayName", "Meditation Stone");
            SetFloat(awakening, "interactRadius", 3f);
            SetRef(awakening, "kneelPoint", kneel.transform);
            StationProps.MeditationStone(stone);

            var mat = Prim(PrimitiveType.Cylinder, "Aura Mat", null, new Vector3(-14f, 0.03f, 3f), new Vector3(2.4f, 0.03f, 2.4f), m.AuraMat);
            var stand = Empty("StandPoint", null, new Vector3(-14f, 1.07f, 3f), 0f);
            stand.transform.SetParent(mat.transform, true);
            var drill = mat.AddComponent<AuraControlDrill>();
            SetString(drill, "displayName", "Aura Mat");
            SetInt(drill, "intensityLevel", 1);
            SetRef(drill, "definition", def);
            SetRef(drill, "standPoint", stand.transform);
            StationProps.Seal(m.AuraMat, new Color(0.3f, 0.9f, 1f));
        }

        static void BuildGyoStones(Materials m, ExerciseDefinition def)
        {
            var root = Prim(PrimitiveType.Cylinder, "Gyo Stones", null, new Vector3(-22f, 0.03f, -8f), new Vector3(2f, 0.03f, 2f), m.Stone);
            var stones = new List<Object>();
            const int count = 5;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                var stone = Prim(PrimitiveType.Cylinder, $"Reading Stone {i + 1}", null,
                    new Vector3(-22f + Mathf.Cos(a) * 3.5f, 0.6f, -8f + Mathf.Sin(a) * 3.5f), new Vector3(0.6f, 0.6f, 0.6f), m.Dark);
                stone.transform.SetParent(root.transform, true);
                stones.Add(stone.transform);
            }
            var lightGo = new GameObject("AuraMark");
            lightGo.transform.SetParent(root.transform, true);
            var mark = lightGo.AddComponent<Light>();
            mark.type = LightType.Point;
            mark.color = new Color(0.6f, 0.85f, 1f);
            mark.range = 3f;
            mark.intensity = 6f;
            mark.enabled = false;

            var drill = root.AddComponent<AuraReadingDrill>();
            SetString(drill, "displayName", "Gyo Stones");
            SetInt(drill, "intensityLevel", 1);
            SetRef(drill, "definition", def);
            SetRefs(drill, "stones", stones);
            SetRef(drill, "markLight", mark);
            StationProps.Seal(m.Stone, new Color(0.6f, 0.75f, 1f));
        }

        static void BuildArena(Materials m, PlayerContext player, ExerciseDefinition sparringDef, ExerciseDefinition gyoDef)
        {
            var root = Empty("Sparring Arena", null, new Vector3(24f, 0f, -4f), 0f);
            Prim(PrimitiveType.Cube, "Floor", root.transform, new Vector3(0f, 0.01f, 0f), new Vector3(18f, 0.02f, 18f), m.ArenaFloor, false);
            Prim(PrimitiveType.Cube, "Wall N", root.transform, new Vector3(0f, 0.6f, 9f), new Vector3(18.5f, 1.2f, 0.5f), m.Wall);
            Prim(PrimitiveType.Cube, "Wall S", root.transform, new Vector3(0f, 0.6f, -9f), new Vector3(18.5f, 1.2f, 0.5f), m.Wall);
            Prim(PrimitiveType.Cube, "Wall E", root.transform, new Vector3(9f, 0.6f, 0f), new Vector3(0.5f, 1.2f, 18f), m.Wall);
            Prim(PrimitiveType.Cube, "Wall W1", root.transform, new Vector3(-9f, 0.6f, 5.5f), new Vector3(0.5f, 1.2f, 7f), m.Wall);
            Prim(PrimitiveType.Cube, "Wall W2", root.transform, new Vector3(-9f, 0.6f, -5.5f), new Vector3(0.5f, 1.2f, 7f), m.Wall);

            var enemy = Prim(PrimitiveType.Capsule, "Sparring Rival", root.transform, new Vector3(3f, 1f, 0f), Vector3.one, m.Enemy);
            enemy.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            Object.DestroyImmediate(enemy.GetComponent<CapsuleCollider>());
            var ecc = enemy.AddComponent<CharacterController>();
            ecc.height = 2f;
            ecc.radius = 0.45f;
            ecc.center = Vector3.zero;

            var cueLightGo = new GameObject("CueLight");
            cueLightGo.transform.SetParent(enemy.transform, false);
            cueLightGo.transform.localPosition = new Vector3(0f, 0.3f, 0.6f);
            var cueLight = cueLightGo.AddComponent<Light>();
            cueLight.type = LightType.Point;
            cueLight.range = 3.5f;
            cueLight.enabled = false;

            var controller = enemy.AddComponent<EnemyController>();
            SetRef(controller, "cueLight", cueLight);
            var model = AttachCharacterModel(enemy, ecc);
            if (model != null)
            {
                // Same Killua model and clips as the player; telegraphs read from the animation, cue light and labels.
                enemy.GetComponent<MeshRenderer>().enabled = false;
                var animation = enemy.AddComponent<EnemyAnimator>();
                SetRef(animation, "animator", model.GetComponent<Animator>());
                SetFloat(animation, "runClipSpeed", PlayerModelSetup.ClipSpeed("Run", 5.58f));
                SetBool(controller, "tipOverOnDefeat", false);
                ApplyRivalColours(model);
                ToonMaterials.Apply(model, "KilluaRival");
                AttachRivalModels(enemy, ecc, model.GetComponent<Animator>());
            }
            else
            {
                Prim(PrimitiveType.Cube, "Visor", enemy.transform, new Vector3(0f, 0.5f, 0.35f), new Vector3(0.6f, 0.2f, 0.3f), m.Dark, false);
                var guard = Prim(PrimitiveType.Cube, "Guard", enemy.transform, new Vector3(0f, 0.1f, 0.6f), new Vector3(1.2f, 1.4f, 0.12f), m.Guard, false);
                guard.SetActive(false);
                SetRef(controller, "bodyRenderer", enemy.GetComponent<Renderer>());
                SetRef(controller, "guardVisual", guard);
            }

            var post = Prim(PrimitiveType.Cube, "Arena Post", root.transform, new Vector3(-11f, 1f, 2.5f), new Vector3(0.3f, 2f, 0.3f), m.Accent);
            StationProps.Signpost(post, m.Accent);
            StationProps.Arena(root.transform, 9f, m.Wall);
            var entry = Empty("EntryPoint", root.transform, new Vector3(-11f, 1f, 0f), 90f, local: true);
            var arena = post.AddComponent<ArenaController>();
            SetString(arena, "displayName", "Sparring Arena");
            SetFloat(arena, "interactRadius", 3f);
            SetRef(arena, "enemy", controller);
            SetRef(arena, "arenaCenter", root.transform);
            SetRef(arena, "entryPoint", entry.transform);
            SetRef(arena, "sparringExercise", sparringDef);
            SetRef(arena, "gyoReadingExercise", gyoDef);
        }

        // ------------------------------------------------------------------ Player model

        const string PlayerControllerPath = Root + "/Art/Characters/Killua/Killua.controller";
        /// <summary>Model height in metres (rest pose). Killua's native height is ~1.56 m; scaled to fill the 2 m controller.</summary>
        const float PlayerModelHeight = 1.9f;

        // Set up once per build and shared by every character that uses the Killua model (player and rival).
        static Avatar characterAvatar;
        static RuntimeAnimatorController characterAnimator;

        static void PrepareCharacterModel()
        {
            characterAvatar = null;
            characterAnimator = null;
            if (AssetImporter.GetAtPath(PlayerModelSetup.ModelPath) == null) return;
            // Always rebuild the avatar: a stale reference pose silently distorts every retargeted clip.
            var avatar = PlayerModelSetup.Configure();
            if (avatar == null || !avatar.isHuman) return;
            characterAvatar = avatar;
            characterAnimator = CreatePlayerController();
        }

        /// <summary>The player's fighter: one of the rigged characters in Rivals/ (falls back to the original Killua).</summary>
        const string PlayerCharacter = "Gon";
        const float PlayerCharacterHeight = 2f;

        static GameObject AttachPlayerModel(GameObject player, CharacterController controller, out string character)
        {
            character = PlayerCharacter;
            var avatar = characterAnimator != null ? RivalModelSetup.Configure(PlayerCharacter) : null;
            if (avatar != null)
                return AttachModel(player, controller, RivalModelSetup.ModelPath(PlayerCharacter), avatar, PlayerCharacterHeight, "Model");
            character = "Killua";
            return AttachCharacterModel(player, controller);
        }

        /// <summary>Adds the animated Killua model under a CharacterController host. Returns null if the model is unavailable.</summary>
        static GameObject AttachCharacterModel(GameObject host, CharacterController controller)
        {
            if (characterAvatar == null) return null;
            return AttachModel(host, controller, PlayerModelSetup.ModelPath, characterAvatar, PlayerModelHeight, "Model");
        }

        /// <summary>
        /// Gives each opponent tier its own fighter when its Mixamo-rigged model is in Rivals/ (inactive until the
        /// arena selects that tier). Tiers without one keep the default shadow Killua.
        /// </summary>
        static void AttachRivalModels(GameObject enemy, CharacterController controller, Animator defaultModel)
        {
            var entries = new List<RivalModels.Entry>();
            foreach (var (name, tierId, height) in RivalModelSetup.Characters)
            {
                var avatar = RivalModelSetup.Configure(name);
                if (avatar == null) continue;
                var model = AttachModel(enemy, controller, RivalModelSetup.ModelPath(name), avatar, height, name);
                if (model == null) continue;
                model.SetActive(false);
                ToonMaterials.Apply(model, name);
                entries.Add(new RivalModels.Entry { tierId = tierId, model = model.GetComponent<Animator>() });
            }
            if (entries.Count == 0) return;
            enemy.AddComponent<RivalModels>().Configure(defaultModel, entries.ToArray());
            Debug.Log($"[Hunter Awakening] Rival models: {string.Join(", ", entries.ConvertAll(e => $"{e.tierId}={e.model.name}"))}");
        }

        /// <summary>Instantiates a Humanoid model under a CharacterController host, scaled to the given height.</summary>
        static GameObject AttachModel(GameObject host, CharacterController controller, string path, Avatar avatar, float height, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;
            var model = Object.Instantiate(prefab, host.transform);
            model.name = name;
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = characterAnimator;

            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Object.DestroyImmediate(model);
                return null;
            }
            var bounds = ModelBounds(renderers);
            model.transform.localScale *= height / bounds.size.y;

            // Humanoid clips put the soles at the model origin, so the origin goes on the ground: the controller's
            // bottom minus its skin width (the gap CharacterController keeps above the floor), centred on its axis.
            model.transform.localPosition = controller.center - Vector3.up * (controller.height * 0.5f + controller.skinWidth);
            return model;
        }

        /// <summary>
        /// One state per Mixamo clip, named like the clip; PlayerAnimator cross-fades between them from code, so the
        /// controller needs no transitions.
        /// </summary>
        static RuntimeAnimatorController CreatePlayerController()
        {
            PlayerModelSetup.EnsureAnimationsConfigured();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(PlayerControllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states) machine.RemoveState(child.state);

            AnimatorState idle = null;
            foreach (var (file, _, _) in PlayerModelSetup.Clips)
            {
                var clip = PlayerModelSetup.LoadClip(file);
                if (clip == null) continue;
                var state = machine.AddState(file);
                state.motion = clip;
                state.writeDefaultValues = true;
                // Foot IK plants the feet where the source clip had them; without it, Killua's different leg
                // proportions leave the feet rotated (most visibly the lead/left foot in the fighting stance).
                state.iKOnFeet = true;
                if (file == "Idle") idle = state;
            }
            if (idle != null) machine.defaultState = idle;
            else Debug.LogWarning($"[Hunter Awakening] No Idle clip in {PlayerModelSetup.AnimationDir}; the player will hold its rest pose.");
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        const string RivalMaterialDir = Root + "/Art/Characters/Killua/RivalMaterials";

        // "Shadow" variant so the rival reads as a different fighter: renderer name → base colour.
        static readonly (string renderer, Color color)[] RivalColours =
        {
            ("OutShirt", new Color(0.55f, 0.08f, 0.1f)),
            ("Inshirt", new Color(0.07f, 0.07f, 0.09f)),
            ("Pants", new Color(0.12f, 0.1f, 0.11f)),
            ("Belt", new Color(0.3f, 0.05f, 0.05f)),
            ("UpperHair", new Color(0.32f, 0.33f, 0.38f)),
            ("UnderHair", new Color(0.25f, 0.26f, 0.3f)),
            ("Shoe", new Color(0.1f, 0.1f, 0.1f)),
            ("Shoe002", new Color(0.5f, 0.08f, 0.1f)),
            ("Show001", new Color(0.1f, 0.1f, 0.1f)),
        };

        /// <summary>Gives the rival tinted copies of the model's materials (saved as assets; the player's are untouched).</summary>
        static void ApplyRivalColours(GameObject model)
        {
            EnsureFolder(RivalMaterialDir);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                int index = System.Array.FindIndex(RivalColours, c => c.renderer == r.name);
                if (index < 0) continue;
                var source = r.sharedMaterials;
                var tinted = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] == null) continue;
                    string path = $"{RivalMaterialDir}/Rival_{r.name}_{i}.mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null)
                    {
                        mat = new Material(source[i]);
                        AssetDatabase.CreateAsset(mat, path);
                    }
                    mat.CopyPropertiesFromMaterial(source[i]);
                    mat.color = RivalColours[index].color;
                    EditorUtility.SetDirty(mat);
                    tinted[i] = mat;
                }
                r.sharedMaterials = tinted;
            }
        }

        static Bounds ModelBounds(Renderer[] renderers)
        {
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        // ------------------------------------------------------------------ Assets

        static ProgressionConfig CreateConfig()
        {
            string path = DataDir + "/ProgressionConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<ProgressionConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ProgressionConfig>();
                config.PopulateDefaults();
                AssetDatabase.CreateAsset(config, path);
            }
            return config;
        }

        static ExerciseDefinition Exercise(string id, string displayName, ExerciseCategory category, string description,
            (AttributeId attribute, float specificity)[] targets, TechniqueId? mastery, float fatigue, int maxLevel, bool requiresAura,
            string qualityHint, string lowHint, string metric, bool lowerIsBetter, BodyArea stressedArea = BodyArea.None)
        {
            string path = $"{DataDir}/Exercise_{id}.asset";
            var def = AssetDatabase.LoadAssetAtPath<ExerciseDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<ExerciseDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }
            def.id = id;
            def.displayName = displayName;
            def.category = category;
            def.description = description;
            def.targets = new List<AttributeTarget>();
            foreach (var t in targets) def.targets.Add(new AttributeTarget { attribute = t.attribute, specificity = t.specificity });
            def.hasMastery = mastery.HasValue;
            def.masteryTechnique = mastery ?? default;
            def.fatigueCost = fatigue;
            def.maxIntensityLevel = maxLevel;
            def.requiresAura = requiresAura;
            def.qualityHint = qualityHint;
            def.lowIntensityHint = lowHint;
            def.metricLabel = metric;
            def.lowerMetricIsBetter = lowerIsBetter;
            def.stressedArea = stressedArea;
            EditorUtility.SetDirty(def);
            return def;
        }

        class Materials
        {
            public readonly Material Ground = Mat("Ground", new Color(0.36f, 0.45f, 0.32f));
            public readonly Material Player = Mat("Player", new Color(0.25f, 0.5f, 0.85f));
            public readonly Material Enemy = Mat("Enemy", new Color(0.75f, 0.3f, 0.3f));
            public readonly Material Dark = Mat("Dark", new Color(0.12f, 0.12f, 0.14f));
            public readonly Material Metal = Mat("Metal", new Color(0.6f, 0.62f, 0.66f));
            public readonly Material Wood = Mat("Wood", new Color(0.55f, 0.38f, 0.22f));
            public readonly Material Checkpoint = Mat("Checkpoint", new Color(0.5f, 0.5f, 0.55f));
            public readonly Material Pad = Mat("Pad", new Color(0.35f, 0.4f, 0.5f));
            public readonly Material Accent = Mat("Accent", new Color(0.95f, 0.75f, 0.2f));
            public readonly Material Stone = Mat("Stone", new Color(0.35f, 0.25f, 0.45f));
            public readonly Material AuraMat = Mat("AuraMat", new Color(0.2f, 0.65f, 0.7f));
            public readonly Material RenMat = Mat("RenMat", new Color(0.85f, 0.45f, 0.15f));
            public readonly Material ArenaFloor = Mat("ArenaFloor", new Color(0.6f, 0.55f, 0.45f));
            public readonly Material Wall = Mat("Wall", new Color(0.45f, 0.42f, 0.4f));
            public readonly Material Guard = Mat("Guard", new Color(0.3f, 0.55f, 1f));
        }

        /// <summary>Unlit particle material for impact sparks (saved as an asset so builds include the shader).</summary>
        static Material ParticleMaterial()
        {
            string path = $"{MaterialDir}/ImpactSpark.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return null;
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Material Mat(string name, Color color)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ Helpers

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            else if (type == PrimitiveType.Cylinder)
            {
                // A cylinder primitive comes with a capsule collider; squashed into a pad or disc it becomes a sphere
                // as wide as the disc (a 1.6 m pad stood 0.8 m tall), so use the cylinder's own shape instead.
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            }
            return go;
        }

        static GameObject Empty(string name, Transform parent, Vector3 position, float yaw, bool local = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (local) go.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            else go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        static SerializedProperty Prop(SerializedObject so, string field)
        {
            var p = so.FindProperty(field);
            if (p == null) Debug.LogError($"[Hunter Awakening] Field '{field}' not found on {so.targetObject.GetType().Name}.");
            return p;
        }

        static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetRefs(Object target, string field, IList<Object> values)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetInt(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            if (p == null) return;
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
