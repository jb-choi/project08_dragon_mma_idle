using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DragonMMA.EditorTools
{
    public static class BasicCombatQa
    {
        public const string Root="Artifacts/BasicCombatQA";
        static DragonAnimationLab liveLab;
        static int contacts,frames,lastKey;
        static Vector2 fighterAnchor,dragonAnchor,fighterSize,dragonSize;
        static float maxAnchorDrift,maxSizeDrift;
        static double wallStart;
        static int warmupFrames;
        public static void StartLive()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Live QA needs Play Mode.");
            liveLab=UnityEngine.Object.FindFirstObjectByType<DragonAnimationLab>();
            if(liveLab==null)throw new InvalidOperationException("Open AnimationTestScene first.");
            liveLab.SetSpecies(DragonKind.Baby);liveLab.SetMode(DragonAnimationLab.PreviewMode.BasicJab);liveLab.SetLoop(true);liveLab.SetSpeed(1);liveLab.Seek(0);
            fighterAnchor=((RectTransform)liveLab.Fighter.transform).anchoredPosition;dragonAnchor=((RectTransform)liveLab.Dragon.transform).anchoredPosition;
            fighterSize=liveLab.Fighter.Image.rectTransform.sizeDelta;dragonSize=liveLab.Dragon.Image.rectTransform.sizeDelta;
            contacts=0;frames=0;lastKey=-1;maxAnchorDrift=0;maxSizeDrift=0;wallStart=EditorApplication.timeSinceStartup;
            Directory.CreateDirectory(Root);File.WriteAllText(Root+"/live-10-cycles.txt","Graphics-enabled Unity Play Mode; unscaled live clock, 1x.\n");
            // Resuming a paused Editor can produce one very large unscaledDeltaTime.
            // Let it settle while the lab is stopped, then begin real playback.
            EditorApplication.update-=LiveUpdate;EditorApplication.update-=Warmup;
            EditorApplication.isPaused=false;warmupFrames=4;EditorApplication.update+=Warmup;
        }
        static void Warmup()
        {
            if(!EditorApplication.isPlaying||liveLab==null){EditorApplication.update-=Warmup;return;}
            if(--warmupFrames>0)return;
            EditorApplication.update-=Warmup;liveLab.Seek(0);wallStart=EditorApplication.timeSinceStartup;liveLab.SetPlaying(true);EditorApplication.update+=LiveUpdate;
        }
        static void LiveUpdate()
        {
            if(!EditorApplication.isPlaying||liveLab==null){EditorApplication.update-=LiveUpdate;return;}
            frames++;
            maxAnchorDrift=Mathf.Max(maxAnchorDrift,Vector2.Distance(fighterAnchor,((RectTransform)liveLab.Fighter.transform).anchoredPosition),Vector2.Distance(dragonAnchor,((RectTransform)liveLab.Dragon.transform).anchoredPosition));
            maxSizeDrift=Mathf.Max(maxSizeDrift,Vector2.Distance(fighterSize,liveLab.Fighter.Image.rectTransform.sizeDelta),Vector2.Distance(dragonSize,liveLab.Dragon.Image.rectTransform.sizeDelta));
            var f=DragonCombatChoreography.EvaluateBasicJab(liveLab.Elapsed);
            if(f.Hit&&f.ContactKey!=lastKey&&f.ContactKey<10)
            {
                contacts++;lastKey=f.ContactKey;
                File.AppendAllText(Root+"/live-10-cycles.txt",$"cycle={f.ContactKey+1} time={liveLab.Elapsed:F5} fighter={liveLab.Fighter.Image.sprite.name} dragon={liveLab.Dragon.Image.sprite.name}\n");
            }
            if(liveLab.Elapsed<BasicCombatProfile.CycleSeconds*10) return;
            liveLab.SetPlaying(false);EditorApplication.update-=LiveUpdate;
            File.AppendAllText(Root+"/live-10-cycles.txt",$"contacts={contacts}/10 sampledUpdates={frames} wallSeconds={EditorApplication.timeSinceStartup-wallStart:F2} maxRootDrift={maxAnchorDrift:F6} maxImageSizeDrift={maxSizeDrift:F6}\n");
            bool pass=contacts==10&&maxAnchorDrift<.01f&&maxSizeDrift<.01f;
            File.AppendAllText(Root+"/live-10-cycles.txt","Technical repetition gate="+(pass?"PASS":"FAIL")+"; perceptual quality requires captured-frame/loop review.\n");
            if(pass)Debug.Log("[Basic Combat QA] LIVE 10-cycle technical gate PASS");else Debug.LogError("[Basic Combat QA] LIVE 10-cycle technical gate FAIL");
        }
        public static void CaptureMainSequence()
        {
            var game=UnityEngine.Object.FindFirstObjectByType<DragonMmaGame>();if(game==null||game.DebugSession==null||!EditorApplication.isPlaying)throw new InvalidOperationException("Initialized main overlay Play Mode required; stop and re-enter Play after any script reload.");
            Directory.CreateDirectory(Root+"/frames");
            var actors=UnityEngine.Object.FindObjectsByType<DragonActorView>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var fighter=actors.First(a=>a.BasicCombat!=null&&a.name=="Hunter");var dragon=actors.First(a=>a.BasicCombat!=null&&a.name=="Dragon");
            float impact=BasicCombatProfile.IdleSeconds+BasicCombatProfile.JabContact+.00001f;
            File.WriteAllText(Root+"/exact-contact-10-cycles.txt","Ten independent exact contact samples in main game Play Mode, encounter reset each sample, VFX hidden. Continuous ten-cycle playback is recorded separately in live-10-cycles.txt.\n");
            for(int cycle=0;cycle<10;cycle++)
            {
                Sample(game,impact);
                Vector3 glove=fighter.SpritePixelWorld(fighter.BasicCombat.fighterImpactPixel),head=dragon.SpritePixelWorld(fighter.BasicCombat.dragonHeadPixels[0]);
                float distance=Vector2.Distance(fighter.transform.parent.InverseTransformPoint(glove),fighter.transform.parent.InverseTransformPoint(head));
                bool correct=fighter.Image.sprite.name=="fighter_jab_3"&&dragon.Image.sprite.name=="dragon_0_light_hit_0"&&distance<.05f;
                File.AppendAllText(Root+"/exact-contact-10-cycles.txt",$"cycle={cycle+1} gloveToHeadLocalPixels={distance:F6} fighter={fighter.Image.sprite.name} dragon={dragon.Image.sprite.name} {(correct?"PASS":"FAIL")}\n");
                if(!correct)throw new InvalidOperationException("Actual Animator contact mismatch in cycle "+(cycle+1)+" distance="+distance);
                DragonMmaUpgradeValidation.Capture(Root+"/contact-"+(cycle+1).ToString("D2")+"-640.png",640,240);
            }
            int count=Mathf.CeilToInt(BasicCombatProfile.CycleSeconds*60);
            for(int frame=0;frame<count;frame++) {Sample(game,frame/60f);DragonMmaUpgradeValidation.Capture(Root+"/frames/frame-"+frame.ToString("D3")+".png",640,240);}
            foreach(var phase in new[]{new Vector2(impact,0),new Vector2(impact+.05f,1),new Vector2(impact+.16f,2),new Vector2(impact+.28f,3),new Vector2(BasicCombatProfile.CycleSeconds-.05f,4)})
            {
                Sample(game,phase.x);DragonMmaUpgradeValidation.Capture(Root+"/final-phase-"+(int)phase.y+"-1920.png");
            }
            Debug.Log("[Basic Combat QA] Exact contact 10/10, full 60Hz game sequence captured (not a realtime recording).");
        }
        static void Sample(DragonMmaGame game,float time)
        {
            // Reset only the isolated QA encounter; do not change battle rules or production saves.
            game.DebugSession.DebugForceEncounter(DragonKind.Baby,0);game.DebugSession.Tick(time,DragonMmaGame.Now);game.DebugView.Refresh(true);game.DebugView.RefreshLayout(640,240);game.DebugView.Tick(0);
        }
    }
}
