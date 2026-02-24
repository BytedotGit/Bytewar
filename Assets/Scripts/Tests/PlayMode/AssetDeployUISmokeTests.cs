using System.Collections;
using ByteWar.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ByteWar.Tests.PlayMode
{
    public class AssetDeployUISmokeTests
    {
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

            yield return null;

            Object.Destroy(spawned);
            Object.Destroy(host);
        }
    }
}
