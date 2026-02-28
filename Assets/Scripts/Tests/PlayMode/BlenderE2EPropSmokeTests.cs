using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode;

namespace ByteWar.Tests.PlayMode
{
    public class BlenderE2EPropSmokeTests
    {
        [UnityTest]
        public IEnumerator BlenderE2EProp_CanLoadFromResources_WhenPresent()
        {
            var prefab = Resources.Load<GameObject>("Generated/BlenderE2EProp/BlenderE2EProp");
            if (prefab == null)
                Assert.Ignore("Resources prefab missing. Run Generate Blender E2E Prop Prefab (or Generate All) after generating the FBX.");

            var instance = Object.Instantiate(prefab);
            try
            {
                Assert.IsNotNull(instance.GetComponent<LODGroup>(), "LODGroup missing on runtime prefab root.");
                Assert.IsNotNull(instance.GetComponent<Collider>(), "Collider missing on runtime prefab root.");
            }
            finally
            {
                Object.Destroy(instance);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator LargeTree_CanLoadFromResources_WithNetworkRootAndCollider_WhenPresent()
        {
            var prefab = Resources.Load<GameObject>("Generated/LargeTree/LargeTree");
            if (prefab == null)
                Assert.Ignore("Resources prefab missing. Run Generate Large Tree Prefab (or Generate All) after generating LargeTree FBX.");

            var instance = Object.Instantiate(prefab);
            try
            {
                Assert.IsNotNull(instance.GetComponent<NetworkObject>(), "NetworkObject missing on LargeTree runtime prefab root.");
                Assert.IsNotNull(instance.GetComponent<LODGroup>(), "LODGroup missing on LargeTree runtime prefab root.");
                Assert.IsNotNull(instance.GetComponent<Collider>(), "Collider missing on LargeTree runtime prefab root.");
            }
            finally
            {
                Object.Destroy(instance);
            }

            yield return null;
        }
    }
}
