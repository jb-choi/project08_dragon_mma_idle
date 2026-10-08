using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DragonMMA.EditorTools
{
    public static class BabyFirstBattleQa
    {
        public const string Root = "Artifacts/BabyFirstBattle-20261008";
        private static DragonAnimationLab liveLab;
        private static int warmupFrames, contacts, lastKey, updates;
        private static bool[] threats;
        private static Vector2 fighterAnchor, dragonAnchor, fighterSize, dragonSize;
        private static float anchorDrift, sizeDrift;
        private static double wallStart;
        private static DragonMmaGame Game()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<DragonMmaGame>();
            if (!EditorApplication.isPlaying || game == null || game.DebugSession == null || string.IsNullOrEmpty(DragonSaveSystem.OverridePath))
                throw new InvalidOperationException("Fresh initialized Play with isolated save required.");
            return game;
        }
        private static void Sample(DragonMmaGame game, float time, float roll = 0)
        {
            game.DebugData.cart.Clear(); // Only isolated QA state; keep the first capture scenario reproducible.
            game.DebugSession.DebugForceEncounter(DragonKind.Baby, roll);
            game.DebugSession.Tick(time, DragonMmaGame.Now);
            game.DebugView.Refresh(true); game.DebugView.RefreshLayout(640, 240); game.DebugView.Tick(0);
        }
        public static void CaptureKeyFrames()
        {
            var game = Game(); Directory.CreateDirectory(Root);
            float[] times = { 0f, 1.7f, 1.75f, 5.49f, 5.5f, 5.7f, 6f, 6.3f, 6.95f, 7.8f, 13.7f, 14.5f, 18f, 19.99f, 20.04f, 20.4f, 20.9f };
            foreach (float t in times)
            {
                Sample(game, t + .00001f);
                DragonMmaUpgradeValidation.Capture(Root + "/beat-" + t.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ".png", 640, 240);
            }
            Sample(game, 20.4f, 99);
            DragonMmaUpgradeValidation.Capture(Root + "/defeat-20.4.png", 640, 240);
        }
        public static void CaptureSequence()
        {
            var game = Game(); Directory.CreateDirectory(Root + "/frames");
            for (int n = 0; n < 600; n++)
            {
                Sample(game, n / 30f);
                DragonMmaUpgradeValidation.Capture(Root + "/frames/frame-" + n.ToString("D3") + ".png", 640, 240);
            }
            Directory.CreateDirectory(Root);
            File.WriteAllText(Root + "/sampled-sequence.txt", "600 actual Unity graphics renderer samples, 640x240, 30Hz, 20 seconds. Each frame resets an isolated Baby encounter. This is NOT a realtime screen recording.\n");
        }
        public static void ValidateMainContactsAndOutcomes()
        {
            var game = Game();
            var actors = UnityEngine.Object.FindObjectsByType<DragonActorView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var fighter = actors.First(a => a.name == "Hunter" && a.BasicCombat != null);
            var dragon = actors.First(a => a.name == "Dragon" && a.BasicCombat != null);
            Directory.CreateDirectory(Root);
            string log = "Independent exact contact samples in the actual main view; isolated encounters reset per sample.\n";
            foreach (float t in new[] { 1.7f, 3.8f, 7.8f, 9.2f, 13.7f, 14.5f, 18f })
            {
                Sample(game, t + .00001f);
                float distance = Vector3.Distance(fighter.SpritePixelWorld(fighter.BasicCombat.fighterImpactPixel), dragon.SpritePixelWorld(fighter.BasicCombat.dragonHeadPixels[0]));
                bool pass = fighter.Image.sprite.name == "fighter_jab_3" && dragon.Image.sprite.name == "dragon_0_light_hit_0" && distance < .05f;
                log += $"t={t:F2} gloveToHeadWorldPixels={distance:F6} fighter={fighter.Image.sprite.name} dragon={dragon.Image.sprite.name} {(pass ? "PASS" : "FAIL")}\n";
                if (!pass) { File.WriteAllText(Root + "/main-contacts-and-outcomes.txt", log); throw new InvalidOperationException("Contact mismatch at " + t); }
            }
            foreach (float roll in new[] { 0f, 99f })
            {
                Sample(game, 19.99f, roll);
                var beforeFighter=((RectTransform)fighter.transform).anchoredPosition;
                var beforeDragonSize=dragon.Image.rectTransform.sizeDelta;
                if (game.DebugData.phase != HuntPhase.Fighting) throw new InvalidOperationException("Early result");
                game.DebugSession.Tick(.05f, DragonMmaGame.Now); game.DebugView.Refresh(true); game.DebugView.Tick(0);
                var expected = roll == 0 ? HuntPhase.Result : HuntPhase.Recovery;
                if (game.DebugData.phase != expected) throw new InvalidOperationException("Outcome regression");
                var notification = game.DebugView.GetComponentsInChildren<RectTransform>(true).Single(r => r.name == "Notification");
                float toastRight = notification.TransformPoint(notification.rect.max).x;
                float fighterLeft = fighter.Image.rectTransform.TransformPoint(fighter.Image.rectTransform.rect.min).x;
                if(toastRight >= fighterLeft)throw new InvalidOperationException("Result toast conceals fighter silhouette");
                if(roll==0)
                {
                    if(!dragon.Image.sprite.name.StartsWith("dragon_0_yield_"))throw new InvalidOperationException("Legacy color switch on capture");
                    if(Vector2.Distance(beforeFighter,((RectTransform)fighter.transform).anchoredPosition)>.01f ||
                       Vector2.Distance(beforeDragonSize,dragon.Image.rectTransform.sizeDelta)>.01f)throw new InvalidOperationException("Resolution anchor/size jump");
                }
                else if(!dragon.Image.sprite.name.StartsWith("dragon_0_fight_idle_"))throw new InvalidOperationException("Legacy color switch on loss");
                log += $"roll={roll} phase19.99=Fighting phase20.04={game.DebugData.phase} PASS\n";
            }
            File.WriteAllText(Root + "/main-contacts-and-outcomes.txt", log);
            Debug.Log("[First Battle QA] Seven real main-view contacts + both session outcomes PASS");
        }
        public static void StartLiveThreeBattles()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Graphics Play required");
            liveLab=UnityEngine.Object.FindFirstObjectByType<DragonAnimationLab>();
            if(liveLab==null)throw new InvalidOperationException("AnimationTestScene required");
            liveLab.SetSpecies(DragonKind.Baby);liveLab.SetMode(DragonAnimationLab.PreviewMode.Combat);
            liveLab.SetLoop(true);liveLab.SetSpeed(1);liveLab.Seek(0);
            fighterAnchor=((RectTransform)liveLab.Fighter.transform).anchoredPosition;dragonAnchor=((RectTransform)liveLab.Dragon.transform).anchoredPosition;
            fighterSize=liveLab.Fighter.Image.rectTransform.sizeDelta;dragonSize=liveLab.Dragon.Image.rectTransform.sizeDelta;
            contacts=0;lastKey=-1;updates=0;anchorDrift=0;sizeDrift=0;threats=new bool[6];
            Directory.CreateDirectory(Root);File.WriteAllText(Root+"/live-three-battles.txt","Graphics Play, 1x, three full20s Animation Lab COMBAT/Baby sequences using the same choreography as the main view. Not a main-game state simulation.\n");
            EditorApplication.update-=Warmup;EditorApplication.update-=LiveUpdate;
            warmupFrames=4;EditorApplication.isPaused=false;EditorApplication.update+=Warmup;
        }
        private static void Warmup()
        {
            if(!EditorApplication.isPlaying||liveLab==null){EditorApplication.update-=Warmup;return;}
            if(--warmupFrames>0)return;
            EditorApplication.update-=Warmup;liveLab.Seek(0);wallStart=EditorApplication.timeSinceStartup;
            liveLab.SetPlaying(true);EditorApplication.update+=LiveUpdate;
        }
        private static void LiveUpdate()
        {
            if(!EditorApplication.isPlaying||liveLab==null){EditorApplication.update-=LiveUpdate;return;}
            updates++;float t=liveLab.Elapsed;int round=Mathf.FloorToInt(t/BabyFirstBattle.Duration);
            var f=BabyFirstBattle.Evaluate(Mathf.Repeat(t,BabyFirstBattle.Duration));
            anchorDrift=Mathf.Max(anchorDrift,Vector2.Distance(fighterAnchor,((RectTransform)liveLab.Fighter.transform).anchoredPosition),Vector2.Distance(dragonAnchor,((RectTransform)liveLab.Dragon.transform).anchoredPosition));
            sizeDrift=Mathf.Max(sizeDrift,Vector2.Distance(fighterSize,liveLab.Fighter.Image.rectTransform.sizeDelta),Vector2.Distance(dragonSize,liveLab.Dragon.Image.rectTransform.sizeDelta));
            int key=round*7+f.ContactKey;
            if(round<3&&f.Hit&&key!=lastKey)
            {
                lastKey=key;contacts++;
                File.AppendAllText(Root+"/live-three-battles.txt",$"round={round+1} jab={f.ContactKey+1} t={t:F4} {liveLab.Fighter.Image.sprite.name} / {liveLab.Dragon.Image.sprite.name}\n");
            }
            if(round<3&&f.DragonPose=="threat"&&f.DragonSeconds>=.27f&&f.FighterPose=="first_guard")
                threats[round*2+(Mathf.Repeat(t,20)<10?0:1)]=true;
            if(t<60)return;
            EditorApplication.update-=LiveUpdate;liveLab.SetPlaying(false);
            bool pass=contacts==21&&threats.All(v=>v)&&anchorDrift<.01f&&sizeDrift<.01f;
            File.AppendAllText(Root+"/live-three-battles.txt",$"contacts={contacts}/21 guardThreats={threats.Count(v=>v)}/6 updates={updates} wallSeconds={EditorApplication.timeSinceStartup-wallStart:F2} maxRootDrift={anchorDrift:F6} maxSizeDrift={sizeDrift:F6} {(pass?"PASS":"FAIL")}\n");
            if(pass)Debug.Log("[First Battle QA] Three full live battles PASS");else Debug.LogError("[First Battle QA] Three full live battles FAIL");
        }
    }
}
