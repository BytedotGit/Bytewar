using UnityEngine;
using UnityEditor;

namespace ByteWar.Editor
{
    /// <summary>
    /// Sets up a <c>PostProcessVolume</c> (legacy <c>com.unity.postprocessing</c>) in the scene
    /// with Bloom and ColorGrading effects, and installs a <c>PostProcessLayer</c> on the main
    /// camera.
    ///
    /// The volume profile asset is saved to <c>Assets/Resources/PostProcessProfile.asset</c>
    /// so it is included in builds and loadable via <c>Resources.Load</c> at runtime.
    ///
    /// Called by <see cref="SceneGenerator"/> after the camera is created, and also via
    /// the ByteWar menu for manual regeneration.
    /// </summary>
    public static class PostProcessingSetup
    {
        private const string ProfilePath = "Assets/Resources/PostProcessProfile.asset";

        [MenuItem("ByteWar/Setup Post-Processing")]
        public static void SetupPostProcessing()
        {
            Debug.Log("[PostProcessingSetup] Configuring post-processing...");

#if UNITY_POST_PROCESSING_STACK_V2
            SetupPostProcessInternal();
#else
            Debug.LogWarning("[PostProcessingSetup] com.unity.postprocessing package not imported yet. " +
                             "Open Unity once so the package resolver can install it, then regenerate.");
#endif
        }

        public static void SetupPostProcessInternal()
        {
#if UNITY_POST_PROCESSING_STACK_V2
            // ── 1. Profile ────────────────────────────────────────────────────────

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            UnityEngine.Rendering.PostProcessing.PostProcessProfile profile =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.PostProcessing.PostProcessProfile>(ProfilePath);

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.PostProcessing.PostProcessProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            // Bloom
            if (!profile.TryGetSettings<UnityEngine.Rendering.PostProcessing.Bloom>(out var bloom))
            {
                bloom = profile.AddSettings<UnityEngine.Rendering.PostProcessing.Bloom>();
            }
            bloom.enabled.value     = true;
            bloom.intensity.value   = 0.6f;
            bloom.threshold.value   = 0.9f;
            bloom.softKnee.value    = 0.5f;
            bloom.intensity.overrideState   = true;
            bloom.threshold.overrideState   = true;
            bloom.softKnee.overrideState    = true;

            // Color Grading
            if (!profile.TryGetSettings<UnityEngine.Rendering.PostProcessing.ColorGrading>(out var cg))
            {
                cg = profile.AddSettings<UnityEngine.Rendering.PostProcessing.ColorGrading>();
            }
            cg.enabled.value        = true;
            cg.temperature.value    = -5f;
            cg.saturation.value     = 10f;
            cg.contrast.value       = 15f;
            cg.temperature.overrideState    = true;
            cg.saturation.overrideState     = true;
            cg.contrast.overrideState       = true;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            // ── 2. Volume in scene ────────────────────────────────────────────────

            GameObject volumeObj = new GameObject("PostProcessVolume");
            var volume = volumeObj.AddComponent<UnityEngine.Rendering.PostProcessing.PostProcessVolume>();
            volume.isGlobal = true;
            volume.profile  = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.PostProcessing.PostProcessProfile>(ProfilePath);
            volume.weight   = 1f;

            Debug.Log("[PostProcessingSetup] PostProcessVolume (global) added to scene.");

            // ── 3. Layer on camera ────────────────────────────────────────────────

            GameObject camObj = GameObject.FindWithTag("MainCamera");
            if (camObj != null)
            {
                if (camObj.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>() == null)
                {
                    var layer = camObj.AddComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>();
                    layer.volumeLayer = LayerMask.GetMask("Everything") != 0
                        ? ~0
                        : -1;   // fallback to all
                    layer.antialiasingMode =
                        UnityEngine.Rendering.PostProcessing.PostProcessLayer.Antialiasing.TemporalAntialiasing;
                    Debug.Log("[PostProcessingSetup] PostProcessLayer added to Main Camera.");
                }
                else
                {
                    Debug.Log("[PostProcessingSetup] PostProcessLayer already present on Main Camera.");
                }
            }
            else
            {
                Debug.LogWarning("[PostProcessingSetup] Main Camera not found. PostProcessLayer not added.");
            }
#endif
        }
    }
}
