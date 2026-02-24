using UnityEditor;
using UnityEngine;

namespace ByteWar.Editor
{
    public class BuildScript
    {
        private const string BuildGenPrefix = "[BuildGen]";

        [MenuItem("ByteWar/Build Windows Client")]
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

            // Optional: include Blender E2E prop prefab in builds when the FBX exists.
            // Safe no-op if the FBX hasn't been generated yet.
            BlenderE2EPropPrefabGenerator.Generate();

            Debug.Log($"{BuildGenPrefix} Pre-build generation complete.");

            string[] scenes = { "Assets/Scenes/TestScene.unity" };
            string buildPath = "Builds/Windows/ByteWar.exe";

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

                if (Application.isBatchMode)
                {
                    Debug.Log($"{BuildGenPrefix} Exiting editor with code 0 (batchmode).");
                    EditorApplication.Exit(0);
                }
            }
            else if (summary.result == UnityEditor.Build.Reporting.BuildResult.Failed)
            {
                Debug.LogError($"{BuildGenPrefix} Build failed");

                if (Application.isBatchMode)
                {
                    Debug.LogError($"{BuildGenPrefix} Exiting editor with code 1 (batchmode).");
                    EditorApplication.Exit(1);
                }
            }
        }
    }
}
