using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ByteWar.Editor
{
    internal static class LargeTreeProductionMaterialPipeline
    {
        internal const string ArtAssetFolder = "Assets/Art/Environment/Vegetation/LargeTree";
        internal const string ArtTextureFolder = "Assets/Art/Environment/Vegetation/LargeTree/Textures";
        internal const string ArtMaterialFolder = "Assets/Art/Environment/Vegetation/LargeTree/Materials";
        internal const string BarkAlbedoTexturePath = "Assets/Art/Environment/Vegetation/LargeTree/Textures/LargeTree_Bark_Albedo.png";
        internal const string LeavesAlbedoTexturePath = "Assets/Art/Environment/Vegetation/LargeTree/Textures/LargeTree_Leaves_Albedo.png";
        internal const string BarkMaterialPath = "Assets/Art/Environment/Vegetation/LargeTree/Materials/LargeTree_Bark.mat";
        internal const string LeavesMaterialPath = "Assets/Art/Environment/Vegetation/LargeTree/Materials/LargeTree_Leaves.mat";

        internal static bool TryEnsureAndAssign(GameObject modelRoot, out string message)
        {
            EnsureFolder(ArtAssetFolder, "Textures");
            EnsureFolder(ArtAssetFolder, "Materials");

            var barkTexture = SaveAndImportAlbedoTexture(BarkAlbedoTexturePath, BuildBarkAlbedoTexture(256, 256));
            var leavesTexture = SaveAndImportAlbedoTexture(LeavesAlbedoTexturePath, BuildLeavesAlbedoTexture(256, 256));

            if (barkTexture == null || leavesTexture == null)
            {
                message = "Production texture generation failed for LargeTree.";
                return false;
            }

            var barkMaterial = EnsureStandardMaterial(BarkMaterialPath, barkTexture, new Color(0.34f, 0.25f, 0.17f), 0.0f, 0.30f);
            var leavesMaterial = EnsureStandardMaterial(LeavesMaterialPath, leavesTexture, new Color(0.84f, 0.95f, 0.84f), 0.0f, 0.15f);

            if (barkMaterial == null || leavesMaterial == null)
            {
                message = "Production material creation failed for LargeTree.";
                return false;
            }

            if (!TryAssignTreeMaterials(modelRoot, barkMaterial, leavesMaterial, out int rendererCount, out int barkSlots, out int leavesSlots))
            {
                message = $"Tree material assignment failed (renderers={rendererCount}, barkSlots={barkSlots}, leavesSlots={leavesSlots}).";
                return false;
            }

            message = $"Production materials assigned (renderers={rendererCount}, barkSlots={barkSlots}, leavesSlots={leavesSlots}).";
            return true;
        }

        private static Texture2D SaveAndImportAlbedoTexture(string assetPath, Texture2D texture)
        {
            try
            {
                byte[] bytes = texture.EncodeToPNG();
                File.WriteAllBytes(assetPath, bytes);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Texture2D BuildBarkAlbedoTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var dark = new Color(0.17f, 0.12f, 0.08f, 1.0f);
            var mid = new Color(0.31f, 0.22f, 0.14f, 1.0f);
            var light = new Color(0.43f, 0.30f, 0.19f, 1.0f);

            for (int y = 0; y < height; y++)
            {
                float v = height <= 1 ? 0.0f : y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = width <= 1 ? 0.0f : x / (float)(width - 1);
                    float grain = Mathf.PerlinNoise(u * 18.0f, v * 4.0f);
                    float detail = Mathf.PerlinNoise(u * 48.0f + 11.0f, v * 12.0f + 7.0f);
                    float ridges = Mathf.Abs(Mathf.Sin((u + detail * 0.17f) * Mathf.PI * 20.0f));
                    float blend = Mathf.Clamp01(grain * 0.55f + detail * 0.25f + ridges * 0.20f);
                    Color color = Color.Lerp(dark, mid, blend);
                    color = Color.Lerp(color, light, Mathf.Clamp01((detail - 0.55f) * 2.0f));
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D BuildLeavesAlbedoTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var dark = new Color(0.12f, 0.32f, 0.09f, 1.0f);
            var mid = new Color(0.24f, 0.50f, 0.16f, 1.0f);
            var light = new Color(0.42f, 0.68f, 0.28f, 1.0f);

            for (int y = 0; y < height; y++)
            {
                float v = height <= 1 ? 0.0f : y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = width <= 1 ? 0.0f : x / (float)(width - 1);
                    float macro = Mathf.PerlinNoise(u * 6.0f, v * 6.0f);
                    float micro = Mathf.PerlinNoise(u * 38.0f + 4.0f, v * 38.0f + 9.0f);
                    float veins = Mathf.PerlinNoise(u * 3.0f + 40.0f, v * 22.0f + 5.0f);
                    float blend = Mathf.Clamp01(macro * 0.55f + micro * 0.30f + veins * 0.15f);
                    Color color = Color.Lerp(dark, mid, blend);
                    color = Color.Lerp(color, light, Mathf.Clamp01((micro - 0.60f) * 2.4f));
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return texture;
        }

        private static Material EnsureStandardMaterial(string materialPath, Texture2D albedo, Color tint, float metallic, float smoothness)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var shader = FindStandardShader();

            Material material = existing;
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
                material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            }

            if (material == null)
                return null;

            if (material.shader == null || material.shader.name != shader.name)
                material.shader = shader;

            material.color = tint;
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", albedo);
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Glossiness"))
                material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);

            EditorUtility.SetDirty(material);
            AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceUpdate);
            return material;
        }

        private static Shader FindStandardShader()
        {
            var shader = Shader.Find("Standard");
            if (shader != null)
                return shader;

            shader = Shader.Find("Diffuse");
            if (shader != null)
                return shader;

            return Shader.Find("Hidden/InternalErrorShader");
        }

        private static bool TryAssignTreeMaterials(GameObject modelRoot, Material barkMaterial, Material leavesMaterial, out int rendererCount, out int barkSlots, out int leavesSlots)
        {
            rendererCount = 0;
            barkSlots = 0;
            leavesSlots = 0;

            MeshRenderer firstRenderer = null;
            Material[] firstAssigned = null;

            foreach (var renderer in modelRoot.GetComponentsInChildren<MeshRenderer>(includeInactive: true))
            {
                if (renderer == null)
                    continue;

                string rendererName = renderer.gameObject.name;
                if (rendererName.EndsWith("_COL", StringComparison.OrdinalIgnoreCase) || rendererName.EndsWith("_TRIG", StringComparison.OrdinalIgnoreCase))
                    continue;

                var source = renderer.sharedMaterials;
                if (source == null || source.Length == 0)
                    continue;

                rendererCount++;
                var assigned = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    string sourceName = source[i] != null ? source[i].name : string.Empty;
                    if (IsLeavesMaterialName(sourceName))
                    {
                        assigned[i] = leavesMaterial;
                        leavesSlots++;
                    }
                    else if (IsBarkMaterialName(sourceName))
                    {
                        assigned[i] = barkMaterial;
                        barkSlots++;
                    }
                    else
                    {
                        assigned[i] = i == 0 ? barkMaterial : leavesMaterial;
                        if (i == 0)
                            barkSlots++;
                        else
                            leavesSlots++;
                    }
                }

                renderer.sharedMaterials = assigned;

                if (firstRenderer == null)
                {
                    firstRenderer = renderer;
                    firstAssigned = assigned;
                }
            }

            if (rendererCount == 0)
                return false;

            // Some generated variations can arrive with all slots named like leaves.
            // Enforce at least one bark/leaves slot so assignment is always usable.
            if (barkSlots == 0 && firstRenderer != null && firstAssigned != null && firstAssigned.Length > 0)
            {
                if (ReferenceEquals(firstAssigned[0], leavesMaterial))
                    leavesSlots = Mathf.Max(0, leavesSlots - 1);

                firstAssigned[0] = barkMaterial;
                barkSlots++;
                firstRenderer.sharedMaterials = firstAssigned;
            }

            if (leavesSlots == 0 && firstRenderer != null && firstAssigned != null && firstAssigned.Length > 0)
            {
                int targetIndex = firstAssigned.Length > 1 ? 1 : 0;
                if (ReferenceEquals(firstAssigned[targetIndex], barkMaterial))
                    barkSlots = Mathf.Max(0, barkSlots - 1);

                firstAssigned[targetIndex] = leavesMaterial;
                leavesSlots++;
                firstRenderer.sharedMaterials = firstAssigned;
            }

            return barkSlots > 0 && leavesSlots > 0;
        }

        private static bool IsBarkMaterialName(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
                return false;

            string n = materialName.ToLowerInvariant();
            return n.Contains("bark") || n.Contains("trunk") || n.Contains("wood");
        }

        private static bool IsLeavesMaterialName(string materialName)
        {
            if (string.IsNullOrEmpty(materialName))
                return false;

            string n = materialName.ToLowerInvariant();
            return n.Contains("leaf") || n.Contains("leaves") || n.Contains("canopy") || n.Contains("foliage");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = string.IsNullOrEmpty(parent) ? name : $"{parent}/{name}";
            if (AssetDatabase.IsValidFolder(path))
                return;

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                string[] parts = parent.Split('/');
                if (parts.Length >= 2)
                {
                    string current = parts[0];
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string next = $"{current}/{parts[i]}";
                        if (!AssetDatabase.IsValidFolder(next))
                            AssetDatabase.CreateFolder(current, parts[i]);
                        current = next;
                    }
                }
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}