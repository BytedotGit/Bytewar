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
