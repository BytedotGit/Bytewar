using UnityEngine;
using UnityEditor;
using System.IO;

namespace SurvivalRPG.Editor
{
    public static class MixamoProcessor
    {
        private const string BuildGenPrefix = "[BuildGen]";
        private const string MixamoFolder = MixamoLocalAssets.MixamoFolder;
        private const string CharacterFile = "Character";

        // Animation file names (everything that is NOT the base mesh)
        private static readonly string[] AnimationFiles = { "Idle", "Walking", "Running", "Jump", "Attack" };

        [MenuItem("SurvivalRPG/Process Mixamo Assets")]
        public static void ProcessAssets()
        {
            Debug.Log($"{BuildGenPrefix} MixamoProcessor: start");

            if (!MixamoLocalAssets.AreAvailable(out string reason))
            {
                Debug.Log($"{BuildGenPrefix} MixamoProcessor: Mixamo not available locally ({reason}). Using fallback content.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(MixamoFolder))
            {
                Debug.LogWarning($"{BuildGenPrefix} MixamoProcessor: folder missing path={MixamoFolder}");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { MixamoFolder });
            if (guids.Length == 0)
            {
                Debug.LogWarning($"{BuildGenPrefix} MixamoProcessor: no FBX models found under {MixamoFolder}");
                return;
            }

            Debug.Log($"{BuildGenPrefix} MixamoProcessor: found modelCount={guids.Length}");

            // --- Pass 1: import the character mesh so its avatar exists ---
            string characterPath = null;
            foreach (string guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileNameWithoutExtension(p), CharacterFile,
                    System.StringComparison.OrdinalIgnoreCase))
                {
                    characterPath = p;
                    break;
                }
            }

            if (characterPath == null)
            {
                Debug.LogError($"{BuildGenPrefix} MixamoProcessor: Character.fbx not found — cannot process animations.");
                return;
            }

            ModelImporter charImporter = AssetImporter.GetAtPath(characterPath) as ModelImporter;
            charImporter.animationType = ModelImporterAnimationType.Human;
            charImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            charImporter.SaveAndReimport();
            Debug.Log($"{BuildGenPrefix} MixamoProcessor: Character.fbx imported as Humanoid with Avatar.");

            // --- Pass 2: process animation FBX files, copying the avatar ---
            Avatar sourceAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(
                Path.ChangeExtension(characterPath, null) + "Avatar" /* won't match */ );

            // Unity names the avatar asset as "<fileName> Avatar" inside the FBX
            // The reliable way is to load it from the imported assets:
            foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(characterPath))
            {
                if (obj is Avatar av && !av.name.StartsWith("__preview__"))
                {
                    sourceAvatar = av;
                    Debug.Log($"{BuildGenPrefix} MixamoProcessor: sourceAvatar='{av.name}'");
                    break;
                }
            }

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(assetPath);
                if (!IsAnimationFile(fileName)) continue;

                ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null) continue;

                importer.animationType = ModelImporterAnimationType.Human;

                if (sourceAvatar != null)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    importer.sourceAvatar = sourceAvatar;
                }
                else
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                }

                // Rename the clip and set loop flag
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                if (clips != null && clips.Length > 0)
                {
                    clips[0].name = fileName;
                    clips[0].loopTime = fileName == "Idle" || fileName == "Walking" || fileName == "Running";
                    importer.clipAnimations = clips;
                }

                importer.SaveAndReimport();
                Debug.Log($"{BuildGenPrefix} MixamoProcessor: processed animation='{fileName}' loop={(clips != null && clips.Length > 0 ? clips[0].loopTime : false)}");
            }

            Debug.Log($"{BuildGenPrefix} MixamoProcessor: done");
        }

        private static bool IsAnimationFile(string fileName)
        {
            foreach (string anim in AnimationFiles)
            {
                if (string.Equals(fileName, anim, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
