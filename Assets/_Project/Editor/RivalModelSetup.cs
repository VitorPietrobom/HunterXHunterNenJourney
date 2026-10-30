using UnityEditor;
using UnityEngine;

namespace HunterAwakening.EditorTools
{
    /// <summary>
    /// Imports the opponent characters as Humanoid. Each one is rigged on Mixamo (upload the prepared FBX, download
    /// "FBX for Unity", T-pose, with skin) and saved as Rivals/&lt;Name&gt;/&lt;Name&gt;.fbx. Mixamo's skeleton matches the
    /// Mixamo clips, so Unity's automatic Humanoid mapping is enough.
    /// </summary>
    public static class RivalModelSetup
    {
        public const string Dir = "Assets/_Project/Art/Characters/Rivals";

        /// <summary>Character, the tier it plays, and its in-game height (the player's Killua stands 1.9 m).</summary>
        public static readonly (string name, string tierId, float height)[] Characters =
        {
            ("Gon", "rookie", 1.85f),
            ("Kurapika", "rival", 2.05f),
            ("Hisoka", "veteran", 2.25f),
            ("Meruem", "elite", 2.6f),
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
