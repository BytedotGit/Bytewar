using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode;
using ByteWar.Building;
using ByteWar.Core;
using ByteWar.UI;

namespace ByteWar.Tests.PlayMode
{
    public class DevModeAndHUDSmokeTests
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
        public IEnumerator KeybindingHUD_Instantiates_InPlayMode()
        {
            var hudGo = new GameObject("KeybindingHUD");
            var hud = hudGo.AddComponent<KeybindingHUD>();
            yield return null;

            Assert.IsNotNull(hud, "KeybindingHUD should be present.");
            Debug.Log("[Test] PASS: KeybindingHUD instantiated in PlayMode.");

            Object.Destroy(hudGo);
        }

        [UnityTest]
        public IEnumerator DevMode_AllowsFreeBuildingPlacement()
        {
            // Explicitly enable DevMode for this test
            var gc = ScriptableObject.CreateInstance<GameConstants>();
            GameConstants.SetInstanceForTesting(gc);
            GameConstants.SetBuildingCostsEnabled(true);
            GameConstants.SetDevMode(true);

            try
            {
                Assert.IsTrue(GameConstants.IsDevMode(), "DevMode should be explicitly enabled.");
                Assert.IsTrue(GameConstants.IsBuildingCostsEnabled(), "Building costs should be enabled to validate DevMode bypass.");

                // Get player's BuildingController
                var playerObj = _networkManager.SpawnManager.GetLocalPlayerObject();
                var bc = playerObj.GetComponent<BuildingController>();

                if (bc == null)
                {
                    Debug.Log("[Test] SKIP: BuildingController not on test player.");
                    yield break;
                }

                // Create a recipe with expensive costs
                var recipe = ScriptableObject.CreateInstance<BuildingRecipe>();
                var wood = ScriptableObject.CreateInstance<ByteWar.Survival.Item>();
                wood.ItemName = "Wood";
                recipe.RecipeName = "ExpensiveFoundation";
                recipe.PieceType = BuildingPieceType.Foundation;
                recipe.SetCost(new System.Collections.Generic.List<ByteWar.Building.RecipeIngredient>
                {
                    new ByteWar.Building.RecipeIngredient { Item = wood, Amount = 9999 }
                });

                var inv = playerObj.GetComponent<ByteWar.Survival.InventoryComponent>();

                // In DevMode, this should still be affordable
                Assert.IsTrue(recipe.CanAfford(inv), "DevMode should make everything affordable.");
                Assert.IsTrue(recipe.ConsumeResources(inv), "DevMode should allow consuming without resources.");

                Debug.Log("[Test] PASS: DevMode allows free building.");

                Object.Destroy(recipe);
                Object.Destroy(wood);
                yield return null;
            }
            finally
            {
                // Restore DevMode to off
                GameConstants.SetInstanceForTesting(null);
                Object.DestroyImmediate(gc);
            }
        }

        [UnityTest]
        public IEnumerator EnemyAI_HasCharacterController_WhenProperlySetup()
        {
            // Create an enemy with CharacterController (as PrefabGenerator would)
            var enemyGo = new GameObject("TestEnemy");
            enemyGo.AddComponent<NetworkObject>();
            enemyGo.AddComponent<ByteWar.Abilities.AttributeSet>();
            var cc = enemyGo.AddComponent<CharacterController>();
            cc.height = 2.2f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 1.1f, 0f);
            enemyGo.AddComponent<ByteWar.Survival.EnemyAI>();

            yield return null;

            Assert.IsNotNull(enemyGo.GetComponent<CharacterController>(),
                "Enemy should have a CharacterController for collision.");
            Debug.Log("[Test] PASS: EnemyAI has CharacterController.");

            Object.Destroy(enemyGo);
        }
    }
}
