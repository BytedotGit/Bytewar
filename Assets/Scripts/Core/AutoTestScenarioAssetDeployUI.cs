using System.Collections;
using ByteWar.UI;
using UnityEngine;

namespace ByteWar.Core
{
    public class AutoTestScenarioAssetDeployUI : IAutoTestScenario
    {
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
    }
}
