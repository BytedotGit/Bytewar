using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SurvivalRPG.Editor
{
    /// <summary>
    /// Dev-only safety rail:
    /// - If Mixamo FBX files exist locally, ensures the generated player prefab/animator use them.
    /// - If missing, logs exactly which file is missing and what to do.
    ///
    /// Rationale: Mixamo FBX files are intentionally not committed in this public repo.
    /// Without an enforcer, it's easy to stay stuck on the fallback model during dev.
    /// </summary>
    [InitializeOnLoad]
    public static class MixamoDevEnforcer
    {
        private const string SessionKey = "Bytewar_MixamoDevEnforcer_Ran";

        static MixamoDevEnforcer()
        {
            // Run once per Editor session.
            if (SessionState.GetBool(SessionKey, false))
                return;

            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += Run;
        }

        [MenuItem("SurvivalRPG/Diagnostics/Mixamo/Report Status")]
        public static void ReportStatusMenu()
        {
            Run(reportOnly: true);
        }

        private static void Run()
        {
            Run(reportOnly: false);
        }

        private static void Run(bool reportOnly)
        {
            if (!MixamoLocalAssets.AreAvailable(out string reason))
            {
                var missing = MixamoLocalAssets.GetMissingFiles();
                string missingSummary = (missing != null && missing.Length > 0)
                    ? (" Missing:\n - " + string.Join("\n - ", missing))
                    : string.Empty;

                Debug.LogWarning($"[MixamoDevEnforcer] Mixamo not available locally: {reason} " +
                                 $"(expected in {MixamoLocalAssets.MixamoFolder}). Using fallback model. " +
                                 "To enable high-res character in dev, import the Mixamo FBX files listed in the folder README." +
                                 missingSummary);
                return;
            }

            // Mixamo exists locally; ensure generated prefabs actually reference it.
            bool usesMixamo = PrefabUsesMixamo("Assets/GeneratedPrefabs/NetworkPlayer.prefab");
            Debug.Log($"[MixamoDevEnforcer] Mixamo available locally. NetworkPlayer usesMixamo={usesMixamo}.");

            if (reportOnly) return;
            if (usesMixamo) return;

            Debug.LogWarning("[MixamoDevEnforcer] Mixamo is available but generated prefabs are still using fallback. Regenerating animator + prefabs...");

            // Ensure avatar/clips are processed, then regenerate animator + prefabs.
            MixamoProcessor.ProcessAssets();
            AnimatorGenerator.GenerateAnimatorController();
            PrefabGenerator.GeneratePrefabs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            bool nowUsesMixamo = PrefabUsesMixamo("Assets/GeneratedPrefabs/NetworkPlayer.prefab");
            Debug.Log($"[MixamoDevEnforcer] Regeneration complete. NetworkPlayer usesMixamo={nowUsesMixamo}.");
        }

        private static bool PrefabUsesMixamo(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return false;

            // Dependencies contain the FBX path when referenced.
            var deps = AssetDatabase.GetDependencies(prefabPath, recursive: true);
            return deps.Any(d => d.StartsWith(MixamoLocalAssets.MixamoFolder));
        }
    }
}
