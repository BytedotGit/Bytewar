#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ByteWar.Editor
{
    public static class BatchTestRunner
    {
        private static TestRunnerApi _api;
        private static bool _running;

        public static void RunEditModeTests()
        {
            RunTests(TestMode.EditMode);
        }

        public static void RunPlayModeTests()
        {
            RunTests(TestMode.PlayMode);
        }

        private static void RunTests(TestMode mode)
        {
            if (_running)
            {
                Debug.LogWarning("[BatchTestRunner] Test run already in progress.");
                return;
            }

            _running = true;
            Debug.Log($"[BatchTestRunner] Starting {mode} tests...");

            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            _api.RegisterCallbacks(new Callbacks(mode));

            var filter = new Filter
            {
                testMode = mode
            };

            _api.Execute(new ExecutionSettings(filter));
        }

        private sealed class Callbacks : ICallbacks
        {
            private readonly TestMode _mode;

            public Callbacks(TestMode mode)
            {
                _mode = mode;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log($"[BatchTestRunner] RunStarted({_mode}): {testsToRun.Name}");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Debug.Log($"[BatchTestRunner] RunFinished({_mode}): Passed={result.PassCount} Failed={result.FailCount} Skipped={result.SkipCount} Inconclusive={result.InconclusiveCount}");

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
