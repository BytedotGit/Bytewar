using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using SurvivalRPG.Building;
using System.Collections.Generic;

namespace SurvivalRPG.UI
{
    public class CraftingUI : MonoBehaviour
    {
        [SerializeField] private CraftingStation currentStation;
        [SerializeField] private Transform recipeContainer;
        [SerializeField] private GameObject recipePrefab; // Prefab with Button and TextMeshProUGUI

        private List<GameObject> _spawnedRecipes = new List<GameObject>();

        private void Update()
        {
            // In a real game, this would be set when interacting with a station
            if (currentStation == null)
            {
                FindCraftingStation();
                return;
            }
        }

        private void FindCraftingStation()
        {
            // Just find the first one for testing
            currentStation = Object.FindFirstObjectByType<CraftingStation>();
            if (currentStation != null)
            {
                Debug.Log("CraftingUI bound to CraftingStation.");
                UpdateUI();
            }
        }

        public void SetCraftingStation(CraftingStation station)
        {
            currentStation = station;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (currentStation == null || recipeContainer == null || recipePrefab == null) return;

            // Clear old UI
            foreach (var obj in _spawnedRecipes)
            {
                Destroy(obj);
            }
            _spawnedRecipes.Clear();

            // Spawn new UI
            for (int i = 0; i < currentStation.AvailableRecipes.Count; i++)
            {
                int index = i;
                CraftingRecipe recipe = currentStation.AvailableRecipes[i];
                if (recipe != null)
                {
                    GameObject go = Instantiate(recipePrefab, recipeContainer);

                    TextMeshProUGUI text = go.GetComponentInChildren<TextMeshProUGUI>();
                    if (text != null)
                    {
                        string reqs = "";
                        foreach (var req in recipe.Ingredients)
                        {
                            reqs += $"{req.Amount} {req.Item.ItemName}, ";
                        }
                        text.text = $"{recipe.RecipeName} (Requires: {reqs.TrimEnd(',', ' ')})";
                    }

                    Button btn = go.GetComponentInChildren<Button>();
                    if (btn != null)
                    {
                        btn.onClick.AddListener(() => OnCraftClicked(index));
                    }

                    _spawnedRecipes.Add(go);
                }
            }
        }

        private void OnCraftClicked(int recipeIndex)
        {
            if (currentStation == null) return;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
            {
                ulong clientId = NetworkManager.Singleton.LocalClientId;
                currentStation.RequestCraftServerRpc(recipeIndex, clientId);
                Debug.Log($"CraftingUI: Requested craft for recipe index {recipeIndex}");
            }
        }
    }
}