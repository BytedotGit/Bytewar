using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode;
using ByteWar.Survival;
using ByteWar.Abilities;
using ByteWar.Building;
using ByteWar.Abilities.Mage;
using ByteWar.Networking;

namespace ByteWar.Tests.PlayMode
{
    public class GameLoopTests
    {
        private NetworkManager _networkManager;
        private GameObject _playerPrefab;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _networkManager = NGOTestHelper.CreateNetworkManager();
            _playerPrefab = _networkManager.NetworkConfig.PlayerPrefab;
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
        public IEnumerator EndToEndGameLoop_GatherCraftEquipCombat()
        {
            Debug.Log("Starting End-to-End Game Loop Test...");

            // 1. Setup Player and Environment
            var playerObj = _networkManager.SpawnManager.GetLocalPlayerObject();
            Assert.IsNotNull(playerObj, "Local player object should exist.");

            var player = playerObj.GetComponent<NetworkPlayer>();
            var inventory = playerObj.GetComponent<InventoryComponent>();
            var equipment = playerObj.GetComponent<EquipmentComponent>();
            var abilitySystem = playerObj.GetComponent<AbilitySystemComponent>();

            Assert.IsNotNull(player, "NetworkPlayer should be attached to the spawned player.");
            Assert.IsNotNull(inventory, "InventoryComponent should be attached to the spawned player.");
            Assert.IsNotNull(equipment, "EquipmentComponent should be attached to the spawned player.");
            Assert.IsNotNull(abilitySystem, "AbilitySystemComponent should be attached to the spawned player.");

            // Create mock items and recipe
            var wood = ScriptableObject.CreateInstance<Item>();
            wood.ItemName = "Wood";
            var stone = ScriptableObject.CreateInstance<Item>();
            stone.ItemName = "Stone";
            var staff = ScriptableObject.CreateInstance<Item>();
            staff.ItemName = "Basic Staff";
            staff.IsEquippable = true;
            staff.DamageBonus = 10f;
            staff.HealthBonus = 10f;

            var recipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            recipe.RecipeName = "Staff Recipe";
            recipe.SetIngredients(new System.Collections.Generic.List<RecipeIngredient>
            {
                new RecipeIngredient { Item = wood, Amount = 1 },
                new RecipeIngredient { Item = stone, Amount = 1 }
            });
            recipe.Result = staff;
            recipe.ResultAmount = 1;

            // Create Crafting Station
            var stationObj = new GameObject("CraftingStation");
            var stationNetObj = stationObj.AddComponent<NetworkObject>();
            var station = stationObj.AddComponent<CraftingStation>();
            station.SetAvailableRecipes(new System.Collections.Generic.List<CraftingRecipe> { recipe });

            stationNetObj.Spawn();
            yield return null;

            // Create Enemy
            var enemyObj = new GameObject("Enemy");
            var enemyNetObj = enemyObj.AddComponent<NetworkObject>();
            var enemyAttr = enemyObj.AddComponent<AttributeSet>();
            enemyNetObj.Spawn();
            yield return null;

            enemyAttr.Health.Value = 20f;
            enemyAttr.MaxHealth.Value = 20f;

            // Create Fireball Ability
            var fireball = ScriptableObject.CreateInstance<FireballAbility>();
            fireball.AbilityName = "Fireball";
            fireball.ManaCost = 10f;
            fireball.Cooldown = 1f;
            var damageEffect = ScriptableObject.CreateInstance<GameplayEffect>();
            damageEffect.Type = EffectType.Damage;
            damageEffect.Magnitude = 25f; // Enough to kill enemy
            fireball.DamageEffect = damageEffect;

            abilitySystem.AddLearnedAbility(fireball);

            yield return null;

            // 2. Gather Resources (Simulated by adding directly for this test, as ResourceNode requires physics/raycasts)
            Debug.Log("Simulating Resource Gathering...");
            inventory.AddItem(wood);
            inventory.AddItem(stone);
            Assert.IsTrue(inventory.HasItem(wood, 1));
            Assert.IsTrue(inventory.HasItem(stone, 1));

            // 3. Craft Staff
            Debug.Log("Simulating Crafting...");
            station.RequestCraftServerRpc(0, player.OwnerClientId);
            yield return null; // Wait for RPC

            Assert.IsFalse(inventory.HasItem(wood, 1), "Wood should be consumed");
            Assert.IsFalse(inventory.HasItem(stone, 1), "Stone should be consumed");
            Assert.IsTrue(inventory.HasItem(staff, 1), "Staff should be crafted");

            // 4. Equip Staff
            Debug.Log("Simulating Equipping...");
            equipment.EquipItem(staff);
            Assert.AreEqual(staff, equipment.EquippedWeapon, "Staff should be equipped");
            Assert.AreEqual(110f, abilitySystem.Attributes.MaxHealth.Value, "Equipping the staff should apply HealthBonus (+10).");

            // 5. Combat
            Debug.Log("Simulating Combat...");
            // We simulate the fireball hitting the enemy directly since the actual projectile requires physics time
            fireball.DamageEffect.ApplyEffect(enemyAttr);
            yield return null;

            Assert.LessOrEqual(enemyAttr.Health.Value, 0f, "Enemy should be dead");
            Debug.Log("End-to-End Game Loop Test Completed Successfully!");
        }
    }
}
