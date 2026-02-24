using UnityEngine;
using System.Collections.Generic;
using ByteWar.Core;
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
        [SerializeField] private string _recipeName;
        [SerializeField] private BuildingPieceType _pieceType;
        [SerializeField] private List<RecipeIngredient> _cost;

        public string RecipeName { get => _recipeName; internal set => _recipeName = value; }
        public BuildingPieceType PieceType { get => _pieceType; internal set => _pieceType = value; }
        public IReadOnlyList<RecipeIngredient> Cost => _cost;

        /// <summary>Sets the cost list directly. Internal for test construction.</summary>
        internal void SetCost(List<RecipeIngredient> cost) => _cost = cost;

        /// <summary>
        /// Returns true if the inventory contains all required ingredients.
        /// </summary>
        public bool CanAfford(InventoryComponent inventory)
        {
            if (GameConstants.IsDevMode()) return true;
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
            if (GameConstants.IsDevMode())
            {
                Debug.Log($"[BuildingRecipe] DevMode: skipping resource consumption for '{RecipeName}'.");
                return true;
            }

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
