using UnityEngine;
using UnityEditor;

namespace ByteWar.Editor
{
    /// <summary>
    /// Generates detailed character models using Unity primitives with metallic materials.
    /// GenerateHumanoidModel() → armoured knight intended for the player.
    /// GenerateEnemyModel()    → brutish orc creature for enemies.
    /// </summary>
    public static class CharacterGenerator
    {
        private const string BuildGenPrefix = "[BuildGen]";
        private const string ModelsPath = "Assets/GeneratedPrefabs/Models";

        // ──────────────────────────────────────────────────────────────────────────
        //  PLAYER — Armoured Knight
        // ──────────────────────────────────────────────────────────────────────────
        [MenuItem("ByteWar/Generate Humanoid Model")]
        public static void GenerateHumanoidModel()
        {
            Debug.Log($"{BuildGenPrefix} CharacterGenerator: generating armoured knight model...");
            EnsureFolder(ModelsPath);

            // Materials
            var skin = Mat("KnightSkin", new Color(0.88f, 0.70f, 0.52f), 0.00f, 0.30f);
            var steel = Mat("KnightSteel", new Color(0.48f, 0.50f, 0.54f), 0.85f, 0.75f);
            var darkSteel = Mat("KnightDark", new Color(0.22f, 0.24f, 0.28f), 0.80f, 0.65f);
            var gold = Mat("KnightGold", new Color(0.82f, 0.68f, 0.12f), 0.90f, 0.80f);
            var leather = Mat("KnightLeather", new Color(0.35f, 0.22f, 0.10f), 0.00f, 0.20f);
            var glow = Mat("KnightEye", new Color(0.20f, 0.70f, 1.00f), 0.00f, 1.00f, emissive: new Color(0.10f, 0.40f, 1.00f));
            var bladeMat = Mat("KnightBlade", new Color(0.80f, 0.84f, 0.90f), 1.00f, 0.95f);

            var root = new GameObject("HumanoidModel");

            // — Head & Helmet ——————————————————————————————————————————————————————
            P(root, "Helmet", PrimitiveType.Sphere, new Vector3(0f, 1.90f, 0f), V(0.60f, 0.65f, 0.60f), steel);
            P(root, "VisorBar", PrimitiveType.Cube, new Vector3(0f, 1.80f, 0.28f), V(0.48f, 0.13f, 0.12f), gold);
            P(root, "EyeL", PrimitiveType.Cube, new Vector3(-0.13f, 1.84f, 0.32f), V(0.13f, 0.05f, 0.04f), glow);
            P(root, "EyeR", PrimitiveType.Cube, new Vector3(0.13f, 1.84f, 0.32f), V(0.13f, 0.05f, 0.04f), glow);
            P(root, "Crest", PrimitiveType.Cylinder, new Vector3(0f, 2.22f, 0f), V(0.07f, 0.20f, 0.07f), gold);

            // — Neck & Gorget ——————————————————————————————————————————————————————
            P(root, "Gorget", PrimitiveType.Cylinder, new Vector3(0f, 1.62f, 0f), V(0.26f, 0.11f, 0.26f), darkSteel);

            // — Torso ——————————————————————————————————————————————————————————————
            P(root, "Chest", PrimitiveType.Cube, new Vector3(0f, 1.24f, 0f), V(0.78f, 0.58f, 0.44f), steel);
            P(root, "ChestL", PrimitiveType.Sphere, new Vector3(-0.20f, 1.25f, 0.22f), V(0.30f, 0.23f, 0.18f), darkSteel);
            P(root, "ChestR", PrimitiveType.Sphere, new Vector3(0.20f, 1.25f, 0.22f), V(0.30f, 0.23f, 0.18f), darkSteel);
            P(root, "Belt", PrimitiveType.Cube, new Vector3(0f, 0.74f, 0f), V(0.74f, 0.10f, 0.44f), gold);
            P(root, "Abs", PrimitiveType.Cube, new Vector3(0f, 0.90f, 0f), V(0.66f, 0.26f, 0.40f), leather);
            P(root, "Groin", PrimitiveType.Cube, new Vector3(0f, 0.58f, 0f), V(0.56f, 0.18f, 0.40f), leather);

            // — Shoulders ——————————————————————————————————————————————————————————
            P(root, "PaulL1", PrimitiveType.Sphere, new Vector3(-0.55f, 1.42f, 0f), V(0.32f, 0.25f, 0.30f), steel);
            P(root, "PaulL2", PrimitiveType.Sphere, new Vector3(-0.60f, 1.24f, 0f), V(0.24f, 0.20f, 0.24f), darkSteel);
            P(root, "PaulR1", PrimitiveType.Sphere, new Vector3(0.55f, 1.42f, 0f), V(0.32f, 0.25f, 0.30f), steel);
            P(root, "PaulR2", PrimitiveType.Sphere, new Vector3(0.60f, 1.24f, 0f), V(0.24f, 0.20f, 0.24f), darkSteel);

            // — Arms ———————————————————————————————————————————————————————————————
            P(root, "UArmL", PrimitiveType.Cylinder, new Vector3(-0.64f, 1.10f, 0f), V(0.17f, 0.30f, 0.17f), steel);
            P(root, "UArmR", PrimitiveType.Cylinder, new Vector3(0.64f, 1.10f, 0f), V(0.17f, 0.30f, 0.17f), steel);
            P(root, "LArmL", PrimitiveType.Cylinder, new Vector3(-0.64f, 0.77f, 0f), V(0.15f, 0.26f, 0.15f), leather);
            P(root, "LArmR", PrimitiveType.Cylinder, new Vector3(0.64f, 0.77f, 0f), V(0.15f, 0.26f, 0.15f), leather);
            P(root, "GloveL", PrimitiveType.Cube, new Vector3(-0.64f, 0.52f, 0f), V(0.20f, 0.14f, 0.16f), steel);
            P(root, "GloveR", PrimitiveType.Cube, new Vector3(0.64f, 0.52f, 0f), V(0.20f, 0.14f, 0.16f), steel);

            // — Legs ———————————————————————————————————————————————————————————————
            P(root, "ThighL", PrimitiveType.Cylinder, new Vector3(-0.22f, 0.28f, 0f), V(0.24f, 0.36f, 0.24f), steel);
            P(root, "ThighR", PrimitiveType.Cylinder, new Vector3(0.22f, 0.28f, 0f), V(0.24f, 0.36f, 0.24f), steel);
            P(root, "ShinL", PrimitiveType.Cylinder, new Vector3(-0.22f, -0.09f, 0.02f), V(0.20f, 0.30f, 0.20f), darkSteel);
            P(root, "ShinR", PrimitiveType.Cylinder, new Vector3(0.22f, -0.09f, 0.02f), V(0.20f, 0.30f, 0.20f), darkSteel);
            P(root, "BootL", PrimitiveType.Cube, new Vector3(-0.22f, -0.42f, 0.04f), V(0.24f, 0.13f, 0.30f), leather);
            P(root, "BootR", PrimitiveType.Cube, new Vector3(0.22f, -0.42f, 0.04f), V(0.24f, 0.13f, 0.30f), leather);

            // — Sword (right hand) ————————————————————————————————————————————————
            P(root, "SwGrip", PrimitiveType.Cylinder, new Vector3(0.74f, 0.53f, 0f), V(0.06f, 0.16f, 0.06f), leather);
            P(root, "SwGuard", PrimitiveType.Cube, new Vector3(0.74f, 0.39f, 0f), V(0.35f, 0.04f, 0.06f), gold);
            P(root, "SwBlade", PrimitiveType.Cube, new Vector3(0.74f, 0.08f, 0f), V(0.05f, 0.56f, 0.02f), bladeMat);
            P(root, "SwPommel", PrimitiveType.Sphere, new Vector3(0.74f, 0.63f, 0f), V(0.10f, 0.10f, 0.10f), gold);

            SavePrefab(root, $"{ModelsPath}/HumanoidModel.prefab");
            Debug.Log($"{BuildGenPrefix} CharacterGenerator: knight model saved.");
        }

        // ──────────────────────────────────────────────────────────────────────────
        //  ENEMY — Brutish Orc
        // ──────────────────────────────────────────────────────────────────────────
        public static string GenerateEnemyModel()
        {
            string prefabPath = $"{ModelsPath}/OrcModel.prefab";
            Debug.Log($"{BuildGenPrefix} CharacterGenerator: generating orc enemy model...");
            EnsureFolder(ModelsPath);

            var orcSkin = Mat("OrcSkin", new Color(0.22f, 0.42f, 0.12f), 0.00f, 0.20f);
            var orcDark = Mat("OrcDark", new Color(0.14f, 0.28f, 0.08f), 0.00f, 0.15f);
            var orcRed = Mat("OrcEye", new Color(1.00f, 0.15f, 0.05f), 0.00f, 0.80f, emissive: new Color(1.00f, 0.10f, 0.00f));
            var orcArmor = Mat("OrcArmor", new Color(0.32f, 0.26f, 0.18f), 0.10f, 0.25f);
            var orcTusk = Mat("OrcTusk", new Color(0.90f, 0.85f, 0.72f), 0.00f, 0.35f);
            var orcBone = Mat("OrcBone", new Color(0.75f, 0.70f, 0.60f), 0.00f, 0.30f);

            var root = new GameObject("OrcModel");

            // — Head (broad, low) ——————————————————————————————————————————————————
            P(root, "Head", PrimitiveType.Sphere, new Vector3(0f, 2.05f, 0f), V(0.80f, 0.65f, 0.72f), orcSkin);
            P(root, "BrowRidge", PrimitiveType.Cube, new Vector3(0f, 2.18f, 0.28f), V(0.70f, 0.12f, 0.28f), orcDark);
            P(root, "EyeL", PrimitiveType.Sphere, new Vector3(-0.18f, 2.10f, 0.36f), V(0.14f, 0.14f, 0.10f), orcRed);
            P(root, "EyeR", PrimitiveType.Sphere, new Vector3(0.18f, 2.10f, 0.36f), V(0.14f, 0.14f, 0.10f), orcRed);
            P(root, "TuskL", PrimitiveType.Cylinder, new Vector3(-0.16f, 1.82f, 0.30f), V(0.06f, 0.18f, 0.06f), orcTusk);
            P(root, "TuskR", PrimitiveType.Cylinder, new Vector3(0.16f, 1.82f, 0.30f), V(0.06f, 0.18f, 0.06f), orcTusk);
            P(root, "HornL", PrimitiveType.Cylinder, new Vector3(-0.30f, 2.35f, 0f), V(0.07f, 0.22f, 0.07f), orcBone);
            P(root, "HornR", PrimitiveType.Cylinder, new Vector3(0.30f, 2.35f, 0f), V(0.07f, 0.22f, 0.07f), orcBone);

            // — Torso (massive) ————————————————————————————————————————————————————
            P(root, "Torso", PrimitiveType.Cube, new Vector3(0f, 1.30f, 0f), V(1.05f, 0.80f, 0.60f), orcSkin);
            P(root, "Gut", PrimitiveType.Sphere, new Vector3(0f, 0.95f, 0.12f), V(0.85f, 0.60f, 0.55f), orcSkin);
            P(root, "ArmorVest", PrimitiveType.Cube, new Vector3(0f, 1.35f, 0.20f), V(0.80f, 0.55f, 0.18f), orcArmor);
            P(root, "BeltL", PrimitiveType.Cube, new Vector3(0f, 0.72f, 0f), V(0.90f, 0.10f, 0.50f), orcArmor);

            // — Arms (ape-like) ————————————————————————————————————————————————————
            P(root, "UArmL", PrimitiveType.Cylinder, new Vector3(-0.78f, 1.25f, 0f), V(0.28f, 0.42f, 0.28f), orcSkin);
            P(root, "UArmR", PrimitiveType.Cylinder, new Vector3(0.78f, 1.25f, 0f), V(0.28f, 0.42f, 0.28f), orcSkin);
            P(root, "LArmL", PrimitiveType.Cylinder, new Vector3(-0.82f, 0.75f, 0f), V(0.24f, 0.38f, 0.24f), orcSkin);
            P(root, "LArmR", PrimitiveType.Cylinder, new Vector3(0.82f, 0.75f, 0f), V(0.24f, 0.38f, 0.24f), orcSkin);
            P(root, "FistL", PrimitiveType.Sphere, new Vector3(-0.84f, 0.44f, 0f), V(0.30f, 0.26f, 0.30f), orcDark);
            P(root, "FistR", PrimitiveType.Sphere, new Vector3(0.84f, 0.44f, 0f), V(0.30f, 0.26f, 0.30f), orcDark);
            // Club weapon in right hand
            P(root, "Club", PrimitiveType.Cylinder, new Vector3(0.85f, 0.10f, 0f), V(0.14f, 0.50f, 0.14f), orcBone);
            P(root, "ClubHead", PrimitiveType.Sphere, new Vector3(0.85f, -0.22f, 0f), V(0.30f, 0.30f, 0.30f), orcBone);

            // — Legs ———————————————————————————————————————————————————————————————
            P(root, "ThighL", PrimitiveType.Cylinder, new Vector3(-0.28f, 0.30f, 0f), V(0.30f, 0.40f, 0.30f), orcSkin);
            P(root, "ThighR", PrimitiveType.Cylinder, new Vector3(0.28f, 0.30f, 0f), V(0.30f, 0.40f, 0.30f), orcSkin);
            P(root, "ShinL", PrimitiveType.Cylinder, new Vector3(-0.28f, -0.10f, 0f), V(0.26f, 0.35f, 0.26f), orcSkin);
            P(root, "ShinR", PrimitiveType.Cylinder, new Vector3(0.28f, -0.10f, 0f), V(0.26f, 0.35f, 0.26f), orcSkin);
            P(root, "FootL", PrimitiveType.Cube, new Vector3(-0.28f, -0.46f, 0.06f), V(0.30f, 0.14f, 0.36f), orcDark);
            P(root, "FootR", PrimitiveType.Cube, new Vector3(0.28f, -0.46f, 0.06f), V(0.30f, 0.14f, 0.36f), orcDark);

            SavePrefab(root, prefabPath);
            Debug.Log($"{BuildGenPrefix} CharacterGenerator: orc model saved path={prefabPath}");
            return prefabPath;
        }

        // ──────────────────────────────────────────────────────────────────────────
        //  Shared helpers
        // ──────────────────────────────────────────────────────────────────────────
        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        private static void P(GameObject parent, string partName, PrimitiveType prim,
            Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(prim);
            go.name = partName;
            go.transform.SetParent(parent.transform);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        private static Material Mat(string name, Color color, float metallic, float smoothness,
            Color emissive = default)
        {
            string path = $"{ModelsPath}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                AssetDatabase.DeleteAsset(path);

            // Try URP/Lit first, fall back to BIRP Standard
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            var mat = new Material(shader) { color = color };

            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

            if (emissive != default)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", emissive);
            }

            AssetDatabase.CreateAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static void SavePrefab(GameObject root, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string child = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}