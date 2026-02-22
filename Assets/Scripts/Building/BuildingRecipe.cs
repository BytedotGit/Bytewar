using UnityEngine;
using System.Collections.Generic;
using ByteWar.Survival;

namespace ByteWar.Building
{
    /// <summary>
    /// Defines the cost and output for placing a building piece.
    /// Separate from CraftingRecipe to keep the building pipeline independent.
    /// </summary>
    [CreateAssetMenu(fileName = "NewBuildingRecipe", menuName = "ByteWar/Building/BuildingRecipe")]
    public class BuildingRecipe : ScriptableObject
    {
        public string RecipeName;
        public BuildingPieceType PieceType;
        public List<RecipeIngredient> Cost;

        /// <summary>
        /// Returns true if the inventory contains all required ingredients.
        /// </summary>
        public bool CanAfford(InventoryComponent inventory)
        {
            if (inventory == null) return false;
            foreach (var ingredient in Cost)
            {
                if (!inventory.HasItem(ingredient.Item, ingredient.Amount))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Deducts ingredients. Call only on the server.
        /// Returns true if successful.
        /// </summary>
        public bool ConsumeResources(InventoryComponent inventory)
        {
            if (!CanAfford(inventory))
            {
                Debug.LogWarning($"[BuildingRecipe] Cannot afford '{RecipeName}'.");
                return false;
            }

            foreach (var ingredient in Cost)
            {
                inventory.RemoveItem(ingredient.Item, ingredient.Amount);
            }
            Debug.Log($"[BuildingRecipe] Consumed resources for '{RecipeName}'.");
            return true;
        }
    }
}
