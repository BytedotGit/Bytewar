using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace SurvivalRPG.Editor
{
    public static class UIGenerator
    {
        [MenuItem("SurvivalRPG/Generate UI Prefab")]
        public static void GenerateUIPrefab()
        {
            Debug.Log("[UIGenerator] Generating UI Prefab...");

            string basePath = "Assets/GeneratedPrefabs/UI";
            if (!AssetDatabase.IsValidFolder(basePath))
            {
                if (!AssetDatabase.IsValidFolder("Assets/GeneratedPrefabs"))
                {
                    AssetDatabase.CreateFolder("Assets", "GeneratedPrefabs");
                }
                AssetDatabase.CreateFolder("Assets/GeneratedPrefabs", "UI");
            }

            // Create Canvas
            GameObject canvasObj = new GameObject("PlayerHUD");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();

            // Create Crosshair
            GameObject crosshairObj = new GameObject("Crosshair");
            crosshairObj.transform.SetParent(canvasObj.transform);
            Image crosshairImg = crosshairObj.AddComponent<Image>();
            crosshairImg.color = new Color(1, 1, 1, 0.5f);
            RectTransform crosshairRect = crosshairObj.GetComponent<RectTransform>();
            crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
            crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
            crosshairRect.pivot = new Vector2(0.5f, 0.5f);
            crosshairRect.anchoredPosition = Vector2.zero;
            crosshairRect.sizeDelta = new Vector2(10, 10);

            // Create Vitals Panel
            GameObject vitalsObj = new GameObject("VitalsPanel");
            vitalsObj.transform.SetParent(canvasObj.transform);
            RectTransform vitalsRect = vitalsObj.AddComponent<RectTransform>();
            vitalsRect.anchorMin = new Vector2(0, 1);
            vitalsRect.anchorMax = new Vector2(0, 1);
            vitalsRect.pivot = new Vector2(0, 1);
            vitalsRect.anchoredPosition = new Vector2(20, -20);
            vitalsRect.sizeDelta = new Vector2(200, 100);

            // Health Bar
            GameObject healthBar = CreateBar("HealthBar", vitalsObj.transform, Color.red, new Vector2(0, 0));
            // Mana Bar
            GameObject manaBar = CreateBar("ManaBar", vitalsObj.transform, Color.blue, new Vector2(0, -30));
            // Stamina Bar
            GameObject staminaBar = CreateBar("StaminaBar", vitalsObj.transform, Color.green, new Vector2(0, -60));

            // Add PlayerVitalsUI component
            SurvivalRPG.UI.PlayerVitalsUI vitalsUI = canvasObj.AddComponent<SurvivalRPG.UI.PlayerVitalsUI>();
            vitalsUI.healthSlider = healthBar.GetComponent<Slider>();
            vitalsUI.manaSlider = manaBar.GetComponent<Slider>();
            vitalsUI.staminaSlider = staminaBar.GetComponent<Slider>();

            // Create Action Bar
            GameObject actionBarObj = new GameObject("ActionBar");
            actionBarObj.transform.SetParent(canvasObj.transform);
            RectTransform actionBarRect = actionBarObj.AddComponent<RectTransform>();
            actionBarRect.anchorMin = new Vector2(0.5f, 0);
            actionBarRect.anchorMax = new Vector2(0.5f, 0);
            actionBarRect.pivot = new Vector2(0.5f, 0);
            actionBarRect.anchoredPosition = new Vector2(0, 20);
            actionBarRect.sizeDelta = new Vector2(400, 60);

            HorizontalLayoutGroup layout = actionBarObj.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 10;

            for (int i = 0; i < 5; i++)
            {
                GameObject slot = new GameObject($"Slot_{i}");
                slot.transform.SetParent(actionBarObj.transform);
                Image slotImg = slot.AddComponent<Image>();
                slotImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                RectTransform slotRect = slot.GetComponent<RectTransform>();
                slotRect.sizeDelta = new Vector2(50, 50);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(canvasObj, $"{basePath}/PlayerHUD.prefab");
            Object.DestroyImmediate(canvasObj);

            Debug.Log($"[UIGenerator] Created PlayerHUD prefab at {basePath}/PlayerHUD.prefab");
        }

        private static GameObject CreateBar(string name, Transform parent, Color color, Vector2 position)
        {
            GameObject barObj = new GameObject(name);
            barObj.transform.SetParent(parent);
            RectTransform rect = barObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(200, 20);

            // Background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(barObj.transform);
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Fill Area
            GameObject fillAreaObj = new GameObject("Fill Area");
            fillAreaObj.transform.SetParent(barObj.transform);
            RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(2, 2);
            fillAreaRect.offsetMax = new Vector2(-2, -2);

            // Fill
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform);
            Image fillImg = fillObj.AddComponent<Image>();
            fillImg.color = color;
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Slider slider = barObj.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.value = 1f;

            return barObj;
        }
    }
}