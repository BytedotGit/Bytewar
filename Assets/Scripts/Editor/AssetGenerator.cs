using UnityEngine;
using UnityEditor;
using ByteWar.Survival;
using ByteWar.Building;
using ByteWar.Abilities.Mage;
using ByteWar.Abilities.Talents;
using System.Collections.Generic;
using System.IO;

namespace ByteWar.Editor
{
    public static class AssetGenerator
    {
        [MenuItem("ByteWar/Generate Assets")]
        public static void GenerateAssets()
        {
            Debug.Log("[AssetGenerator] Starting asset generation...");

            string basePath = "Assets/GeneratedAssets";
            if (!AssetDatabase.IsValidFolder(basePath))
            {
                AssetDatabase.CreateFolder("Assets", "GeneratedAssets");
                Debug.Log($"[AssetGenerator] Created folder: {basePath}");
            }

            // Generate Items
            Item wood = CreateAsset<Item>($"{basePath}/Wood.asset");
            wood.ItemName = "Wood";
            wood.Description = "A basic crafting material.";
            wood.IsEquippable = false;

            Item stone = CreateAsset<Item>($"{basePath}/Stone.asset");
            stone.ItemName = "Stone";
            stone.Description = "A basic crafting material.";
            stone.IsEquippable = false;

            Item basicStaff = CreateAsset<Item>($"{basePath}/BasicStaff.asset");
            basicStaff.ItemName = "Basic Staff";
            basicStaff.Description = "A simple wooden staff for casting spells.";
            basicStaff.IsEquippable = true;
            basicStaff.DamageBonus = 5f;
            basicStaff.ManaBonus = 20f;

            // Generate Recipe
            CraftingRecipe staffRecipe = CreateAsset<CraftingRecipe>($"{basePath}/StaffRecipe.asset");
            staffRecipe.RecipeName = "Basic Staff Recipe";
            staffRecipe.Ingredients = new List<RecipeIngredient>
            {
                new RecipeIngredient { Item = wood, Amount = 5 },
                new RecipeIngredient { Item = stone, Amount = 2 }
            };
            staffRecipe.Result = basicStaff;
            staffRecipe.ResultAmount = 1;

            // Generate Abilities
            FireballAbility fireball = CreateAsset<FireballAbility>($"{basePath}/FireballAbility.asset");
            fireball.AbilityName = "Fireball";
            fireball.ManaCost = 15f;
            fireball.Cooldown = 2f;
            fireball.ProjectileSpeed = 20f;

            FrostNovaAbility frostNova = CreateAsset<FrostNovaAbility>($"{basePath}/FrostNovaAbility.asset");
            frostNova.AbilityName = "Frost Nova";
            frostNova.ManaCost = 25f;
            frostNova.Cooldown = 8f;
            frostNova.Radius = 5f;

            // Generate Talent
            AbilityModifierTalent manaRegenTalent = CreateAsset<AbilityModifierTalent>($"{basePath}/ManaRegenTalent.asset");
            manaRegenTalent.TalentName = "Mana Regeneration";
            manaRegenTalent.Description = "Increases maximum mana and reduces Fireball mana cost.";
            manaRegenTalent.MaxRank = 3;
            manaRegenTalent.ManaBonus = 50f;
            manaRegenTalent.TargetAbilityName = "Fireball";
            manaRegenTalent.ManaCostReduction = 5f;

            // Generate Building Recipes
            BuildingRecipe foundationRecipe = CreateAsset<BuildingRecipe>($"{basePath}/FoundationRecipe.asset");
            foundationRecipe.RecipeName = "Foundation";
            foundationRecipe.PieceType = BuildingPieceType.Foundation;
            foundationRecipe.Cost = new List<RecipeIngredient>
            {
                new RecipeIngredient { Item = wood, Amount = 4 },
                new RecipeIngredient { Item = stone, Amount = 2 }
            };

            BuildingRecipe wallRecipe = CreateAsset<BuildingRecipe>($"{basePath}/WallRecipe.asset");
            wallRecipe.RecipeName = "Wall";
            wallRecipe.PieceType = BuildingPieceType.Wall;
            wallRecipe.Cost = new List<RecipeIngredient>
            {
                new RecipeIngredient { Item = wood, Amount = 3 },
                new RecipeIngredient { Item = stone, Amount = 1 }
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AssetGenerator] Asset generation completed successfully.");
        }

        private static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
                Debug.Log($"[AssetGenerator] Created new asset at {path}");
            }
            else
            {
                EditorUtility.SetDirty(asset);
                Debug.Log($"[AssetGenerator] Updated existing asset at {path}");
            }
            return asset;
        }
    }
}
