using UnityEditor;
using UnityEngine;

namespace HunterAwakening.EditorTools
{
    /// <summary>
    /// Imports the rigged characters (the player's Gon and the opponents) as Humanoid. Each is rigged by
    /// Rivals/autorig.py.txt (or on Mixamo) with Mixamo bone names and saved as Rivals/&lt;Name&gt;/&lt;Name&gt;.fbx, so
    /// Unity's automatic Humanoid mapping is enough.
    /// </summary>
    public static class RivalModelSetup
    {
        public const string Dir = "Assets/_Project/Art/Characters/Rivals";

        /// <summary>
        /// Character, the tier it plays, and its height target (the player's Gon uses 2.0; heights are relative, so
        /// Gon and Killua match and the older fighters tower over them).
        /// </summary>
        public static readonly (string name, string tierId, float height)[] Characters =
        {
            ("Killua", "rookie", 2f),
            ("Kurapika", "rival", 2.2f),
            ("Hisoka", "veteran", 2.45f),
            ("Killua", "elite", 2f), // Killua in Godspeed; Meruem is the castle's King (PrototypeSceneBuilder.World)
        };

        public static string ModelPath(string name) => $"{Dir}/{name}/{name}.fbx";

        /// <summary>Imports the character as Humanoid. Returns its avatar, or null if the file is missing or not humanoid.</summary>
        public static Avatar Configure(string name)
        {
            string path = ModelPath(name);
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return null;

            if (importer.animationType != ModelImporterAnimationType.Human
                || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
                || importer.importAnimation)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = false; // all motion comes from the shared Mixamo clips
                importer.importCameras = false;
                importer.importLights = false;
                importer.SaveAndReimport();
            }

            // Textures embedded in the FBX are only linked to its materials once they exist as assets.
            string textureDir = $"{Dir}/{name}/Textures";
            if (!AssetDatabase.IsValidFolder(textureDir))
            {
                AssetDatabase.CreateFolder($"{Dir}/{name}", "Textures");
                if (importer.ExtractTextures(textureDir))
                {
                    AssetDatabase.Refresh();
                    importer.SaveAndReimport();
                }
            }

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(asset is Avatar avatar)) continue;
                if (avatar.isValid && avatar.isHuman) return avatar;
                Debug.LogWarning($"[Hunter Awakening] {path} did not map to a Humanoid avatar; is it the Mixamo-rigged file?");
                return null;
            }
            return null;
        }
    }
}
