using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DragonMMA.EditorTools
{
    // Captures the same walk and planted-contact timings used by Animation Test and gameplay.
    [InitializeOnLoad]
    public static class AnimationQualityFinalQa
    {
        private const string RunningKey = "DragonMMA.AnimationQualityQa.Running";
        private const string StageKey = "DragonMMA.AnimationQualityQa.Stage";
        private const string FailureKey = "DragonMMA.AnimationQualityQa.Failure";
        private const string PreviousSceneKey = "DragonMMA.AnimationQualityQa.PreviousScene";
        private const string MainScenePath = "Assets/DragonMMA/Scenes/DesktopOverlayScene.unity";
        private const string ArtifactRoot = "Artifacts/CombatAnimationBounceQa-20261008";
        private const string ReportPath = ArtifactRoot + "/runtime-smoke.txt";
        private static int frames;

        static AnimationQualityFinalQa()
        {
            if (!SessionState.GetBool(RunningKey, false)) return;
            Attach(); EditorApplication.delayCall += ResumeAfterReload;
        }

        [MenuItem("Dragon MMA/QA/Run Animation Bounce QA")]
        public static void Run()
        {
            if (SessionState.GetBool(RunningKey, false)) throw new InvalidOperationException("Animation quality QA is already running.");
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!Application.isBatchMode && activeScene.isDirty)
                throw new InvalidOperationException("Save the current scene before running animation QA so it can be restored safely.");
            Directory.CreateDirectory(ArtifactRoot);
            string mechanics = FighterWalkQualityUpgrade.ValidateBounceMechanics();
            File.WriteAllText(ReportPath, "Animation Quality Runtime QA\n" + mechanics + Environment.NewLine);
            SessionState.SetString(PreviousSceneKey, activeScene.IsValid() ? activeScene.path : "");
            SessionState.SetBool(RunningKey, true); SessionState.SetInt(StageKey, 1); SessionState.EraseString(FailureKey);
            DragonMmaUpgradeValidation.PrepareIsolatedPlay(); Attach();
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

        private static void BeginPlay(int stage)
        {
            SessionState.SetInt(StageKey, stage); frames = 5;
            EditorApplication.update -= OnUpdate; EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            if (--frames > 0) return;
            EditorApplication.update -= OnUpdate;
            try
            {
                int stage = SessionState.GetInt(StageKey, 0);
                if (stage == 2) RunLab(); else if (stage == 5) RunMainGame();
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void RunLab()
        {
            DragonAnimationLabAuthoring.SmokeTest();
            var lab = UnityEngine.Object.FindFirstObjectByType<DragonAnimationLab>();
            Require(lab != null, "Animation Lab runtime controller missing.");
            lab.SelectFighterPose("walk"); lab.SetLoop(true); lab.SetPlaying(false);
            float duration = FighterCombatTiming.Duration("walk");
            float integratedTravel = 0;
            float sampleDt = duration / 8f;
            for (int sample = 0; sample < 16; sample++)
            {
                int frame = sample % 8;
                float at = sample * sampleDt;
                lab.Seek(at);
                DragonAnimationLabAuthoring.CapturePreview();
                File.Copy("Artifacts/AnimationLab/preview.png", ArtifactRoot + "/walk-loop-" + (sample / 8) + "-frame-" + frame + ".png", true);
                float multiplier = AnimationMotionQuality.WalkSpeedMultiplier(at, duration);
                integratedTravel += AnimationMotionQuality.RecommendedWalkGroundSpeed * multiplier * sampleDt;
                Append($"walk sample={sample:00} loop={sample / 8} frame={frame} beat={AnimationMotionQuality.WalkBeat(frame)} floor={AnimationMotionQuality.WalkFloorPixel(frame)}px speedMultiplier={multiplier:0.00}");
            }
            float expectedTravel = AnimationMotionQuality.RecommendedWalkGroundSpeed * duration * 2;
            Require(Mathf.Abs(integratedTravel - expectedTravel) <= .05f,
                $"Two-loop walk distance mismatch. actual={integratedTravel:0.000}px expected={expectedTravel:0.000}px");

            lab.SetMode(DragonAnimationLab.PreviewMode.Combat); lab.SetSpecies(DragonKind.Baby); lab.SetAllSkills(false);
            float contact = DragonCombatChoreography.CounterStart(DragonKind.Baby, 0) + FighterCombatTiming.Contact("punch");
            lab.Seek(contact - .02f); CaptureLab("lab-approach-planted.png");
            lab.Seek(contact); CaptureLab("lab-contact.png");
            float kickContact = DragonCombatChoreography.CycleSeconds(DragonKind.Baby, 0) +
                DragonCombatChoreography.CounterStart(DragonKind.Baby, 1) + FighterCombatTiming.Contact("kick");
            lab.Seek(kickContact); CaptureLab("lab-kick-contact.png");
            float residual = Mathf.Abs(FighterCombatTiming.Travel("punch", FighterCombatTiming.Contact("punch")) -
                FighterCombatTiming.Travel("punch", FighterCombatTiming.Contact("punch") - .02f));
            Require(residual <= 2, "Approach residual slide exceeded 2 px: " + residual);
            Require(FighterCombatTiming.Travel("kick", FighterCombatTiming.Contact("kick")) == 0,
                "Kick still contains fighter root travel.");
            Append($"Animation Test runtime PASS; 2 loops / 16 walk frames captured; travel={integratedTravel:0.000}px; punch/kick root travel={residual:0.000}px");
            SessionState.SetInt(StageKey, 3); EditorApplication.isPlaying = false;
        }

        private static void CaptureLab(string fileName)
        {
            DragonAnimationLabAuthoring.CapturePreview();
            File.Copy("Artifacts/AnimationLab/preview.png", ArtifactRoot + "/" + fileName, true);
        }

        private static void BeginMainScene()
        {
            if (SessionState.GetInt(StageKey, 0) != 3) return;
            EditorSceneManager.OpenScene(MainScenePath); SessionState.SetInt(StageKey, 4); EditorApplication.isPlaying = true;
        }

        private static void RunMainGame()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<DragonMmaGame>();
            Require(game != null && game.DebugSession != null, "Main game did not initialize.");
            Require(game.DebugData.phase == HuntPhase.Walking, "Isolated new game must start in Walking for walk QA.");
            game.DebugView.Refresh(true); game.DebugView.Tick(0);
            DragonMmaUpgradeValidation.Capture(ArtifactRoot + "/main-game-walk-after.png");

            float contact = DragonCombatChoreography.CounterStart(DragonKind.Baby, 0) + FighterCombatTiming.Contact("punch");
            CaptureFightAt(game, contact - .02f, "main-approach-planted.png");
            CaptureFightAt(game, contact, "main-contact.png");
            float kickContact = DragonCombatChoreography.CycleSeconds(DragonKind.Baby, 0) +
                DragonCombatChoreography.CounterStart(DragonKind.Baby, 1) + FighterCombatTiming.Contact("kick");
            CaptureFightAt(game, kickContact, "main-kick-contact.png");
            var planted = DragonCombatChoreography.Evaluate(DragonKind.Baby, contact - .02f, new bool[5]);
            var strike = DragonCombatChoreography.Evaluate(DragonKind.Baby, contact, new bool[5]);
            Require(Mathf.Abs(strike.FighterOffset.x - planted.FighterOffset.x) <= .001f && strike.FighterTravel == 0,
                "Main-game punch still moves the fighter root.");
            var kick = DragonCombatChoreography.Evaluate(DragonKind.Baby, kickContact, new bool[5]);
            Require(kick.FighterTravel == 0 && Mathf.Abs(kick.FighterOffset.x - AnimationMotionQuality.FighterCombatStanceOffset) <= .001f,
                "Main-game kick still moves the fighter root.");
            Append("Main game runtime PASS; bounce-step walk, in-place punch and in-place heavy kick/VFX captured");
            Append(game.GetDebugSummary());
            SessionState.SetInt(StageKey, 6); EditorApplication.isPlaying = false;
        }

        private static void CaptureFightAt(DragonMmaGame game, float elapsed, string fileName)
        {
            game.DebugSession.DebugForceEncounter(DragonKind.Baby, 0);
            game.DebugSession.Tick(elapsed, DragonMmaGame.Now);
            game.DebugView.Refresh(true); game.DebugView.Tick(0);
            Require(game.DebugData.phase == HuntPhase.Fighting, fileName + " left Fighting phase.");
            DragonMmaUpgradeValidation.Capture(ArtifactRoot + "/" + fileName);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Append(string line) => File.AppendAllText(ReportPath, line + Environment.NewLine);

        private static void Fail(Exception exception)
        {
            Debug.LogException(exception); SessionState.SetString(FailureKey, exception.ToString());
            Append("FAIL: " + exception); SessionState.SetInt(StageKey, 90);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false; else Finish(false);
        }

        private static void Finish(bool success)
        {
            EditorApplication.update -= OnUpdate; EditorApplication.playModeStateChanged -= OnPlayMode;
            DragonMmaUpgradeValidation.EndIsolation(); string failure = SessionState.GetString(FailureKey, "");
            string previousScene = SessionState.GetString(PreviousSceneKey, "");
            SessionState.EraseBool(RunningKey); SessionState.EraseInt(StageKey); SessionState.EraseString(FailureKey); SessionState.EraseString(PreviousSceneKey);
            if (success) Debug.Log("[Animation Quality QA] Runtime smoke PASS. " + Path.GetFullPath(ReportPath));
            else Debug.LogError("[Animation Quality QA] Runtime smoke FAILED. " + failure);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else if (!string.IsNullOrEmpty(previousScene) && File.Exists(previousScene)) EditorSceneManager.OpenScene(previousScene);
        }
    }
}
