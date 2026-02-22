using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SurvivalRPG.Editor
{
    /// <summary>
    /// Hardening layer: runs for all builds (editor UI or batchmode) and enforces
    /// Active Input Handling = Both so the new Input System actually works.
    /// </summary>
    public sealed class InputHandlingBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log($"[InputHandlingBuildPreprocessor] Preprocess build: {report.summary.platform}");
            InputHandlingUtility.EnsureInputHandlingBoth();
        }
    }
}
