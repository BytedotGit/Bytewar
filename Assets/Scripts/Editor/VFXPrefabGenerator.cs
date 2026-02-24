using UnityEngine;
using UnityEditor;
using System.IO;
using ByteWar.Core;

namespace ByteWar.Editor
{
    /// <summary>
    /// Generates 6 self-destructing particle VFX prefabs procedurally.
    /// Called by <see cref="AssetGenerator"/> and accessible via the ByteWar menu.
    /// All prefabs are saved to <c>Assets/GeneratedPrefabs/VFX/</c>.
    /// </summary>
    public static class VFXPrefabGenerator
    {
        private const string VFXFolder = "Assets/GeneratedPrefabs/VFX";

        [MenuItem("ByteWar/Generate VFX Prefabs")]
        public static void GenerateVFXPrefabs()
        {
            Debug.Log("[VFXPrefabGenerator] Generating VFX prefabs...");

            if (!AssetDatabase.IsValidFolder("Assets/GeneratedPrefabs"))
                AssetDatabase.CreateFolder("Assets", "GeneratedPrefabs");
            if (!AssetDatabase.IsValidFolder(VFXFolder))
                AssetDatabase.CreateFolder("Assets/GeneratedPrefabs", "VFX");

            GameObject muzzle    = CreateVFXPrefab("Fireball_MuzzleFlash",  30, new Color(1.0f, 0.55f, 0.0f), new Color(1.0f, 0.85f, 0.0f), 0.3f, 2f,  15f, burst: true);
            GameObject impact    = CreateVFXPrefab("Fireball_Impact",       40, new Color(1.0f, 0.20f, 0.0f), new Color(1.0f, 0.60f, 0.1f), 0.5f, 4f,  25f, burst: true);
            GameObject gatherHit = CreateVFXPrefab("GatherHit",             12, new Color(0.45f, 0.27f, 0.08f), new Color(0.55f, 0.37f, 0.15f), 0.4f, 1.5f, 5f, burst: true);
            GameObject resDeath  = CreateVFXPrefab("ResourceDeath",         25, new Color(0.45f, 0.40f, 0.35f), new Color(0.35f, 0.30f, 0.25f), 0.8f, 3f,  8f,  burst: true);
            GameObject building  = CreateVFXPrefab("BuildingPlace",         18, new Color(0.75f, 0.65f, 0.50f), new Color(0.85f, 0.75f, 0.60f), 0.6f, 2.5f, 4f, burst: true);
            GameObject pickup    = CreateVFXPrefab("ItemPickup",             8, new Color(1.0f, 0.85f, 0.0f),  new Color(1.0f, 1.0f, 0.50f),  0.5f, 1.2f, 6f,  burst: true);
            GameObject cleave    = CreateVFXPrefab("CleaveHit",             20, new Color(0.80f, 0.80f, 0.85f), new Color(0.60f, 0.60f, 0.70f), 0.4f, 3f,  12f, burst: true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[VFXPrefabGenerator] All VFX prefabs generated.");
        }

        // ── Returns the 6 generated prefabs for wiring into VFXManager ────────────

        public static (GameObject muzzle, GameObject impact, GameObject gather,
                       GameObject resDeath, GameObject building, GameObject pickup,
                       GameObject cleave)
        GetOrGeneratePrefabs()
        {
            if (!AssetDatabase.IsValidFolder(VFXFolder)) GenerateVFXPrefabs();
            return (
                Load("Fireball_MuzzleFlash"),
                Load("Fireball_Impact"),
                Load("GatherHit"),
                Load("ResourceDeath"),
                Load("BuildingPlace"),
                Load("ItemPickup"),
                Load("CleaveHit")
            );
        }

        private static GameObject Load(string name)
            => AssetDatabase.LoadAssetAtPath<GameObject>($"{VFXFolder}/{name}.prefab");

        // ── Prefab builder ────────────────────────────────────────────────────────

        private static GameObject CreateVFXPrefab(
            string prefabName,
            int maxParticles,
            Color colorStart, Color colorEnd,
            float lifetime, float radius, float speed,
            bool burst)
        {
            string path = $"{VFXFolder}/{prefabName}.prefab";

            // Remove stale prefab so we always regenerate deterministically
            if (File.Exists(Path.Combine(Application.dataPath, "../", path)))
                AssetDatabase.DeleteAsset(path);

            var go = new GameObject(prefabName);
            var ps = go.AddComponent<ParticleSystem>();

            // --- Main module ---
            var main = ps.main;
            main.loop              = false;
            main.startLifetime     = lifetime;
            main.startSpeed        = speed;
            main.startSize         = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startColor        = new ParticleSystem.MinMaxGradient(colorStart, colorEnd);
            main.maxParticles      = maxParticles;
            main.simulationSpace   = ParticleSystemSimulationSpace.World;
            main.stopAction        = ParticleSystemStopAction.Destroy;  // auto self-destruct

            // --- Emission ---
            var emission = ps.emission;
            emission.enabled = true;
            if (burst)
            {
                emission.rateOverTime = 0;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, maxParticles) });
            }
            else
            {
                emission.rateOverTime = maxParticles / lifetime;
            }

            // --- Shape ---
            var shape = ps.shape;
            shape.enabled     = true;
            shape.shapeType   = ParticleSystemShapeType.Sphere;
            shape.radius      = radius * 0.25f;
            shape.radiusThickness = 1f;

            // --- Colour over lifetime (fade out) ---
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            // Save prefab
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            Debug.Log($"[VFXPrefabGenerator] Created {path}");
            return prefab;
        }
    }
}
