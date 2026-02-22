using UnityEngine;
using System.Collections.Generic;
using SurvivalRPG.Survival;

namespace SurvivalRPG.Building
{
    [System.Serializable]
    public struct RecipeIngredient
    {
        public Item Item;
        public int Amount;
    }

    [CreateAssetMenu(fileName = "NewRecipe", menuName = "SurvivalRPG/Building/CraftingRecipe")]
    public class CraftingRecipe : ScriptableObject
    {
        public string RecipeName;
        public List<RecipeIngredient> Ingredients;
        public Item Result;
        public int ResultAmount = 1;
    }
}
