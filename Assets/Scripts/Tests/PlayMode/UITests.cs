using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode;
using SurvivalRPG.UI;
using SurvivalRPG.Abilities;
using SurvivalRPG.Abilities.Mage;
using SurvivalRPG.Survival;
using SurvivalRPG.Building;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace SurvivalRPG.Tests.PlayMode
{
    public class UITests
    {
        private NetworkManager _networkManager;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _networkManager = NGOTestHelper.CreateNetworkManager();
            _networkManager.StartHost();
            yield return new WaitForSeconds(0.1f);
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            NGOTestHelper.CleanUp();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerVitalsUI_UpdatesWhenAttributesChange()
        {
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            var attributes = localPlayer.GetComponent<AttributeSet>();

            // Create UI
            var uiGo = new GameObject("PlayerVitalsUI");
            var vitalsUI = uiGo.AddComponent<PlayerVitalsUI>();

            // Add mock UI elements
            var healthTextGo = new GameObject("HealthText");
            healthTextGo.transform.SetParent(uiGo.transform);
            var healthText = healthTextGo.AddComponent<TextMeshProUGUI>();

            // Use reflection to set private fields for testing
            var type = typeof(PlayerVitalsUI);
            type.GetField("healthText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(vitalsUI, healthText);

            yield return null; // Wait for Update to bind

            // Change health
            attributes.ApplyDamage(20f);
            yield return null; // Wait for UpdateUI

            Assert.AreEqual("HP: 80/100", healthText.text);
            Debug.Log("PlayerVitalsUI updated correctly.");
        }

        [UnityTest]
        public IEnumerator ActionBarUI_UpdatesWhenAbilityCast()
        {
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            var abilitySystem = localPlayer.GetComponent<AbilitySystemComponent>();

            // Create a mock ability
            var ability = ScriptableObject.CreateInstance<FireballAbility>();
            ability.AbilityName = "Fireball";
            ability.Cooldown = 5f;
            ability.ManaCost = 10f;
            abilitySystem.LearnedAbilities.Add(ability);

            // Create UI
            var uiGo = new GameObject("ActionBarUI");
            var actionBarUI = uiGo.AddComponent<ActionBarUI>();

            var slot = new ActionBarUI.ActionSlot();
            var textGo = new GameObject("CooldownText");
            slot.CooldownText = textGo.AddComponent<TextMeshProUGUI>();

            var slotsList = new List<ActionBarUI.ActionSlot> { slot };
            var type = typeof(ActionBarUI);
            type.GetField("actionSlots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(actionBarUI, slotsList);

            yield return null; // Wait for Update to bind

            // Cast ability
            abilitySystem.TryCastAbility(0, Vector3.zero);
            yield return new WaitForSeconds(0.1f); // Wait for RPC and UpdateUI

            Assert.IsTrue(slot.CooldownText.text != "", "Cooldown text should not be empty after casting.");
            Debug.Log("ActionBarUI updated correctly.");
        }

        [UnityTest]
        public IEnumerator InventoryUI_UpdatesWhenItemAdded()
        {
            var localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject;
            var inventory = localPlayer.GetComponent<InventoryComponent>();

            // Create UI
            var uiGo = new GameObject("InventoryUI");
            var inventoryUI = uiGo.AddComponent<InventoryUI>();

            var containerGo = new GameObject("Container");
            var prefabGo = new GameObject("Prefab");
            prefabGo.AddComponent<TextMeshProUGUI>();

            var type = typeof(InventoryUI);
            type.GetField("itemContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(inventoryUI, containerGo.transform);
            type.GetField("itemPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(inventoryUI, prefabGo);

            yield return null; // Wait for Update to bind

            // Add item
            var item = ScriptableObject.CreateInstance<Item>();
            item.ItemName = "TestItem";
            inventory.AddItem(item);

            yield return null; // Wait for UpdateUI

            Assert.AreEqual(1, containerGo.transform.childCount);
            var text = containerGo.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            Assert.AreEqual("TestItem x1", text.text);
            Debug.Log("InventoryUI updated correctly.");
        }

        [UnityTest]
        public IEnumerator CraftingUI_UpdatesWhenStationSet()
        {
            // Create Station
            var stationGo = new GameObject("CraftingStation");
            var station = stationGo.AddComponent<CraftingStation>();
            station.AvailableRecipes = new List<CraftingRecipe>();

            var recipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            recipe.RecipeName = "TestRecipe";
            recipe.Ingredients = new List<RecipeIngredient>();
            station.AvailableRecipes.Add(recipe);

            // Create UI
            var uiGo = new GameObject("CraftingUI");
            var craftingUI = uiGo.AddComponent<CraftingUI>();

            var containerGo = new GameObject("Container");
            var prefabGo = new GameObject("Prefab");
            prefabGo.AddComponent<TextMeshProUGUI>();
            prefabGo.AddComponent<Button>();

            var type = typeof(CraftingUI);
            type.GetField("recipeContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(craftingUI, containerGo.transform);
            type.GetField("recipePrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(craftingUI, prefabGo);

            yield return null; // Wait for Update to bind

            Assert.AreEqual(1, containerGo.transform.childCount);
            var text = containerGo.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            Assert.IsTrue(text.text.Contains("TestRecipe"));
            Debug.Log("CraftingUI updated correctly.");
        }
    }
}