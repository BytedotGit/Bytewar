using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using ByteWar.Editor;
using ByteWar.Survival;
using ByteWar.Building;
using ByteWar.Abilities.Mage;
using ByteWar.Abilities.Talents;
using System.IO;

namespace ByteWar.Tests.EditMode
{
    public class GeneratorTests
    {
        [Test]
        public void AssetGenerator_CreatesExpectedAssets()
        {
            // Arrange
            string basePath = "Assets/GeneratedAssets";

            // Act
            AssetGenerator.GenerateAssets();

            // Assert
            Assert.IsTrue(AssetDatabase.IsValidFolder(basePath), "GeneratedAssets folder should exist.");

            Item wood = AssetDatabase.LoadAssetAtPath<Item>($"{basePath}/Wood.asset");
            Assert.IsNotNull(wood, "Wood item should be generated.");
            Assert.AreEqual("Wood", wood.ItemName);

            Item stone = AssetDatabase.LoadAssetAtPath<Item>($"{basePath}/Stone.asset");
            Assert.IsNotNull(stone, "Stone item should be generated.");

            Item basicStaff = AssetDatabase.LoadAssetAtPath<Item>($"{basePath}/BasicStaff.asset");
            Assert.IsNotNull(basicStaff, "BasicStaff item should be generated.");
            Assert.IsTrue(basicStaff.IsEquippable);

            CraftingRecipe staffRecipe = AssetDatabase.LoadAssetAtPath<CraftingRecipe>($"{basePath}/StaffRecipe.asset");
            Assert.IsNotNull(staffRecipe, "StaffRecipe should be generated.");
            Assert.AreEqual(basicStaff, staffRecipe.Result);

            FireballAbility fireball = AssetDatabase.LoadAssetAtPath<FireballAbility>($"{basePath}/FireballAbility.asset");
            Assert.IsNotNull(fireball, "FireballAbility should be generated.");

            FrostNovaAbility frostNova = AssetDatabase.LoadAssetAtPath<FrostNovaAbility>($"{basePath}/FrostNovaAbility.asset");
            Assert.IsNotNull(frostNova, "FrostNovaAbility should be generated.");

            AbilityModifierTalent manaRegenTalent = AssetDatabase.LoadAssetAtPath<AbilityModifierTalent>($"{basePath}/ManaRegenTalent.asset");
            Assert.IsNotNull(manaRegenTalent, "ManaRegenTalent should be generated.");
        }

        [Test]
        public void PrefabGenerator_CreatesExpectedPrefabs()
        {
            // Arrange
            string basePath = "Assets/GeneratedPrefabs";

            // Act
            PrefabGenerator.GeneratePrefabs();

            // Assert
            Assert.IsTrue(AssetDatabase.IsValidFolder(basePath), "GeneratedPrefabs folder should exist.");

            GameObject networkPlayer = AssetDatabase.LoadAssetAtPath<GameObject>($"{basePath}/NetworkPlayer.prefab");
            Assert.IsNotNull(networkPlayer, "NetworkPlayer prefab should be generated.");
            Assert.IsNotNull(networkPlayer.GetComponent<ByteWar.Networking.NetworkPlayer>(), "NetworkPlayer should have NetworkPlayer component.");

            GameObject resourceNode = AssetDatabase.LoadAssetAtPath<GameObject>($"{basePath}/ResourceNode.prefab");
            Assert.IsNotNull(resourceNode, "ResourceNode prefab should be generated.");
            Assert.IsNotNull(resourceNode.GetComponent<ResourceNode>(), "ResourceNode should have ResourceNode component.");

            GameObject enemyAI = AssetDatabase.LoadAssetAtPath<GameObject>($"{basePath}/EnemyAI.prefab");
            Assert.IsNotNull(enemyAI, "EnemyAI prefab should be generated.");
            Assert.IsNotNull(enemyAI.GetComponent<EnemyAI>(), "EnemyAI should have EnemyAI component.");

            GameObject networkManager = AssetDatabase.LoadAssetAtPath<GameObject>($"{basePath}/NetworkManager.prefab");
            Assert.IsNotNull(networkManager, "NetworkManager prefab should be generated.");
            Assert.IsNotNull(networkManager.GetComponent<Unity.Netcode.NetworkManager>(), "NetworkManager should have NetworkManager component.");
        }

        [Test]
        public void SceneGenerator_CreatesTestScene()
        {
            // Arrange
            string scenePath = "Assets/Scenes/TestScene.unity";

            // Act
            SceneGenerator.GenerateTestScene();

            // Assert
            Assert.IsTrue(File.Exists(scenePath), "TestScene should be generated.");
        }
    }
}
