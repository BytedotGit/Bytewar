using UnityEditor;
using UnityEngine;

namespace SurvivalRPG.Editor
{
    public class BuildScript
    {
        [MenuItem("SurvivalRPG/Build Windows Client")]
        public static void BuildWindowsClient()
        {
            Debug.Log("Starting Windows Client Build...");

            // Ensure builds keep both Legacy + New Input System enabled.
            // - IMGUI HUD buttons rely on legacy input.
            // - Movement/interaction rely on the new Input System.
            InputHandlingUtility.EnsureInputHandlingBoth();

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
                Debug.Log($"Build succeeded: {summary.totalSize} bytes");
            }
            else if (summary.result == UnityEditor.Build.Reporting.BuildResult.Failed)
            {
                Debug.LogError("Build failed");
            }
        }
    }
}
