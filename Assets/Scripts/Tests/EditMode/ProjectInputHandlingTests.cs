using NUnit.Framework;
using System.IO;
using System;
using System.Linq;
using ByteWar.Editor;
using ByteWar.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ByteWar.Tests.EditMode
{
    public class ProjectInputHandlingTests
    {
        [Test]
        public void ProjectSettings_ActiveInputHandler_IsBoth()
        {
            string path = Path.GetFullPath("ProjectSettings/ProjectSettings.asset");
            Assert.IsTrue(File.Exists(path), $"ProjectSettings.asset not found at '{path}'.");

            string text = File.ReadAllText(path);
            Assert.IsTrue(InputHandlingUtility.TryGetActiveInputHandler(text, out int value),
                "activeInputHandler not found in ProjectSettings.asset.");

            Assert.AreEqual(InputHandlingUtility.Both, value,
                "Active Input Handling must be 'Both' (2) or the new Input System won't drive movement/camera.");
        }

        [Test]
        public void BuildScript_UseWindowsD3D11GraphicsApiForBuild_AppliesAndRestoresSettings()
        {
            const BuildTarget target = BuildTarget.StandaloneWindows64;

            bool originalUseDefault = PlayerSettings.GetUseDefaultGraphicsAPIs(target);
            GraphicsDeviceType[] originalApis = originalUseDefault
                ? Array.Empty<GraphicsDeviceType>()
                : PlayerSettings.GetGraphicsAPIs(target).ToArray();

            using (BuildScript.UseWindowsD3D11GraphicsApiForBuild())
            {
                Assert.IsFalse(PlayerSettings.GetUseDefaultGraphicsAPIs(target),
                    "Build-time override should disable default graphics API selection on Windows.");

                GraphicsDeviceType[] overrideApis = PlayerSettings.GetGraphicsAPIs(target);
                CollectionAssert.AreEqual(new[] { GraphicsDeviceType.Direct3D11 }, overrideApis,
                    "Build-time override should force Direct3D11 only for Windows builds.");
            }

            Assert.AreEqual(originalUseDefault, PlayerSettings.GetUseDefaultGraphicsAPIs(target),
                "Build-time graphics API override should restore original UseDefaultGraphicsAPIs state.");

            if (!originalUseDefault)
            {
                CollectionAssert.AreEqual(originalApis, PlayerSettings.GetGraphicsAPIs(target),
                    "Build-time graphics API override should restore original custom API list.");
            }
        }

        [Test]
        public void BuildScript_EnsureGeneratedTestSceneForBuild_GeneratesPopulatedTerrainScene()
        {
            bool success = BuildScript.EnsureGeneratedTestSceneForBuild();
            Assert.IsTrue(success, "Pre-build scene generation should succeed.");

            const string scenePath = "Assets/Scenes/TestScene.unity";
            Assert.IsTrue(File.Exists(scenePath), "Generated TestScene should exist after build preflight.");

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Assert.IsTrue(scene.IsValid(), "Generated TestScene should be valid.");
            Assert.IsNotNull(Terrain.activeTerrain, "Generated TestScene should include active terrain.");
            Assert.IsNull(GameObject.Find("GreyboxGround"), "Generated TestScene should not include greybox fallback ground.");

            bool populated = AutoTestScenarioCore.TryValidateWorldPopulation(out string summary, out string failureReason);
            Assert.IsTrue(populated, $"Expected populated world scene. failure='{failureReason}' summary='{summary}'");
        }
    }
}
