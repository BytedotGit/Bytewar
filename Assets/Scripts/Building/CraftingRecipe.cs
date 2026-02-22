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
        [SerializeField] private string _recipeName;
        [SerializeField] private List<RecipeIngredient> _ingredients;
        [SerializeField] private Item _result;
        [SerializeField] private int _resultAmount = 1;

        public string RecipeName { get => _recipeName; internal set => _recipeName = value; }
        public IReadOnlyList<RecipeIngredient> Ingredients => _ingredients;
        public Item Result { get => _result; internal set => _result = value; }
        public int ResultAmount { get => _resultAmount; internal set => _resultAmount = value; }

        /// <summary>Sets the ingredients list directly. Internal for test construction.</summary>
        internal void SetIngredients(List<RecipeIngredient> ingredients) => _ingredients = ingredients;
    }
}
