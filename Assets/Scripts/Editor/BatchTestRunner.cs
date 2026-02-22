#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace SurvivalRPG.Editor
{
    public static class BatchTestRunner
    {
        private static TestRunnerApi _api;
        private static bool _running;

        public static void RunEditModeTests()
        {
            if (_running)
            {
                Debug.LogWarning("[BatchTestRunner] Test run already in progress.");
                return;
            }

            _running = true;
            Debug.Log("[BatchTestRunner] Starting EditMode tests...");

            _api = new TestRunnerApi();
            _api.RegisterCallbacks(new Callbacks());

            var filter = new Filter
            {
                testMode = TestMode.EditMode
            };

            _api.Execute(new ExecutionSettings(filter));
        }

        private sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log($"[BatchTestRunner] RunStarted: {testsToRun.Name}");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Debug.Log($"[BatchTestRunner] RunFinished: Passed={result.PassCount} Failed={result.FailCount} Skipped={result.SkipCount} Inconclusive={result.InconclusiveCount}");

                // Exit code mirrors CI conventions.
                int exitCode = result.FailCount > 0 ? 1 : 0;
                Debug.Log($"[BatchTestRunner] Exiting with code {exitCode}");
                EditorApplication.Exit(exitCode);
            }

            public void TestStarted(ITestAdaptor test)
            {
                // Intentionally quiet to avoid log spam in batchmode.
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.TestStatus == TestStatus.Failed)
                {
                    Debug.LogError($"[BatchTestRunner] FAIL: {result.Name} :: {result.Message}\n{result.StackTrace}");
                }
            }
        }
    }
}
#endif
