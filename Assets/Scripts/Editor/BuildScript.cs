using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.IO;

namespace ByteWar.Editor
{
    public class BuildScript
    {
        private const string BuildGenPrefix = "[BuildGen]";
        private const string TestScenePath = "Assets/Scenes/TestScene.unity";

        private sealed class WindowsGraphicsApiScope : IDisposable
        {
            private readonly BuildTarget _target;
            private readonly bool _hadDefaultApis;
            private readonly GraphicsDeviceType[] _originalApis;
            private bool _disposed;

            public WindowsGraphicsApiScope(BuildTarget target)
            {
                _target = target;
                _hadDefaultApis = PlayerSettings.GetUseDefaultGraphicsAPIs(target);
                _originalApis = _hadDefaultApis ? null : PlayerSettings.GetGraphicsAPIs(target);

                PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
                PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.Direct3D11 });
                Debug.Log($"{BuildGenPrefix} Applied Windows graphics API override: Direct3D11 only.");
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;

                if (_hadDefaultApis)
                {
                    PlayerSettings.SetUseDefaultGraphicsAPIs(_target, true);
                    Debug.Log($"{BuildGenPrefix} Restored Windows graphics API setting: UseDefaultGraphicsAPIs=True.");
                    return;
                }

                PlayerSettings.SetUseDefaultGraphicsAPIs(_target, false);
                if (_originalApis != null && _originalApis.Length > 0)
                    PlayerSettings.SetGraphicsAPIs(_target, _originalApis);

                Debug.Log($"{BuildGenPrefix} Restored Windows graphics API list to previous custom configuration.");
            }
        }

        public static IDisposable UseWindowsD3D11GraphicsApiForBuild()
        {
            return new WindowsGraphicsApiScope(BuildTarget.StandaloneWindows64);
        }

        public static bool EnsureGeneratedTestSceneForBuild()
        {
            Debug.Log($"{BuildGenPrefix} Regenerating '{TestScenePath}' before build...");

            try
            {
                SceneGenerator.GenerateTestScene();
            }
            catch (Exception ex)
            {
                Debug.LogError($"{BuildGenPrefix} Scene regeneration failed. {ex}");
                return false;
            }

            if (!File.Exists(TestScenePath))
            {
                Debug.LogError($"{BuildGenPrefix} Expected scene '{TestScenePath}' was not generated.");
                return false;
            }

            if (Terrain.activeTerrain == null)
            {
                Debug.LogError($"{BuildGenPrefix} Generated scene is invalid for build: no active terrain present.");
                return false;
            }

            if (GameObject.Find("GreyboxGround") != null)
            {
                Debug.LogError($"{BuildGenPrefix} Generated scene is invalid for build: greybox fallback detected.");
                return false;
            }

            Debug.Log($"{BuildGenPrefix} Scene regeneration ready for build.");
            return true;
        }

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
            Debug.Log($"{BuildGenPrefix} Pre-build generation: processing Mixamo (if present), generating animator, generating Blender deployables, generating prefabs...");
            MixamoProcessor.ProcessAssets();
            AnimatorGenerator.GenerateAnimatorController();

            // Optional: include Blender deployable prefabs in builds when the FBX files exist.
            // Safe no-op if assets haven't been generated yet.
            BlenderE2EPropPrefabGenerator.Generate();
            LargeTreePrefabGenerator.Generate();
            LargeTreeVariationPrefabGenerator.GenerateAll();

            if (!DeployableAssetCatalogBuilder.TryRegenerate(out string catalogMessage))
            {
                Debug.LogError($"{BuildGenPrefix} Deployable catalog regeneration failed. {catalogMessage}");
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                return;
            }
            Debug.Log($"{BuildGenPrefix} Deployable catalog regenerated. {catalogMessage}");

            PrefabGenerator.GeneratePrefabs();

            if (!EnsureGeneratedTestSceneForBuild())
            {
                Debug.LogError($"{BuildGenPrefix} Pre-build generation failed while preparing '{TestScenePath}'.");
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"{BuildGenPrefix} Pre-build generation complete.");

            string[] scenes = { TestScenePath };
            string buildPath = "Builds/Windows/ByteWar.exe";

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            UnityEditor.Build.Reporting.BuildSummary summary;
            using (UseWindowsD3D11GraphicsApiForBuild())
            {
                var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                summary = report.summary;
            }

            int? batchExitCode = null;

            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"{BuildGenPrefix} Build succeeded: {summary.totalSize} bytes");

                if (Application.isBatchMode)
                {
                    batchExitCode = 0;
                }
            }
            else if (summary.result == UnityEditor.Build.Reporting.BuildResult.Failed)
            {
                Debug.LogError($"{BuildGenPrefix} Build failed");

                if (Application.isBatchMode)
                {
                    batchExitCode = 1;
                }
            }

            if (Application.isBatchMode && batchExitCode.HasValue)
            {
                if (batchExitCode.Value == 0)
                    Debug.Log($"{BuildGenPrefix} Exiting editor with code 0 (batchmode).");
                else
                    Debug.LogError($"{BuildGenPrefix} Exiting editor with code 1 (batchmode).");

                EditorApplication.Exit(batchExitCode.Value);
            }
        }
    }
}
