using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode;
using ByteWar.Survival;

namespace ByteWar.Tests.PlayMode
{
    public class ResourceGatheringSmokeTests
    {
        private NetworkManager _networkManager;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _networkManager = NGOTestHelper.CreateNetworkManager();
            _networkManager.StartHost();
            yield return NGOTestHelper.WaitForLocalPlayerReady(_networkManager);
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            NGOTestHelper.CleanUp();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerDamagesResourceNode_NodeDestroyed_ItemAddedToInventory()
        {
            // Arrange
            var playerObj = _networkManager.SpawnManager.GetLocalPlayerObject();
            var inventory = playerObj.GetComponent<InventoryComponent>();

            // Create a mock item
            var mockItem = ScriptableObject.CreateInstance<Item>();
            mockItem.ItemName = "Wood";

            // Setup Resource Node
            var nodeGo = new GameObject("Tree");
            var nodeNetworkObj = nodeGo.AddComponent<NetworkObject>();
            var resourceNode = nodeGo.AddComponent<ResourceNode>();

            resourceNode.SetupForTest(mockItem, 3);
            nodeNetworkObj.Spawn();
            yield return null;

            float initialHealth = resourceNode.Health.Value;
            bool hasItemBefore = inventory.HasItem(mockItem, 3);

            // Act
            Debug.Log("[Test] Player attacking ResourceNode...");

            // Deal enough damage to destroy it
            resourceNode.TakeDamage(initialHealth, inventory);
            yield return new WaitForSeconds(0.1f); // Wait for RPCs and destruction

            // Assert
            Assert.IsFalse(hasItemBefore, "Player should not have the item before gathering.");
            Assert.IsTrue(inventory.HasItem(mockItem, 3), "Player did not receive the gathered items.");
            Assert.IsTrue(nodeGo == null || !nodeGo.activeInHierarchy, "ResourceNode was not destroyed.");

            Debug.Log("[Test] Resource gathering smoke test passed successfully.");
        }
    }
}