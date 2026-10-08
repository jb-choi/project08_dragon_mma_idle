using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

namespace DragonMMA.EditorTools
{
    // Import registration only: silhouettes are authored by the source artist, not procedurally drawn.
    public static class BasicCombatSpriteAuthoring
    {
        const string Sources = "Assets/DragonMMA/ArtSource/BasicCombat/";
        const string Art = "Assets/Resources/DragonMMA/Art/";
        const string Dragons = "Assets/Resources/DragonMMA/DragonSheets/";
        const string ProfilePath = "Assets/DragonMMA/Configuration/BabyJabContact.asset";
        sealed class Figure { public int x0, y0, x1, y1; public float CenterX => (x0+x1)*.5f; public float CenterY => (y0+y1)*.5f; }
        [MenuItem("Dragon MMA/Basic Combat/Import Registered Sprites")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Stop Play and save scene before import.");
            Directory.CreateDirectory(Art); Directory.CreateDirectory(Dragons); Directory.CreateDirectory("Assets/DragonMMA/Configuration");
            var fighterIdle = Frames(Sources+"fighter-idle.png", 16, 0, 8, true);
            // The second generated row compresses the head height by four pixels.
            // Use the grounded first-row weight transfers in a reversible eight-key loop.
            fighterIdle = new[]{fighterIdle[0],fighterIdle[1],fighterIdle[2],fighterIdle[3],fighterIdle[2],fighterIdle[1],fighterIdle[0],fighterIdle[0]};
            var jab = Frames(Sources+"fighter-jab.png", 8, 0, 8, true);
            // Choose artist-drawn in-betweens in monotonically retracting glove order.
            var halfway = jab[5]; var near = jab[1]; jab[4] = halfway; jab[5] = near;
            var calm = Frames(Sources+"dragon-idle-calm.png",4,0,4,false);
            var dragonIdle = new[]{calm[0],calm[1],calm[2],calm[3],calm[2],calm[1],calm[0],calm[0]};
            var hit = Frames(Sources+"dragon-light-hit.png",8,0,8,false);
            // Reject the two exaggerated chin-up drawings: a jab is a compact
            // backward head/neck flinch, while the heavy clip owns the big twist.
            hit = new[]{dragonIdle[0],hit[1],hit[5],hit[4],hit[6],hit[6],dragonIdle[0],dragonIdle[0]};
            var heavy = Frames(Sources+"dragon-heavy-threat.png",16,0,8,false);
            var threat = Frames(Sources+"dragon-heavy-threat.png",16,8,8,false);
            // Identical boundary drawings eliminate generated identity/foot registration jumps.
            jab[0] = fighterIdle[0]; jab[7] = fighterIdle[0];
            // Contact uses the neutral face boundary, not the already-recoiled head.
            // Otherwise the glove penetrates the neutral muzzle before impact.
            hit[0] = dragonIdle[0]; hit[7] = dragonIdle[0];
            heavy[0] = dragonIdle[0]; heavy[7] = dragonIdle[0];
            threat[0] = dragonIdle[0]; threat[7] = dragonIdle[0];
            Write(Art+"fighter_fight_idle_sheet.png","fighter_fight_idle",fighterIdle,192,128,new Vector2(96,8));
            Write(Art+"fighter_jab_sheet.png","fighter_jab",jab,192,128,new Vector2(96,8));
            Write(Dragons+"dragon_0_fight_idle.png","dragon_0_fight_idle",dragonIdle,256,192,new Vector2(52,8));
            Write(Dragons+"dragon_0_light_hit.png","dragon_0_light_hit",hit,256,192,new Vector2(52,8));
            Write(Dragons+"dragon_0_heavy_hit.png","dragon_0_heavy_hit",heavy,256,192,new Vector2(52,8));
            Write(Dragons+"dragon_0_threat.png","dragon_0_threat",threat,256,192,new Vector2(52,8));
            Clip("fighter","fight_idle",fighterIdle.Length,Uniform(8,.75f,true),true,DragonAnimationAuthoring.HunterControllerPath);
            Clip("fighter","jab",jab.Length,BasicCombatProfile.JabFrameTimes,false,DragonAnimationAuthoring.HunterControllerPath);
            Clip("dragon_0","fight_idle",8,Uniform(8,1.5f,true),true,DragonAnimationAuthoring.DragonControllerPath(0));
            Clip("dragon_0","light_hit",8,BasicCombatProfile.LightHitFrameTimes,false,DragonAnimationAuthoring.DragonControllerPath(0));
            Clip("dragon_0","heavy_hit",8,new float[]{0,.035f,.10f,.18f,.29f,.40f,.52f,.64f},false,DragonAnimationAuthoring.DragonControllerPath(0));
            Clip("dragon_0","threat",8,new float[]{0,.06f,.14f,.21f,.27f,.34f,.42f,.50f},false,DragonAnimationAuthoring.DragonControllerPath(0));
            var profile = AssetDatabase.LoadAssetAtPath<BasicCombatProfile>(ProfilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<BasicCombatProfile>(); AssetDatabase.CreateAsset(profile,ProfilePath); }
            Undo.RecordObject(profile,"Calibrate baby jab contact");
            profile.fighterImpactPixel = RightGlove(jab[3]);
            profile.dragonHeadPixels = Enumerable.Range(0,8).Select(i=>Muzzle(hit[i])).ToArray();
            EditorUtility.SetDirty(profile);
            foreach(var path in new[]{"Assets/DragonMMA/Prefabs/Actors/Hunter.prefab","Assets/DragonMMA/Prefabs/Actors/Dragon.prefab"})
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try { var actor = root.GetComponent<DragonActorView>(); Undo.RecordObject(actor,"Basic contact profile"); var so=new SerializedObject(actor); so.FindProperty("basicCombatProfile").objectReferenceValue=profile; so.ApplyModifiedProperties(); PrefabUtility.SaveAsPrefabAsset(root,path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var previous = EditorSceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene(DragonAnimationLabAuthoring.ScenePath);
            var lab = UnityEngine.Object.FindFirstObjectByType<DragonAnimationLab>();
            var labData = new SerializedObject(lab); var buttons=labData.FindProperty("modeButtons");
            if(buttons.arraySize == 4)
            {
                var original=(Button)buttons.GetArrayElementAtIndex(3).objectReferenceValue;
                var added=UnityEngine.Object.Instantiate(original,original.transform.parent); Undo.RegisterCreatedObjectUndo(added.gameObject,"Basic jab preview button");
                added.name="BasicJabMode"; var rt=(RectTransform)added.transform; rt.anchoredPosition += new Vector2(0,-45);
                added.GetComponentInChildren<Text>().text="BASIC JAB";
                buttons.arraySize=5;buttons.GetArrayElementAtIndex(4).objectReferenceValue=added;
            }
            labData.FindProperty("mode").enumValueIndex=4;labData.ApplyModifiedProperties();
            var stage = (RectTransform)lab.Fighter.transform.parent; Undo.RecordObject(stage,"Fit complete combat pair in preview");
            stage.anchoredPosition = new Vector2(280,stage.anchoredPosition.y);
            var dragonAnchor = (RectTransform)lab.Dragon.transform; Undo.RecordObject(dragonAnchor,"Shared combat floor");
            dragonAnchor.anchoredPosition = new Vector2(dragonAnchor.anchoredPosition.x,0);
            for(int i=0;i<buttons.arraySize;i++)
            {
                var rt=(RectTransform)((Button)buttons.GetArrayElementAtIndex(i).objectReferenceValue).transform;
                Undo.RecordObject(rt,"Fit five mode buttons");rt.anchoredPosition=new Vector2(40+i*192,756);rt.sizeDelta=new Vector2(180,40);
            }
            AddPreviewButtons(labData,"fighterButtons",FighterCombatTiming.Poses.Length,new[]{"fight_idle","jab"},lab.transform,0);
            AddPreviewButtons(labData,"dragonButtons",DragonAnimationTiming.Poses.Length,new[]{"fight_idle","light_hit","heavy_hit","threat"},lab.transform,2);
            labData.ApplyModifiedProperties();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            if(!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Basic Combat Import] Glove={profile.fighterImpactPixel}, neutral muzzle={profile.dragonHeadPixels[0]}. Six separate atlases, fixed ground registration.");
        }
        [MenuItem("Dragon MMA/Basic Combat/Import First Battle Continuity")]
        public static void ApplyFirstBattleContinuity()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play; preserve any dirty scene before import.");
            var guard = Frames(Sources+"fighter-first-guard.png",8,0,8,true);
            var yield = Frames(Sources+"dragon-first-yield.png",8,0,8,false);
            // Copy approved boundary pixels without reimporting/rebuilding the six approved atlases.
            guard[0] = RegisteredFrame(Art+"fighter_fight_idle_sheet.png",192,128);
            guard[7] = guard[0];
            yield[0] = RegisteredFrame(Dragons+"dragon_0_fight_idle.png",256,192);
            foreach (var pixels in guard.Concat(yield))
                for (int i=0;i<pixels.Length;i++)
                    if(pixels[i].a>0 && pixels[i].r==0 && pixels[i].g==0 && pixels[i].b==0)
                        pixels[i]=new Color32(8,12,14,255); // Native window black transparency key.
            Write(Art+"fighter_first_guard_sheet.png","fighter_first_guard",guard,192,128,new Vector2(96,8));
            Write(Dragons+"dragon_0_yield.png","dragon_0_yield",yield,256,192,new Vector2(52,8));
            Clip("fighter","first_guard",8,new float[]{0,.05f,.10f,.15f,.20f,.28f,.35f,.45f},false,DragonAnimationAuthoring.HunterControllerPath);
            Clip("dragon_0","yield",8,new float[]{0,.10f,.20f,.32f,.45f,.60f,.75f,.90f},false,DragonAnimationAuthoring.DragonControllerPath(0));
            var babyPortrait=AssetDatabase.LoadAllAssetsAtPath(Dragons+"dragon_0_fight_idle.png").OfType<Sprite>().Single(s=>s.name=="dragon_0_fight_idle_0");
            foreach(string path in new[]{"Assets/DragonMMA/Prefabs/UI/OverlayRoot.prefab","Assets/DragonMMA/Prefabs/UI/WebOverlayRoot.prefab"})
            {
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view=root.GetComponent<DragonMmaView>();Undo.RecordObject(view,"Match first baby capture portrait");
                    var data=new SerializedObject(view);data.FindProperty("dragonPortraits").GetArrayElementAtIndex(0).objectReferenceValue=babyPortrait;
                    data.ApplyModifiedProperties();PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[First Battle] Added registered guard/yield only; approved Jab/LightHit atlases preserved.");
        }
        static Color32[] RegisteredFrame(string path,int width,int height)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                texture.LoadImage(File.ReadAllBytes(path));var source=texture.GetPixels32();var output=new Color32[width*height];
                for(int y=0;y<height;y++)Array.Copy(source,y*texture.width,output,y*width,width);
                return output;
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        static void AddPreviewButtons(SerializedObject lab,string field,int originalCount,string[] poses,Transform parent,int slotStart)
        {
            var array=lab.FindProperty(field);var template=(Button)array.GetArrayElementAtIndex(0).objectReferenceValue;
            if(array.arraySize==originalCount)
            {
                array.arraySize+=poses.Length;
                // SerializedObject extends arrays by duplicating the last reference.
                // New slots must be cleared before creating independent controls.
                for(int i=0;i<poses.Length;i++)array.GetArrayElementAtIndex(originalCount+i).objectReferenceValue=null;
            }
            for(int i=0;i<poses.Length;i++)
            {
                var button=(Button)array.GetArrayElementAtIndex(originalCount+i).objectReferenceValue;
                if(button==null||button==template){button=UnityEngine.Object.Instantiate(template,parent);Undo.RegisterCreatedObjectUndo(button.gameObject,"Separate combat preview");}
                button.name="Basic "+field+" "+poses[i];button.GetComponentInChildren<Text>().text=poses[i];
                var rt=(RectTransform)button.transform;int slot=slotStart+i;rt.anchoredPosition=new Vector2(60+slot%2*160,530-slot/2*35);rt.sizeDelta=new Vector2(150,30);
                array.GetArrayElementAtIndex(originalCount+i).objectReferenceValue=button;
            }
        }
        static float[] Uniform(int frames,float duration,bool loop) => Enumerable.Range(0,frames+(loop?1:0)).Select(i=>i*duration/frames).ToArray();
        static Color32[][] Frames(string path,int expected,int start,int count,bool fighter)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false); texture.LoadImage(File.ReadAllBytes(path));
            try
            {
                var source=texture.GetPixels32();var figures=Segment(source,texture.width,texture.height);
                if(figures.Count!=expected) throw new InvalidOperationException(path+" silhouettes="+figures.Count+" expected="+expected);
                var sorted=new List<Figure>();
                var vertical=figures.OrderByDescending(f=>f.CenterY).ToArray();
                for(int row=0;row<expected;row+=4)sorted.AddRange(vertical.Skip(row).Take(4).OrderBy(f=>f.CenterX));
                var reference=sorted[start];float scale=(fighter?110f:136f)/(reference.y1-reference.y0+1);
                int w=fighter?192:256,h=fighter?128:192;var output=new Color32[count][];
                for(int n=0;n<count;n++)
                {
                    var f=sorted[start+n]; int foot=fighter?f.x1:f.x0;
                    for(int y=f.y0;y<=f.y0+Mathf.CeilToInt((f.y1-f.y0)*.045f);y++)
                        for(int x=f.x0;x<=f.x1;x++) if(source[y*texture.width+x].a>180) foot=fighter?Math.Min(foot,x):Math.Max(foot,x);
                    var pixels=new Color32[w*h];float anchor=fighter?40:205;
                    for(int y=0;y<h;y++) for(int x=0;x<w;x++)
                    {
                        int sx=Mathf.RoundToInt(foot+(x-anchor)/scale),sy=Mathf.RoundToInt(f.y0+(y-8)/scale);
                        if(sx<f.x0||sx>f.x1||sy<f.y0||sy>f.y1)continue;
                        var c=source[sy*texture.width+sx]; if(c.a<180)continue;c.a=255;pixels[y*w+x]=c;
                    }
                    output[n]=pixels;
                }
                return output;
            } finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        static List<Figure> Segment(Color32[] pixels,int w,int h)
        {
            var seen=new bool[pixels.Length];var output=new List<Figure>();var queue=new Queue<int>();
            for(int seed=0;seed<pixels.Length;seed++)
            {
                if(seen[seed]||pixels[seed].a<180)continue;
                var f=new Figure{x0=w,y0=h,x1=0,y1=0};int size=0;seen[seed]=true;queue.Enqueue(seed);
                while(queue.Count>0)
                {
                    int p=queue.Dequeue(),x=p%w,y=p/w;size++;f.x0=Math.Min(f.x0,x);f.x1=Math.Max(f.x1,x);f.y0=Math.Min(f.y0,y);f.y1=Math.Max(f.y1,y);
                    foreach(int q in new[]{x>0?p-1:-1,x<w-1?p+1:-1,y>0?p-w:-1,y<h-1?p+w:-1}) if(q>=0&&!seen[q]&&pixels[q].a>=180){seen[q]=true;queue.Enqueue(q);}
                }
                if(size>1000)output.Add(f);
            }
            return output;
        }
        static Vector2 RightGlove(Color32[] pixels)
        {
            int edge=0;for(int y=45;y<110;y++)for(int x=0;x<192;x++)if(pixels[y*192+x].a>0)edge=Math.Max(edge,x);
            int total=0,sum=0;for(int y=45;y<110;y++)for(int x=edge-2;x<=edge;x++)if(pixels[y*192+x].a>0){total++;sum+=y;}
            return new Vector2(edge+.5f,(float)sum/Math.Max(1,total));
        }
        static Vector2 Muzzle(Color32[] pixels)
        {
            int edge=256;for(int y=55;y<88;y++)for(int x=0;x<160;x++)if(pixels[y*256+x].a>0)edge=Math.Min(edge,x);
            int sum=0,total=0;for(int y=55;y<88;y++)for(int x=edge;x<edge+3;x++)if(pixels[y*256+x].a>0){sum+=y;total++;}
            return new Vector2(edge+.5f,(float)sum/Math.Max(1,total));
        }
        static void Write(string path,string name,Color32[][] frames,int w,int h,Vector2 pivot)
        {
            var texture=new Texture2D(w*frames.Length,h,TextureFormat.RGBA32,false);var pixels=new Color32[texture.width*h];
            for(int f=0;f<frames.Length;f++)for(int y=0;y<h;y++)Array.Copy(frames[f],y*w,pixels,y*texture.width+f*w,w);
            texture.SetPixels32(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritePixelsPerUnit=1;
            importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.maxTextureSize=4096;
            var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var old=provider.GetSpriteRects().ToDictionary(r=>r.name,r=>r.spriteID);
            var rects=Enumerable.Range(0,frames.Length).Select(i=>new SpriteRect{name=name+"_"+i,rect=new Rect(i*w,0,w,h),alignment=SpriteAlignment.Custom,pivot=new Vector2(pivot.x/w,pivot.y/h),spriteID=old.TryGetValue(name+"_"+i,out var id)?id:GUID.Generate()}).ToArray();
            provider.SetSpriteRects(rects);provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r=>new SpriteNameFileIdPair(r.name,r.spriteID)));
            var outlines=provider.GetDataProvider<ISpriteOutlineDataProvider>();foreach(var r in rects)outlines.SetOutlines(r.spriteID,new List<Vector2[]>{new[]{new Vector2(-w/2f,-h/2f),new Vector2(-w/2f,h/2f),new Vector2(w/2f,h/2f),new Vector2(w/2f,-h/2f)}});
            provider.Apply();importer.SaveAndReimport();
        }
        static void Clip(string prefix,string pose,int frames,float[] times,bool loop,string controllerPath)
        {
            string name=prefix+"_"+pose,path=DragonAnimationAuthoring.DirectoryPath+"/"+name+".anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(clip==null){clip=new AnimationClip{name=name};AssetDatabase.CreateAsset(clip,path);}Undo.RecordObject(clip,"Basic combat clip");
            var sprites=AssetDatabase.LoadAllAssetsAtPath(prefix=="fighter"?Art+name+"_sheet.png":Dragons+name+".png").OfType<Sprite>().ToDictionary(s=>s.name);
            clip.frameRate=60;var keys=times.Select((t,i)=>new ObjectReferenceKeyframe{time=t,value=sprites.TryGetValue(name+"_"+(i%frames),out var s)?s:null}).ToArray();
            if(keys.Any(k=>k.value==null))throw new InvalidOperationException("Missing imported sprites: "+name);
            AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(Image),"m_Sprite"),keys);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);EditorUtility.SetDirty(clip);
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);Undo.RecordObject(controller,"Add separate combat state");
            var machine=controller.layers[0].stateMachine;var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==pose)??machine.AddState(pose);
            Undo.RecordObject(state,"Assign separate combat state");state.motion=clip;state.writeDefaultValues=false;EditorUtility.SetDirty(state);EditorUtility.SetDirty(machine);EditorUtility.SetDirty(controller);
        }
    }
}
