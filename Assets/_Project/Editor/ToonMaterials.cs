using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HunterAwakening.EditorTools
{
    /// <summary>
    /// Gives characters the anime look: each opaque material on a model is swapped for a HunterAwakening/Toon copy
    /// that keeps its texture and colour (saved per character, so rebuilding the scene updates them in place).
    /// </summary>
    static class ToonMaterials
    {
        const string Dir = "Assets/_Project/Art/Characters/Toon";
        const string ShaderPath = "Assets/_Project/Art/Shaders/Toon.shader";

        public static void Apply(GameObject model, string character)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogWarning($"[Hunter Awakening] {ShaderPath} missing; characters keep their Lit materials.");
                return;
            }
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Project/Art/Characters", "Toon");
            if (!AssetDatabase.IsValidFolder($"{Dir}/{character}")) AssetDatabase.CreateFolder(Dir, character);

            var converted = new Dictionary<Material, Material>();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (source == null || source.shader == shader) continue;
                    // Transparent surfaces (glass, effects) keep their own shading.
                    if (source.HasProperty("_Surface") && source.GetFloat("_Surface") > 0.5f) continue;
                    if (!converted.TryGetValue(source, out var toon))
                        converted[source] = toon = ToonCopy(source, character, shader);
                    materials[i] = toon;
                }
                r.sharedMaterials = materials;
            }
        }

        static Material ToonCopy(Material source, string character, Shader shader)
        {
            string name = string.Join("_", source.name.Split(System.IO.Path.GetInvalidFileNameChars()));
            string path = $"{Dir}/{character}/{name}.mat";
            var toon = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (toon == null)
            {
                toon = new Material(shader);
                AssetDatabase.CreateAsset(toon, path);
            }
            toon.shader = shader;
            var texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.mainTexture;
            toon.SetTexture("_BaseMap", texture);
            if (source.HasProperty("_BaseMap"))
            {
                toon.SetTextureScale("_BaseMap", source.GetTextureScale("_BaseMap"));
                toon.SetTextureOffset("_BaseMap", source.GetTextureOffset("_BaseMap"));
            }
            toon.SetColor("_BaseColor", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color);
            EditorUtility.SetDirty(toon);
            return toon;
        }
    }
}
