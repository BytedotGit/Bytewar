using System.Collections;
using System.Collections.Generic;
using ByteWar.UI;
using ByteWar.Survival;
using UnityEngine;

namespace ByteWar.Core
{
    public class AutoTestScenarioAssetDeployUI : IAutoTestScenario
    {
        private const string CatalogResourcePath = "Generated/DeployableAssetCatalog";

        private static readonly string[] ExpectedLargeTreeVariationResourcePaths =
        {
            "Generated/LargeTree/Variants/Seedling/LargeTree_Seedling",
            "Generated/LargeTree/Variants/Sapling/LargeTree_Sapling",
            "Generated/LargeTree/Variants/Young/LargeTree_Young",
            "Generated/LargeTree/Variants/Mature/LargeTree_Mature",
            "Generated/LargeTree/Variants/Adult/LargeTree_Adult",
        };

        public string Name => "AssetDeployUI";

        public IEnumerator Run(AutoTesterContext ctx)
        {
            Debug.Log("[AutoTester] Running AssetDeployUI scenario...");

            if (!AssetDeployUI.IsOpenShortcutPressed(true) || AssetDeployUI.IsOpenShortcutPressed(false))
            {
                Debug.LogError("[AutoTester] FAIL: AssetDeployUI - IsOpenShortcutPressed returned unexpected results.");
                Application.Quit(93);
                yield break;
            }

            var validationRoot = new GameObject("AutoTest_DeployableValidationRoot");
            validationRoot.AddComponent<Unity.Netcode.NetworkObject>();
            var validationVisual = new GameObject("AutoTest_DeployableValidationVisual");
            validationVisual.transform.SetParent(validationRoot.transform, false);
            validationVisual.AddComponent<BoxCollider>();
            validationVisual.AddComponent<LODGroup>();
            if (!AssetDeployUI.HasRequiredDeveloperSpawnComponents(validationRoot))
            {
                Object.Destroy(validationRoot);
                Debug.LogError("[AutoTester] FAIL: AssetDeployUI - HasRequiredDeveloperSpawnComponents rejected a valid prefab shape.");
                Application.Quit(94);
                yield break;
            }
            Object.Destroy(validationRoot);

            float previewScale = AssetDeployUI.ComputeRadialPreviewScale(
                new Bounds(Vector3.zero, new Vector3(4f, 2f, 2f)),
                2f,
                0.2f,
                2f);
            if (Mathf.Abs(previewScale - 0.5f) > 0.0001f)
            {
                Debug.LogError($"[AutoTester] FAIL: AssetDeployUI - ComputeRadialPreviewScale expected 0.5, got {previewScale}.");
                Application.Quit(97);
                yield break;
            }

            if (!TryValidateVariationCatalogCoverage(out string variationCatalogMsg))
            {
                Debug.LogError($"[AutoTester] FAIL: AssetDeployUI - {variationCatalogMsg}");
                Application.Quit(98);
                yield break;
            }
            Debug.Log("[AutoTester] PASS: AssetDeployUI variation catalog coverage.");

            var ui = Object.FindFirstObjectByType<AssetDeployUI>();
            if (ui == null)
            {
                Debug.LogError("[AutoTester] FAIL: AssetDeployUI - AssetDeployUI component not found in scene.");
                Application.Quit(90);
                yield break;
            }

            if (!ui.TryDeployNearLocalPlayer(out var spawned, out var msg))
            {
                Debug.LogError($"[AutoTester] FAIL: AssetDeployUI - Deploy failed: {msg}");
                Application.Quit(91);
                yield break;
            }

            if (spawned == null)
            {
                Debug.LogError("[AutoTester] FAIL: AssetDeployUI - Deploy returned success but spawned is null.");
                Application.Quit(92);
                yield break;
            }

            Debug.Log($"[AutoTester] AssetDeployUI deployed '{spawned.name}'.");
            Debug.Log("[AutoTester] PASS: AssetDeployUI scenario complete.");
            yield return null;
        }

        private static bool TryValidateVariationCatalogCoverage(out string message)
        {
            message = string.Empty;

            var catalog = Resources.Load<DeployableAssetCatalog>(CatalogResourcePath);
            if (catalog == null || catalog.Entries == null)
            {
                message = "DeployableAssetCatalog is missing or has no entries.";
                return false;
            }

            for (int i = 0; i < ExpectedLargeTreeVariationResourcePaths.Length; i++)
            {
                string expectedPath = ExpectedLargeTreeVariationResourcePaths[i];
                var entry = FindEntryByResourcePath(catalog.Entries, expectedPath);
                if (entry == null)
                {
                    message = $"Catalog missing LargeTree variation entry '{expectedPath}'.";
                    return false;
                }

                var prefab = Resources.Load<GameObject>(expectedPath);
                if (prefab == null)
                {
                    message = $"Resources prefab missing for variation '{expectedPath}'.";
                    return false;
                }

                if (!AssetDeployUI.HasRequiredDeveloperSpawnComponents(prefab))
                {
                    message = $"Variation prefab '{expectedPath}' is not server-placement-ready (missing NetworkObject/Collider/LODGroup).";
                    return false;
                }

                if (prefab.GetComponent<ResourceNode>() == null)
                {
                    message = $"Variation prefab '{expectedPath}' is missing ResourceNode for harvesting.";
                    return false;
                }
            }

            message = "OK";
            return true;
        }

        private static DeployableAssetCatalog.Entry FindEntryByResourcePath(IReadOnlyList<DeployableAssetCatalog.Entry> entries, string resourcePath)
        {
            if (entries == null || string.IsNullOrWhiteSpace(resourcePath))
                return null;

            string expected = AssetDeployUI.NormalizeResourceLoadPath(resourcePath);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;

                string actual = AssetDeployUI.NormalizeResourceLoadPath(entry.ResourcePath);
                if (string.Equals(actual, expected, System.StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            return null;
        }
    }
}
