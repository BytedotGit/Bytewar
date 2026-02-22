using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SurvivalRPG.Editor
{
    /// <summary>
    /// Enforces that the project uses Both legacy + new Input System.
    /// This is a common regression source that manifests as "can't move / can't look".
    /// 
    /// Unity 6000.x doesn't always expose a reliable PlayerSettings API for this,
    /// so we patch ProjectSettings/ProjectSettings.asset which is authoritative.
    /// </summary>
    public static class InputHandlingUtility
    {
        public const int Both = 2;
        private const string Key = "activeInputHandler";

        private static readonly Regex ActiveInputHandlerRegex =
            new Regex(@"(^\s*activeInputHandler:\s*)(\d+)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

        public static void EnsureInputHandlingBoth()
        {
            if (!TryEnsureInputHandlingBoth(out string message))
            {
                Debug.LogWarning($"[InputHandlingUtility] {message}");
            }
        }

        /// <summary>
        /// Attempts to enforce 'activeInputHandler: 2' (Both).
        /// Returns true if no change was needed or the change was applied.
        /// Returns false if the settings file couldn't be located or patched.
        /// </summary>
        public static bool TryEnsureInputHandlingBoth(out string message)
        {
            try
            {
                string projectSettingsPath = Path.GetFullPath("ProjectSettings/ProjectSettings.asset");
                if (!File.Exists(projectSettingsPath))
                {
                    message = $"ProjectSettings.asset not found at '{projectSettingsPath}'.";
                    return false;
                }

                string text = File.ReadAllText(projectSettingsPath);
                if (!TryGetActiveInputHandler(text, out int currentValue))
                {
                    message = $"'{Key}' not found in ProjectSettings.asset; cannot enforce Input Handling.";
                    return false;
                }

                if (currentValue == Both)
                {
                    message = $"{Key} already set to {Both} (Both).";
                    return true;
                }

                string patched = ActiveInputHandlerRegex.Replace(text, m => $"{m.Groups[1].Value}{Both}", 1);
                if (patched == text)
                {
                    message = $"Failed to patch {Key}; regex replacement produced no change (current={currentValue}).";
                    return false;
                }

                File.WriteAllText(projectSettingsPath, patched);
                AssetDatabase.Refresh();
                message = $"Forced {Key} to {Both} (Both) by patching ProjectSettings.asset (was {currentValue}).";
                Debug.LogWarning($"[InputHandlingUtility] {message}");
                return true;
            }
            catch (Exception ex)
            {
                message = $"Exception while enforcing input handling: {ex.Message}";
                return false;
            }
        }

        public static bool TryGetActiveInputHandler(string projectSettingsText, out int value)
        {
            value = -1;
            if (string.IsNullOrWhiteSpace(projectSettingsText)) return false;

            var match = ActiveInputHandlerRegex.Match(projectSettingsText);
            if (!match.Success) return false;

            return int.TryParse(match.Groups[2].Value, out value);
        }
    }
}
