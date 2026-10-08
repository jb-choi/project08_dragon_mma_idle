using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DragonMMA.EditorTools
{
    // Command-line-safe play-mode QA. SessionState keeps the small state machine
    // alive across Unity's play-mode domain reloads.
    [InitializeOnLoad]
    public static class CombatDirectingFinalQa
    {
        private const string RunningKey = "DragonMMA.CombatDirectingQa.Running";
        private const string StageKey = "DragonMMA.CombatDirectingQa.Stage";
        private const string FailureKey = "DragonMMA.CombatDirectingQa.Failure";
        private const string ScenePath = "Assets/DragonMMA/Scenes/DesktopOverlayScene.unity";
        private const string ArtifactRoot = "Artifacts/CombatDirectingRedo-20261007";
        private const string ReportPath = ArtifactRoot + "/runtime-smoke.txt";
        private static int frames;

        static CombatDirectingFinalQa()
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            Attach();
            EditorApplication.delayCall += ResumeAfterReload;
        }

        public static void Run()
        {
            if (SessionState.GetBool(RunningKey, false)) throw new InvalidOperationException("Combat directing QA is already running.");
            Directory.CreateDirectory(ArtifactRoot);
            File.WriteAllText(ReportPath, "Combat Directing Runtime QA\n");
            SessionState.SetBool(RunningKey, true);
            SessionState.SetInt(StageKey, 1);
            SessionState.EraseString(FailureKey);
            DragonMmaUpgradeValidation.PrepareIsolatedPlay();
            Attach();
            EditorSceneManager.OpenScene(DragonAnimationLabAuthoring.ScenePath);
            DragonAnimationLabAuthoring.ValidateScene();
            Append("AnimationTestScene edit validation PASS");
            EditorApplication.isPlaying = true;
        }

        private static void Attach()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update -= OnUpdate;
        }

        private static void ResumeAfterReload()
        {
            int stage = SessionState.GetInt(StageKey, 0);
            if (EditorApplication.isPlaying && (stage == 1 || stage == 4)) BeginPlay(stage == 1 ? 2 : 5);
            else if (!EditorApplication.isPlaying && stage == 3) BeginMainScene();
            else if (!EditorApplication.isPlaying && (stage == 6 || stage == 90)) Finish(stage == 6);
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            int stage = SessionState.GetInt(StageKey, 0);
            if (state == PlayModeStateChange.EnteredPlayMode && stage == 1) BeginPlay(2);
            else if (state == PlayModeStateChange.EnteredPlayMode && stage == 4) BeginPlay(5);
            else if (state == PlayModeStateChange.EnteredEditMode && stage == 3) BeginMainScene();
            else if (state == PlayModeStateChange.EnteredEditMode && (stage == 6 || stage == 90)) Finish(stage == 6);
        }

        private static void BeginPlay(int runningStage)
        {
            SessionState.SetInt(StageKey, runningStage);
            frames = 5;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            if (--frames > 0) return;
            EditorApplication.update -= OnUpdate;
            try
            {
                int stage = SessionState.GetInt(StageKey, 0);
                if (stage == 2) RunAnimationLab();
                else if (stage == 5) RunMainGame();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        private static void RunAnimationLab()
        {
            DragonAnimationLabAuthoring.SmokeTest();
            AnimationLabWorkbenchAuthoring.SmokeTest();
            var lab = UnityEngine.Object.FindFirstObjectByType<DragonAnimationLab>();
            lab.SetMode(DragonAnimationLab.PreviewMode.Combat);
            lab.SetSpecies(DragonKind.Baby);
            lab.SetAllSkills(true);
            float third = DragonCombatChoreography.CycleSeconds(DragonKind.Baby, 0) + DragonCombatChoreography.CycleSeconds(DragonKind.Baby, 1);
            lab.Seek(third + DragonCombatChoreography.EnemyStart(DragonKind.Baby, 2) + DragonAnimationTiming.Contact(DragonKind.Baby) + .01f);
            DragonAnimationLabAuthoring.CapturePreview();
            File.Copy("Artifacts/AnimationLab/preview.png", ArtifactRoot + "/animation-test-after.png", true);
            Append("AnimationTestScene runtime smoke PASS; selected dragon Power contact captured");
            SessionState.SetInt(StageKey, 3);
            EditorApplication.isPlaying = false;
        }

        private static void BeginMainScene()
        {
            if (SessionState.GetInt(StageKey, 0) != 3) return;
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetInt(StageKey, 4);
            EditorApplication.isPlaying = true;
        }

        private static void RunMainGame()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<DragonMmaGame>();
            if (game == null || game.DebugSession == null) throw new InvalidOperationException("Main game did not initialize in play mode.");

            float third = DragonCombatChoreography.CycleSeconds(DragonKind.Baby, 0) + DragonCombatChoreography.CycleSeconds(DragonKind.Baby, 1);
            float heavyContact = third + DragonCombatChoreography.EnemyStart(DragonKind.Baby, 2) + DragonAnimationTiming.Contact(DragonKind.Baby) + .01f;
            CaptureFightAt(game, Mathf.Max(0, heavyContact - .10f), "after-anticipation.png");
            CaptureFightAt(game, heavyContact, "after-contact.png");
            CaptureFightAt(game, heavyContact + .22f, "after-follow-through.png");
            Require(game.DebugData.phase == HuntPhase.Fighting, "Heavy-contact capture left Fighting phase.");
            ValidateVisibleExpansion(ArtifactRoot + "/before-heavy.png", ArtifactRoot + "/after-contact.png");

            int beforeWin = game.DebugData.cart.Count;
            game.DebugSession.Tick(game.DebugData.phaseRemaining + .001f, DragonMmaGame.Now);
            game.DebugView.Refresh(true); game.DebugView.Tick(0);
            Require(game.DebugData.phase == HuntPhase.Result, "Winning battle did not enter Result.");
            Require(game.DebugData.cart.Count == beforeWin + 1, "Winning reward was not granted exactly once.");
            int afterWin = game.DebugData.cart.Count;
            game.DebugSession.Tick(.25f, DragonMmaGame.Now);
            Require(game.DebugData.cart.Count == afterWin, "Result phase duplicated the capture reward.");
            game.DebugView.Tick(0);
            DragonMmaUpgradeValidation.Capture(ArtifactRoot + "/main-game-win-after.png");

            game.DebugSession.DebugForceEncounter(DragonKind.Headbutt, 99f);
            game.DebugSession.Tick(game.DebugData.phaseRemaining + .001f, DragonMmaGame.Now);
            game.DebugView.Refresh(true); game.DebugView.Tick(0);
            Require(game.DebugData.phase == HuntPhase.Recovery, "Losing battle did not enter Recovery.");
            Require(game.DebugData.cart.Count == afterWin, "Loss changed capture rewards.");
            DragonMmaUpgradeValidation.Capture(ArtifactRoot + "/main-game-loss-after.png");

            Append("Main game runtime PASS; identical Baby/Power anticipation + contact + follow-through captured; win + loss reward applied once");
            Append(game.GetDebugSummary());
            SessionState.SetInt(StageKey, 6);
            EditorApplication.isPlaying = false;
        }

        private static void CaptureFightAt(DragonMmaGame game, float elapsed, string fileName)
        {
            game.DebugSession.DebugForceEncounter(DragonKind.Baby, 0);
            game.DebugSession.Tick(elapsed, DragonMmaGame.Now);
            game.DebugView.Refresh(true); game.DebugView.Tick(0);
            Require(game.DebugData.phase == HuntPhase.Fighting, fileName + " left Fighting phase.");
            DragonMmaUpgradeValidation.Capture(ArtifactRoot + "/" + fileName);
        }

        private static void ValidateVisibleExpansion(string beforePath, string afterPath)
        {
            Require(File.Exists(beforePath), "Identical-scenario baseline is missing: " + beforePath);
            int beforeTop = TopVisibleRow(beforePath, out int beforeWidth, out int beforeHeight, out float beforeVariation);
            int afterTop = TopVisibleRow(afterPath, out int afterWidth, out int afterHeight, out float afterVariation);
            Require(beforeWidth == afterWidth && beforeHeight == afterHeight, "Before/after dimensions differ.");
            Require(beforeVariation >= .02f, "Baseline capture is blank or nearly uniform. variation=" + beforeVariation);
            Require(afterVariation >= .02f, "After capture is blank or nearly uniform. variation=" + afterVariation);
            int gain = afterTop - beforeTop;
            Append($"Visible-content top row: before={beforeTop}, after={afterTop}, gain={gain}px; variation before={beforeVariation:P1}, after={afterVariation:P1}");
            Require(gain >= 120, "Combat stage did not materially expand in the actual 1920x720 frame. gain=" + gain);
        }

        private static int TopVisibleRow(string path, out int width, out int height, out float variation)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Require(texture.LoadImage(File.ReadAllBytes(path)), "Could not decode QA image: " + path);
                width = texture.width; height = texture.height;
                Color32[] pixels = texture.GetPixels32();
                Color32 background = pixels[(height - 1) * width];
                int different = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    if (Mathf.Abs(pixel.r - background.r) + Mathf.Abs(pixel.g - background.g) + Mathf.Abs(pixel.b - background.b) > 24)
                        different++;
                }
                variation = different / (float)pixels.Length;
                for (int y = height - 1; y >= 0; y--)
                    for (int x = 0; x < width; x++)
                    {
                        Color32 pixel = pixels[y * width + x];
                        if (pixel.a > 10 && Mathf.Abs(pixel.r - background.r) + Mathf.Abs(pixel.g - background.g) +
                            Mathf.Abs(pixel.b - background.b) > 24) return y;
                    }
                return -1;
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Append(string line) => File.AppendAllText(ReportPath, line + Environment.NewLine);

        private static void Fail(Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetString(FailureKey, exception.ToString());
            Append("FAIL: " + exception);
            SessionState.SetInt(StageKey, 90);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            else Finish(false);
        }

        private static void Finish(bool success)
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            DragonMmaUpgradeValidation.EndIsolation();
            string failure = SessionState.GetString(FailureKey, "");
            SessionState.EraseBool(RunningKey);
            SessionState.EraseInt(StageKey);
            SessionState.EraseString(FailureKey);
            if (success) Debug.Log("[Combat Directing QA] Runtime smoke PASS. " + Path.GetFullPath(ReportPath));
            else Debug.LogError("[Combat Directing QA] Runtime smoke FAILED. " + failure);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
