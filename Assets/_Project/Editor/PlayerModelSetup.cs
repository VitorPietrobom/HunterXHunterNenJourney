using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HunterAwakening.EditorTools
{
    /// <summary>
    /// Configures the game-ready Killua FBX (see Art/Characters/Killua/export_killua_game.py.txt) as a Humanoid
    /// with an explicit Rigify DEF-bone mapping, so any humanoid clip (Mixamo, Asset Store, ...) can drive it.
    /// </summary>
    public static class PlayerModelSetup
    {
        public const string ModelPath = "Assets/_Project/Art/Characters/Killua/Killua_Game.fbx";

        /// <summary>
        /// Clip whose avatar supplies the reference pose to match. All Mixamo downloads share the same T-pose, and
        /// matching it bone for bone keeps retargeting consistent (Mixamo's T-pose has splayed legs and toed-out feet).
        /// </summary>
        const string ReferencePoseClip = "Idle";

        static readonly (string from, string to)[] ReferenceChains =
        {
            ("Hips", "Spine"), ("Spine", "Chest"), ("Chest", "UpperChest"), ("UpperChest", "Neck"), ("Neck", "Head"),
            ("LeftShoulder", "LeftUpperArm"), ("LeftUpperArm", "LeftLowerArm"), ("LeftLowerArm", "LeftHand"),
            ("RightShoulder", "RightUpperArm"), ("RightUpperArm", "RightLowerArm"), ("RightLowerArm", "RightHand"),
        };

        static readonly (string human, string bone)[] Mapping =
        {
            ("Hips", "DEF-spine"), ("Spine", "DEF-spine.001"), ("Chest", "DEF-spine.002"), ("UpperChest", "DEF-spine.003"),
            ("Neck", "DEF-spine.004"), ("Head", "DEF-spine.006"),
            ("LeftShoulder", "DEF-shoulder.L"), ("LeftUpperArm", "DEF-upper_arm.L"), ("LeftLowerArm", "DEF-forearm.L"), ("LeftHand", "DEF-hand.L"),
            ("RightShoulder", "DEF-shoulder.R"), ("RightUpperArm", "DEF-upper_arm.R"), ("RightLowerArm", "DEF-forearm.R"), ("RightHand", "DEF-hand.R"),
            ("LeftUpperLeg", "DEF-thigh.L"), ("LeftLowerLeg", "DEF-shin.L"), ("LeftFoot", "DEF-foot.L"), ("LeftToes", "DEF-toe.L"),
            ("RightUpperLeg", "DEF-thigh.R"), ("RightLowerLeg", "DEF-shin.R"), ("RightFoot", "DEF-foot.R"), ("RightToes", "DEF-toe.R"),
            ("Left Thumb Proximal", "DEF-thumb.01.L"), ("Left Thumb Intermediate", "DEF-thumb.02.L"), ("Left Thumb Distal", "DEF-thumb.03.L"),
            ("Left Index Proximal", "DEF-f_index.01.L"), ("Left Index Intermediate", "DEF-f_index.02.L"), ("Left Index Distal", "DEF-f_index.03.L"),
            ("Left Middle Proximal", "DEF-f_middle.01.L"), ("Left Middle Intermediate", "DEF-f_middle.02.L"), ("Left Middle Distal", "DEF-f_middle.03.L"),
            ("Left Ring Proximal", "DEF-f_ring.01.L"), ("Left Ring Intermediate", "DEF-f_ring.02.L"), ("Left Ring Distal", "DEF-f_ring.03.L"),
            ("Left Little Proximal", "DEF-f_pinky.01.L"), ("Left Little Intermediate", "DEF-f_pinky.02.L"), ("Left Little Distal", "DEF-f_pinky.03.L"),
            ("Right Thumb Proximal", "DEF-thumb.01.R"), ("Right Thumb Intermediate", "DEF-thumb.02.R"), ("Right Thumb Distal", "DEF-thumb.03.R"),
            ("Right Index Proximal", "DEF-f_index.01.R"), ("Right Index Intermediate", "DEF-f_index.02.R"), ("Right Index Distal", "DEF-f_index.03.R"),
            ("Right Middle Proximal", "DEF-f_middle.01.R"), ("Right Middle Intermediate", "DEF-f_middle.02.R"), ("Right Middle Distal", "DEF-f_middle.03.R"),
            ("Right Ring Proximal", "DEF-f_ring.01.R"), ("Right Ring Intermediate", "DEF-f_ring.02.R"), ("Right Ring Distal", "DEF-f_ring.03.R"),
            ("Right Little Proximal", "DEF-f_pinky.01.R"), ("Right Little Intermediate", "DEF-f_pinky.02.R"), ("Right Little Distal", "DEF-f_pinky.03.R"),
        };

        [MenuItem("Hunter Awakening/Configure Player Model (Humanoid)", priority = 10)]
        public static void ConfigureFromMenu()
        {
            var avatar = Configure();
            if (avatar != null) Debug.Log($"[Hunter Awakening] {ModelPath}: avatar valid={avatar.isValid}, human={avatar.isHuman}");
        }

        /// <summary>Imports the model as Humanoid with the explicit mapping. Returns the resulting avatar (or null).</summary>
        public static Avatar Configure()
        {
            if (!(AssetImporter.GetAtPath(ModelPath) is ModelImporter importer))
            {
                Debug.LogError($"[Hunter Awakening] Missing {ModelPath}");
                return null;
            }

            // Generic pass first so the skeleton's rest pose can be read from the imported hierarchy.
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importCameras = false;
            importer.importLights = false;
            importer.SaveAndReimport();

            // Work on a temporary copy: the avatar's reference pose is the file's rest pose straightened into a T-pose.
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            var transforms = model.GetComponentsInChildren<Transform>(true);
            var byName = new Dictionary<string, Transform>();
            foreach (var t in transforms) byName[t.name] = t;
            PoseAsTPose(byName);
            MatchReferencePose(byName);

            var human = new List<HumanBone>();
            foreach (var (humanName, boneName) in Mapping)
            {
                if (!byName.ContainsKey(boneName))
                {
                    Debug.LogWarning($"[Hunter Awakening] Bone {boneName} not found for {humanName}");
                    continue;
                }
                human.Add(new HumanBone { humanName = humanName, boneName = boneName, limit = new HumanLimit { useDefaultValues = true } });
            }

            var skeleton = new List<SkeletonBone>();
            foreach (var t in transforms)
                skeleton.Add(new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
            Object.DestroyImmediate(model);

            var description = importer.humanDescription;
            description.human = human.ToArray();
            description.skeleton = skeleton.ToArray();
            description.hasTranslationDoF = false;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.humanDescription = description;
            importer.importAnimation = false; // all motion comes from the Mixamo clips
            importer.SaveAndReimport();

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
                if (asset is Avatar avatar) return avatar;
            return null;
        }

        // ------------------------------------------------------------------ Mixamo clips

        public const string AnimationDir = "Assets/_Project/Art/Characters/Killua/Animations";

        /// <summary>One Mixamo FBX per state. bakeHeight=false discards vertical root motion (the motor owns jumping).</summary>
        public static readonly (string file, bool loop, bool bakeHeight)[] Clips =
        {
            ("Idle", true, true), ("Run", true, true), ("Sprint", true, true), ("Jump", false, false),
            ("Dodge", false, true), ("PunchLight", false, true), ("PunchHeavy", false, true), ("Block", false, true),
            ("HitReact", false, true), ("KnockedDown", false, true), ("SitIdle", true, true), ("Squat", false, true),
        };

        [MenuItem("Hunter Awakening/Configure Player Animations", priority = 11)]
        public static void ConfigureAnimationsFromMenu() => ConfigureAnimations();

        /// <summary>
        /// Imports every clip as Humanoid (Mixamo rigs auto-map) with in-place root settings: rotation and height are
        /// baked into the pose, horizontal root motion is left on the root, where the Animator discards it
        /// (applyRootMotion is off — the CharacterController moves the player).
        /// </summary>
        public static void ConfigureAnimations()
        {
            foreach (var (file, loop, bakeHeight) in Clips)
            {
                string path = $"{AnimationDir}/{file}.fbx";
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
                {
                    Debug.LogWarning($"[Hunter Awakening] Missing animation {path}");
                    continue;
                }
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;

                var source = importer.defaultClipAnimations;
                if (source.Length == 0)
                {
                    importer.SaveAndReimport();
                    source = importer.defaultClipAnimations;
                }
                if (source.Length == 0)
                {
                    Debug.LogWarning($"[Hunter Awakening] No take found in {path}");
                    continue;
                }
                var clip = source[0];
                clip.name = file;
                clip.loopTime = loop;
                clip.loopPose = loop;
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = bakeHeight;
                // Mixamo and Killua have different leg proportions; anchoring height to the feet keeps soles on the floor.
                clip.keepOriginalPositionY = false;
                clip.heightFromFeet = true;
                clip.lockRootPositionXZ = false;
                importer.clipAnimations = new[] { clip };
                importer.SaveAndReimport();
            }
        }

        /// <summary>Configures the clips once; later builds skip it unless a clip is not yet Humanoid.</summary>
        public static void EnsureAnimationsConfigured()
        {
            foreach (var (file, _, _) in Clips)
                if (AssetImporter.GetAtPath($"{AnimationDir}/{file}.fbx") is ModelImporter importer
                    && importer.animationType != ModelImporterAnimationType.Human)
                {
                    ConfigureAnimations();
                    return;
                }
        }

        /// <summary>Horizontal root speed of a clip at 1× playback (humanoid units), or the fallback if unavailable.</summary>
        public static float ClipSpeed(string file, float fallback)
        {
            var clip = LoadClip(file);
            return clip != null && clip.averageSpeed.magnitude > 0.01f ? clip.averageSpeed.magnitude : fallback;
        }

        public static AnimationClip LoadClip(string file)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath($"{AnimationDir}/{file}.fbx"))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
            return null;
        }

        /// <summary>
        /// Straightens the skeleton into a T-pose (facing +Z, left = -X): body upright, legs hanging straight, feet
        /// forward, clavicles and arms out. Unity retargets every Humanoid clip relative to this pose, so any lean left
        /// in it shows up in every animation. Whole chains are aimed (hips→head, hip→ankle) rather than each segment:
        /// the knee's natural bend is what tells Unity which way the knee hinges. The mesh's bind pose is unaffected.
        /// </summary>
        static void PoseAsTPose(Dictionary<string, Transform> bones)
        {
            // Rotates `bone` so that the direction from `from` to `to` (both below it) points along `direction`.
            void AimChain(string bone, string from, string to, Vector3 direction)
            {
                if (!bones.TryGetValue(bone, out var b) || !bones.TryGetValue(from, out var f) || !bones.TryGetValue(to, out var t)) return;
                Vector3 current = t.position - f.position;
                if (current.sqrMagnitude < 1e-8f) return;
                b.rotation = Quaternion.FromToRotation(current, direction) * b.rotation;
            }

            void Aim(string bone, string child, Vector3 direction) => AimChain(bone, bone, child, direction);

            void AimFootForward(string foot, string toe)
            {
                if (!bones.TryGetValue(foot, out var f) || !bones.TryGetValue(toe, out var t)) return;
                Vector3 current = t.position - f.position;
                float horizontal = new Vector2(current.x, current.z).magnitude;
                if (horizontal < 1e-4f) return;
                Aim(foot, toe, new Vector3(0f, current.y, horizontal)); // keep the foot's pitch, point it straight ahead
            }

            // Upright body, keeping the spine's natural curve.
            AimChain("DEF-spine", "DEF-spine", "DEF-spine.006", Vector3.up);

            foreach (var (side, outward) in new[] { ("L", Vector3.left), ("R", Vector3.right) })
            {
                AimChain($"DEF-thigh.{side}", $"DEF-thigh.{side}", $"DEF-foot.{side}", Vector3.down); // hip over ankle, knee bend kept
                AimFootForward($"DEF-foot.{side}", $"DEF-toe.{side}");
                Aim($"DEF-shoulder.{side}", $"DEF-upper_arm.{side}", outward);
                Aim($"DEF-upper_arm.{side}", $"DEF-forearm.{side}", outward);
                Aim($"DEF-forearm.{side}", $"DEF-hand.{side}", outward);
                Aim($"DEF-hand.{side}", $"DEF-f_middle.01.{side}", outward);
            }
        }

        /// <summary>
        /// Refines the T-pose so each limb points exactly like the Mixamo reference skeleton (both face +Z).
        /// Feet copy only the reference's heading (toe-out) and keep Killua's own pitch so his soles stay flat.
        /// Does nothing if the reference clip is missing.
        /// </summary>
        static void MatchReferencePose(Dictionary<string, Transform> bones)
        {
            string path = $"{AnimationDir}/{ReferencePoseClip}.fbx";
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(path);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (avatar == null || !avatar.isHuman || prefab == null) return;

            var reference = Object.Instantiate(prefab);
            try
            {
                reference.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var refByName = new Dictionary<string, Transform>();
                foreach (var t in reference.GetComponentsInChildren<Transform>(true)) refByName[t.name] = t;
                foreach (var sb in avatar.humanDescription.skeleton)
                    if (refByName.TryGetValue(sb.name, out var t) && t != reference.transform)
                    {
                        t.localPosition = sb.position;
                        t.localRotation = sb.rotation;
                    }

                var refHuman = new Dictionary<string, Transform>();
                foreach (var hb in avatar.humanDescription.human)
                    if (refByName.TryGetValue(hb.boneName, out var t)) refHuman[hb.humanName] = t;
                var ours = new Dictionary<string, Transform>();
                foreach (var (humanName, boneName) in Mapping)
                    if (bones.TryGetValue(boneName, out var t)) ours[humanName] = t;

                bool Has(string a, string b) => refHuman.ContainsKey(a) && refHuman.ContainsKey(b) && ours.ContainsKey(a) && ours.ContainsKey(b);

                foreach (var (from, to) in ReferenceChains)
                    if (Has(from, to))
                        AimBone(ours[from], ours[to], refHuman[to].position - refHuman[from].position);

                foreach (var side in new[] { "Left", "Right" })
                {
                    // Legs: match the reference's hip→ankle splay but keep Killua's own knee bend
                    // (copying Mixamo's knee bend made the shins track worse).
                    string upperLeg = side + "UpperLeg", foot = side + "Foot", toes = side + "Toes";
                    if (Has(upperLeg, foot))
                    {
                        Vector3 current = ours[foot].position - ours[upperLeg].position;
                        Vector3 target = refHuman[foot].position - refHuman[upperLeg].position;
                        ours[upperLeg].rotation = Quaternion.FromToRotation(current, target) * ours[upperLeg].rotation;
                    }
                    if (!Has(foot, toes)) continue;
                    Vector3 refDir = refHuman[toes].position - refHuman[foot].position;
                    Vector3 ourDir = ours[toes].position - ours[foot].position;
                    Vector3 heading = new Vector3(refDir.x, 0f, refDir.z).normalized;
                    float horizontal = new Vector2(ourDir.x, ourDir.z).magnitude;
                    AimBone(ours[foot], ours[toes], heading * horizontal + Vector3.up * ourDir.y);
                }
            }
            finally
            {
                Object.DestroyImmediate(reference);
            }
        }

        static void AimBone(Transform bone, Transform child, Vector3 direction)
        {
            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-8f || direction.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.FromToRotation(current, direction) * bone.rotation;
        }
    }
}
