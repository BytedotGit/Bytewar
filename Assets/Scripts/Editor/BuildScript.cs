using UnityEditor;
using UnityEngine;

namespace SurvivalRPG.Editor
{
    public class BuildScript
    {
        private const string BuildGenPrefix = "[BuildGen]";

        [MenuItem("SurvivalRPG/Build Windows Client")]
        public static void BuildWindowsClient()
        {
            Debug.Log($"{BuildGenPrefix} Starting Windows Client Build...");

            // Ensure builds keep both Legacy + New Input System enabled.
            // - IMGUI HUD buttons rely on legacy input.
            // - Movement/interaction rely on the new Input System.
            InputHandlingUtility.EnsureInputHandlingBoth();

            // Deterministic build inputs:
            // - If Mixamo FBX exist locally, bake them into the generated prefabs/animator.
            // - If missing (public repo clone), bake fallbacks.
            Debug.Log($"{BuildGenPrefix} Pre-build generation: processing Mixamo (if present), generating animator, generating prefabs...");
            MixamoProcessor.ProcessAssets();
            AnimatorGenerator.GenerateAnimatorController();
            PrefabGenerator.GeneratePrefabs();
            Debug.Log($"{BuildGenPrefix} Pre-build generation complete.");

            string[] scenes = { "Assets/Scenes/TestScene.unity" };
            string buildPath = "Builds/Windows/SurvivalRPG.exe";

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            var summary = report.summary;

            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"{BuildGenPrefix} Build succeeded: {summary.totalSize} bytes");
            }
            else if (summary.result == UnityEditor.Build.Reporting.BuildResult.Failed)
            {
                Debug.LogError($"{BuildGenPrefix} Build failed");
            }
        }
    }
}
