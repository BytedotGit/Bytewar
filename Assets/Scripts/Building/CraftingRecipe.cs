using UnityEngine;
using System.Collections.Generic;
using ByteWar.Survival;

namespace ByteWar.Building
{
    [System.Serializable]
    public struct RecipeIngredient
    {
        public Item Item;
        public int Amount;
    }

    [CreateAssetMenu(fileName = "NewRecipe", menuName = "ByteWar/Building/CraftingRecipe")]
    public class CraftingRecipe : ScriptableObject
    {
        public string RecipeName;
        public List<RecipeIngredient> Ingredients;
        public Item Result;
        public int ResultAmount = 1;
    }
}
