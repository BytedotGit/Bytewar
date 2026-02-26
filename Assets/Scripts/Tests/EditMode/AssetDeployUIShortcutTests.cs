using ByteWar.UI;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace ByteWar.Tests.EditMode
{
    public class AssetDeployUIShortcutTests
    {
        [Test]
        public void IsOpenShortcutPressed_ReturnsExpected()
        {
            Assert.IsTrue(AssetDeployUI.IsOpenShortcutPressed(true));
            Assert.IsFalse(AssetDeployUI.IsOpenShortcutPressed(false));
        }

        [Test]
        public void HasRequiredDeveloperSpawnComponents_ReturnsFalse_WhenMissingNetworkObject()
        {
            var root = new GameObject("AssetRoot");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<BoxCollider>();
            visual.AddComponent<LODGroup>();

            Assert.IsFalse(AssetDeployUI.HasRequiredDeveloperSpawnComponents(root));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void HasRequiredDeveloperSpawnComponents_ReturnsTrue_WhenRequiredComponentsExist()
        {
            var root = new GameObject("AssetRoot");
            root.AddComponent<NetworkObject>();
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<BoxCollider>();
            visual.AddComponent<LODGroup>();

            Assert.IsTrue(AssetDeployUI.HasRequiredDeveloperSpawnComponents(root));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void HasDisplayableDeveloperComponents_ReturnsTrue_WhenColliderAndLodExist()
        {
            var root = new GameObject("AssetRoot");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<BoxCollider>();
            visual.AddComponent<LODGroup>();

            Assert.IsTrue(AssetDeployUI.HasDisplayableDeveloperComponents(root));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void HasDisplayableDeveloperComponents_ReturnsFalse_WhenLodMissing()
        {
            var root = new GameObject("AssetRoot");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<BoxCollider>();

            Assert.IsFalse(AssetDeployUI.HasDisplayableDeveloperComponents(root));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void BrowserSelection_ClosesBrowser_AndKeepsPlacementActive()
        {
            var host = new GameObject("Test_AssetDeployUI");
            var ui = host.AddComponent<AssetDeployUI>();
            var entry = new DeployableAssetCatalog.Entry("TestAsset", "Generated/BlenderE2EProp/BlenderE2EProp", string.Empty);

            ui.SelectDeveloperEntryFromBrowserForTests(entry);

            Assert.IsFalse(ui.IsBrowserOpenForTests);
            Assert.IsTrue(ui.IsPlacementModeActiveForTests);

            Object.DestroyImmediate(host);
        }

        [Test]
        public void Escape_ClosesBrowser_AndCancelsPlacement()
        {
            var host = new GameObject("Test_AssetDeployUI");
            var ui = host.AddComponent<AssetDeployUI>();
            var entry = new DeployableAssetCatalog.Entry("TestAsset", "Generated/BlenderE2EProp/BlenderE2EProp", string.Empty);

            ui.EnterDeveloperPlacementModeForTests(entry);
            ui.OpenBrowserForTests();

            Assert.IsTrue(ui.IsBrowserOpenForTests);
            Assert.IsTrue(ui.IsPlacementModeActiveForTests);

            ui.HandleEscapeForTests();

            Assert.IsFalse(ui.IsBrowserOpenForTests);
            Assert.IsFalse(ui.IsPlacementModeActiveForTests);

            Object.DestroyImmediate(host);
        }

        [Test]
        public void ComputeRadialPreviewScale_RegularBounds_ReturnsExpectedScale()
        {
            float scale = AssetDeployUI.ComputeRadialPreviewScale(
                new Bounds(Vector3.zero, new Vector3(4f, 2f, 2f)),
                2f,
                0.2f,
                2f);

            Assert.That(scale, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [TestCase("Generated/BlenderE2EProp/Preview.png", "Generated/BlenderE2EProp/Preview")]
        [TestCase("Generated/BlenderE2EProp/Preview.jpg", "Generated/BlenderE2EProp/Preview")]
        [TestCase("Generated\\BlenderE2EProp\\Preview.tga", "Generated/BlenderE2EProp/Preview")]
        [TestCase("Generated/BlenderE2EProp/BlenderE2EProp.prefab", "Generated/BlenderE2EProp/BlenderE2EProp")]
        [TestCase("Generated/BlenderE2EProp/Preview", "Generated/BlenderE2EProp/Preview")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void NormalizeResourceLoadPath_ReturnsExpected(string input, string expected)
        {
            Assert.AreEqual(expected, AssetDeployUI.NormalizeResourceLoadPath(input));
        }
    }
}
