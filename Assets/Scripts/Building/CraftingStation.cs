using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using ByteWar.Survival;

namespace ByteWar.Building
{
    public class CraftingStation : NetworkBehaviour
    {
        public List<CraftingRecipe> AvailableRecipes;

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestCraftServerRpc(int recipeIndex, ulong clientId)
        {
            if (recipeIndex < 0 || recipeIndex >= AvailableRecipes.Count) return;

            CraftingRecipe recipe = AvailableRecipes[recipeIndex];

            // Find the player who requested the craft
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                InventoryComponent playerInventory = client.PlayerObject.GetComponent<InventoryComponent>();
                if (playerInventory == null) return;

                // Check if player has all ingredients
                bool canCraft = true;
                foreach (var ingredient in recipe.Ingredients)
                {
                    if (!playerInventory.HasItem(ingredient.Item, ingredient.Amount))
                    {
                        canCraft = false;
                        break;
                    }
                }

                if (canCraft)
                {
                    // Remove ingredients
                    foreach (var ingredient in recipe.Ingredients)
                    {
                        playerInventory.RemoveItem(ingredient.Item, ingredient.Amount);
                    }

                    // Add result
                    for (int i = 0; i < recipe.ResultAmount; i++)
                    {
                        playerInventory.AddItem(recipe.Result);
                    }

                    Debug.Log($"Player {clientId} crafted {recipe.Result.ItemName}");
                }
                else
                {
                    Debug.Log($"Player {clientId} does not have the required ingredients for {recipe.RecipeName}");
                }
            }
        }
    }
}
