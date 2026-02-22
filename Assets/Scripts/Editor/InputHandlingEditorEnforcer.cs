using UnityEditor;
using UnityEngine;

namespace SurvivalRPG.Editor
{
    /// <summary>
    /// Ensures that entering Play Mode doesn't happen with legacy-only input.
    /// This prevents recurring "no movement / no camera" reports caused by
    /// ProjectSettings.asset regressing to activeInputHandler: 0.
    /// </summary>
    [InitializeOnLoad]
    public static class InputHandlingEditorEnforcer
    {
        static InputHandlingEditorEnforcer()
        {
            // Run once per editor load.
            InputHandlingUtility.EnsureInputHandlingBoth();

            // Also re-check when play mode is entered (some tools regenerate ProjectSettings).
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                Debug.Log("[InputHandlingEditorEnforcer] Entered Play Mode — validating input handling.");
                InputHandlingUtility.EnsureInputHandlingBoth();
            }
        }
    }
}
