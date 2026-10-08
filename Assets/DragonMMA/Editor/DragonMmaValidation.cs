using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace DragonMMA.EditorTools
{
    public static class DragonMmaValidation
    {
        private static TestRunnerApi testRunner;
        private static TestCallbacks callbacks;

        [MenuItem("Dragon MMA/Run EditMode Tests")]
        public static void RunEditModeTests()
        {
            testRunner = ScriptableObject.CreateInstance<TestRunnerApi>();
            callbacks = new TestCallbacks();
            testRunner.RegisterCallbacks(callbacks);
            testRunner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
            Debug.Log("[Dragon MMA Tests] Started EditMode suite.");
        }

        [MenuItem("Dragon MMA/Build Windows Player")]
        public static void BuildWindowsPlayer()
        {
            const string output = "Builds/Windows-Upgrade/DragonMMAIdle.exe";
            string directory = Path.GetDirectoryName(output);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/DragonMMA/Scenes/DesktopOverlayScene.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                string licenses = Path.Combine(directory, "Licenses");
                Directory.CreateDirectory(licenses);
                File.Copy("Assets/DragonMMA/Fonts/OFL.txt", Path.Combine(licenses, "NanumGothic-OFL.txt"), true);
                Debug.Log($"[Dragon MMA Build] SUCCESS size={summary.totalSize} bytes errors={summary.totalErrors} warnings={summary.totalWarnings} output={Path.GetFullPath(output)}");
            }
            else
                Debug.LogError($"[Dragon MMA Build] FAILED result={summary.result} errors={summary.totalErrors} warnings={summary.totalWarnings}");
        }

        private sealed class TestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                Debug.Log($"[Dragon MMA Tests] Running {testsToRun.TestCaseCount} test cases.");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Directory.CreateDirectory("Artifacts");
                TestRunnerApi.SaveResultToFile(result, "Artifacts/upgrade-editmode-results.xml");
                Debug.Log($"[Dragon MMA Tests] Finished status={result.ResultState} passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} inconclusive={result.InconclusiveCount}");
                if (testRunner != null)
                {
                    testRunner.UnregisterCallbacks(this);
                    Object.DestroyImmediate(testRunner);
                    testRunner = null;
                    callbacks = null;
                }
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren || result.TestStatus != TestStatus.Failed) return;
                Debug.LogError($"[Dragon MMA Tests] FAIL {result.FullName}: {result.Message}\n{result.StackTrace}");
            }
        }
    }
}
