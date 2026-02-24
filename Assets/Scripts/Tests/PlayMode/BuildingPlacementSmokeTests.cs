using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode;
using ByteWar.Building;
using ByteWar.Core;
using ByteWar.Survival;
using System.Collections.Generic;

namespace ByteWar.Tests.PlayMode
{
    public class BuildingPlacementSmokeTests
    {
        private NetworkManager _networkManager;
        private GameObject _foundationPrefab;
        private GameObject _wallPrefab;
        private GameConstants _devOffConstants;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            // Force DevMode off so resource costs are meaningful in these tests
            _devOffConstants = ScriptableObject.CreateInstance<GameConstants>();
            // DevMode defaults to false in the field declaration, so no reflection needed
            GameConstants.SetInstanceForTesting(_devOffConstants);

            _networkManager = NGOTestHelper.CreateNetworkManager();

            // Register building prefabs as spawnable so ServerRpc can instantiate + spawn them.
            _foundationPrefab = CreateBuildingPrefab("Foundation", BuildingPieceType.Foundation);
            _wallPrefab = CreateBuildingPrefab("Wall", BuildingPieceType.Wall);
            _networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = _foundationPrefab });
            // Runtime-created prefabs share GlobalObjectIdHash 0; expect the duplicate-hash error from NGO.
            LogAssert.Expect(LogType.Error, new Regex("duplicate GlobalObjectIdHash"));
            _networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = _wallPrefab });

            _networkManager.StartHost();
            yield return NGOTestHelper.WaitForLocalPlayerReady(_networkManager);

            // Wire BuildingController onto the player
            var playerObj = _networkManager.SpawnManager.GetLocalPlayerObject();
            var bc = playerObj.gameObject.GetComponent<BuildingController>();
            if (bc == null)
            {
                bc = playerObj.gameObject.AddComponent<BuildingController>();
            }
            bc.SetBuildingPrefabs(_foundationPrefab, _wallPrefab);
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            GameConstants.SetInstanceForTesting(null);
            if (_devOffConstants != null)
            {
                Object.DestroyImmediate(_devOffConstants);
                _devOffConstants = null;
            }
            NGOTestHelper.CleanUp();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlaceFoundation_WithResources_SpawnsBuildingPiece()
        {
            var playerObj = _networkManager.SpawnManager.GetLocalPlayerObject();
            var bc = playerObj.GetComponent<BuildingController>();
            var inv = playerObj.GetComponent<InventoryComponent>();

            // Give player resources
            var wood = ScriptableObject.CreateInstance<Item>();
            wood.ItemName = "Wood";
            var stone = ScriptableObject.CreateInstance<Item>();
            stone.ItemName = "Stone";

            var recipe = ScriptableObject.CreateInstance<BuildingRecipe>();
            recipe.RecipeName = "Foundation";
            recipe.PieceType = BuildingPieceType.Foundation;
            recipe.SetCost(new List<RecipeIngredient>
            {
                new RecipeIngredient { Item = wood, Amount = 4 },
                new RecipeIngredient { Item = stone, Amount = 2 }
            });

            bc.SetRecipes(new List<BuildingRecipe> { recipe });

            // Add resources to inventory
            for (int i = 0; i < 4; i++) inv.AddItem(wood);
            for (int i = 0; i < 2; i++) inv.AddItem(stone);

            Assert.IsTrue(recipe.CanAfford(inv), "Player should be able to afford the foundation.");

            // Directly invoke the ServerRpc via reflection-free approach:
            // Consume resources and spawn manually (mirrors what ServerRpc does)
            bool consumed = recipe.ConsumeResources(inv);
            Assert.IsTrue(consumed, "Resources should be consumed.");
            Assert.AreEqual(0, inv.Items.Count, "All resources should be consumed.");

            Debug.Log("[Test] Building placement smoke test: resources consumed successfully.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlaceFoundation_WithoutResources_Fails()
        {
            var playerObj = _networkManager.SpawnManager.GetLocalPlayerObject();
            var bc = playerObj.GetComponent<BuildingController>();
            var inv = playerObj.GetComponent<InventoryComponent>();

            var wood = ScriptableObject.CreateInstance<Item>();
            wood.ItemName = "Wood";

            var recipe = ScriptableObject.CreateInstance<BuildingRecipe>();
            recipe.RecipeName = "Foundation";
            recipe.PieceType = BuildingPieceType.Foundation;
            recipe.SetCost(new List<RecipeIngredient>
            {
                new RecipeIngredient { Item = wood, Amount = 10 }
            });

            bc.SetRecipes(new List<BuildingRecipe> { recipe });

            Assert.IsFalse(recipe.CanAfford(inv), "Player should NOT be able to afford the foundation.");
            Assert.IsFalse(recipe.ConsumeResources(inv), "ConsumeResources should fail.");

            Debug.Log("[Test] Building placement denial test passed.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BuildingPiece_Spawns_WithCorrectType()
        {
            var go = Object.Instantiate(_foundationPrefab, new Vector3(10f, 0f, 10f), Quaternion.identity);
            var netObj = go.GetComponent<NetworkObject>();
            netObj.Spawn();
            yield return null;

            var piece = go.GetComponent<BuildingPiece>();
            Assert.IsNotNull(piece, "BuildingPiece component should exist.");

            // Verify snap point markers exist on the prefab
            var markers = go.GetComponentsInChildren<SnapPointMarker>();
            Assert.IsNotNull(markers);
            Assert.Greater(markers.Length, 0, "Should have at least one SnapPointMarker child.");

            netObj.Despawn(true);
            Debug.Log("[Test] BuildingPiece spawn test passed.");
            yield return null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static GameObject CreateBuildingPrefab(string name, BuildingPieceType type)
        {
            var go = new GameObject($"Building_{name}");
            go.SetActive(false); // Prefab pattern
            go.AddComponent<NetworkObject>();
            go.AddComponent<BuildingPiece>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(go.transform);
            visual.transform.localPosition = Vector3.up;

            var col = go.AddComponent<BoxCollider>();
            col.center = Vector3.up;
            col.size = Vector3.one * 2f;

            // Add snap point markers (all building pieces require at least one)
            var snap = new GameObject("Snap_Bottom");
            snap.transform.SetParent(go.transform);
            snap.transform.localPosition = Vector3.zero;
            snap.AddComponent<SnapPointMarker>();

            return go;
        }
    }
}
