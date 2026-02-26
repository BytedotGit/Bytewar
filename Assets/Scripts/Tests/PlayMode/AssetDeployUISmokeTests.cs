using System.Collections;
using ByteWar.Building;
using ByteWar.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ByteWar.Tests.PlayMode
{
    public class AssetDeployUISmokeTests
    {
        [UnityTest]
        public IEnumerator AssetDeployUI_BrowserHelpers_ReturnExpectedValues()
        {
            Assert.IsTrue(AssetDeployUI.IsOpenShortcutPressed(true));
            Assert.IsFalse(AssetDeployUI.IsOpenShortcutPressed(false));

            var host = new GameObject("Test_AssetDeployUI_BrowserFlow");
            var ui = host.AddComponent<AssetDeployUI>();
            ui.SelectDeveloperEntryFromBrowserForTests(new DeployableAssetCatalog.Entry("TestAsset", "Generated/BlenderE2EProp/BlenderE2EProp", string.Empty));
            Assert.IsFalse(ui.IsBrowserOpenForTests);
            Assert.IsTrue(ui.IsPlacementModeActiveForTests);

            Object.Destroy(host);

            yield return null;
        }

        [UnityTest]
        public IEnumerator BuildingController_ExternalInputSuppressed_FlagsState()
        {
            var go = new GameObject("Test_BuildingController");
            var controller = go.AddComponent<BuildingController>();

            controller.SetExternalInputSuppressed(true);
            Assert.IsTrue(controller.IsExternalInputSuppressed);

            controller.SetExternalInputSuppressed(false);
            Assert.IsFalse(controller.IsExternalInputSuppressed);

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AssetDeployUI_CanDeployBlenderE2EProp_WhenPrefabPresent()
        {
            var prefab = Resources.Load<GameObject>("Generated/BlenderE2EProp/BlenderE2EProp");
            if (prefab == null)
                Assert.Ignore("Resources prefab missing: Generated/BlenderE2EProp/BlenderE2EProp");

            var host = new GameObject("Test_AssetDeployUI");
            var ui = host.AddComponent<AssetDeployUI>();

            Assert.IsTrue(ui.TryDeployAt(new Vector3(10f, 0.2f, 10f), Quaternion.identity, out var spawned, out var msg), msg);
            Assert.IsNotNull(spawned);
            Assert.IsNotNull(spawned.GetComponent<LODGroup>(), "Spawned prop should have LODGroup.");
            Assert.IsNotNull(spawned.GetComponent<Collider>(), "Spawned prop should have a root collider.");

            Assert.IsTrue(ui.TryDeployAt(new Vector3(12f, 0.2f, 10f), Quaternion.Euler(0f, 35f, 0f), out var spawnedSecond, out var msgSecond), msgSecond);
            Assert.IsNotNull(spawnedSecond);

            yield return null;

            Object.Destroy(spawned);
            Object.Destroy(spawnedSecond);
            Object.Destroy(host);
        }
    }
}
