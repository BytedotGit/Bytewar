using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ByteWar.Editor
{
    /// <summary>
    /// Local-only availability checks for third-party Mixamo assets.
    ///
    /// Bytewar is public and does not commit Mixamo FBX files. Instead, we detect
    /// whether they exist locally and use them when present; otherwise we fall back
    /// to procedural/placeholder assets.
    /// </summary>
    public static class MixamoLocalAssets
    {
        public const string MixamoFolder = "Assets/Art/Characters/Mixamo";

        private static readonly string[] RequiredFiles =
        {
            "Character.fbx",
            "Idle.fbx",
            "Walking.fbx",
            "Running.fbx",
            "Jump.fbx",
            "Attack.fbx",
        };

        public static bool AreAvailable(out string reason)
        {
            if (!AssetDatabase.IsValidFolder(MixamoFolder))
            {
                reason = $"Folder '{MixamoFolder}' does not exist.";
                return false;
            }

            foreach (var file in RequiredFiles)
            {
                string path = $"{MixamoFolder}/{file}";
                if (!File.Exists(path))
                {
                    reason = $"Missing '{path}'.";
                    return false;
                }
            }

            reason = "OK";
            return true;
        }

        public static string[] GetMissingFiles()
        {
            if (!AssetDatabase.IsValidFolder(MixamoFolder))
            {
                return new[] { $"Folder missing: {MixamoFolder}" };
            }

            return RequiredFiles
                .Select(f => $"{MixamoFolder}/{f}")
                .Where(p => !File.Exists(p))
                .ToArray();
        }
    }
}
