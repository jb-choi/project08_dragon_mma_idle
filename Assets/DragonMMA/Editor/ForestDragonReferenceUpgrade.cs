using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

namespace DragonMMA.EditorTools
{
    // Narrow, repeatable dragon upgrade. Never runs the global scene/art generator.
    // RGBA pixels are not rewritten: separation is SpriteEditor metadata and tight mesh.
    public static class ForestDragonReferenceUpgrade
    {
        public const string SheetRoot = "Assets/DragonMMA/Resources/DragonMMA/DragonSheets";
        public const string MaterialPath = "Assets/DragonMMA/UIArt/forest_dragon_pixels.mat";
        private static readonly float[] Heights = { 68, 90, 112, 118, 126 };
        private static readonly float[] TrainingHeights = { 68, 90, 112, 136, 156 };
        private static readonly float[] WebBattleHeights = { 60, 72, 78, 86, 90 };
        private sealed class Region { public int Id; public RectInt Bounds; public Vector2[] Hull; }

        [MenuItem("Dragon MMA/Dragons/Import Forest Reference Sheets")]
        public static void Import()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // Validate all five sources before touching existing controllers/prefabs.
            var all = new Region[5][];
            for (int kind = 0; kind < 5; kind++) all[kind] = ReadRegions(SheetRoot + "/dragon_" + kind + ".png");
            var portraits = new Sprite[5]; var previews = new Sprite[5]; var scales = new float[5];
            for (int kind = 0; kind < 5; kind++)
            {
                scales[kind] = Heights[kind] / all[kind][0].Bounds.height;
                ImportSheet(kind, all[kind], scales[kind]);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetRoot + "/dragon_" + kind + ".png").OfType<Sprite>().ToDictionary(s => s.name);
                portraits[kind] = sprites["dragon_" + kind + "_portrait"];
                previews[kind] = sprites["dragon_" + kind + "_idle_0"];
                UpgradeController(kind, sprites);
            }
            var shader = Shader.Find("DragonMMA/Forest Dragon UI");
            if (shader == null) throw new InvalidOperationException("Missing forest dragon UI shader.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
            Undo.RecordObject(material, "Forest dragon pixel material");
            material.shader = shader; material.SetFloat("_AlphaCutoff", .9f); material.SetFloat("_RGBFloor", .05f);
            material.EnableKeyword("UNITY_UI_ALPHACLIP"); EditorUtility.SetDirty(material);
            ConfigureActor("Assets/DragonMMA/Prefabs/Actors/Dragon.prefab", previews, scales, material);
            var trainingScales = Enumerable.Range(0, 5).Select(i => scales[i] * TrainingHeights[i] / Heights[i]).ToArray();
            ConfigureActor("Assets/DragonMMA/Prefabs/Actors/TrainingDragon.prefab", previews, trainingScales, material);
            ConfigureUi("Assets/DragonMMA/Prefabs/UI/OverlayRoot.prefab", portraits, previews, scales, material);
            ConfigureUi("Assets/DragonMMA/Prefabs/UI/WebOverlayRoot.prefab", portraits, previews, scales, material);
            AssetDatabase.SaveAssets();
            Debug.Log("[Forest Dragons] Imported 5 RGBA sheets, 180 animation sprites + 5 portraits, 30 clips. Existing dragon GUIDs preserved; fighter/buttons/rules untouched.");
        }

        private static Region[] ReadRegions(string path)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidDataException(path);
                int width = texture.width, height = texture.height;
                var pixels = texture.GetPixels32(); var labels = new int[pixels.Length];
                var queue = new Queue<int>(); var regions = new List<Region>(); int id = 0;
                for (int start = 0; start < pixels.Length; start++)
                {
                    if (labels[start] != 0 || pixels[start].a < 230) continue;
                    id++; labels[start] = id; queue.Enqueue(start);
                    int count = 0, x0 = width, y0 = height, x1 = 0, y1 = 0;
                    while (queue.Count > 0)
                    {
                        int z = queue.Dequeue(), x = z % width, y = z / width;
                        count++; x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y);
                        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                        {
                            int sx = x + dx, sy = y + dy;
                            if (sx < 0 || sy < 0 || sx >= width || sy >= height) continue;
                            int next = sy * width + sx;
                            if (labels[next] != 0 || pixels[next].a < 230) continue;
                            labels[next] = id; queue.Enqueue(next);
                        }
                    }
                    if (count > 1500) regions.Add(new Region { Id = id, Bounds = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1) });
                }
                if (regions.Count != 36) throw new InvalidDataException(path + ": expected 36 full-body regions; found " + regions.Count);
                regions.Sort((a, b) => b.Bounds.center.y.CompareTo(a.Bounds.center.y));
                for (int row = 0; row < 6; row++) regions.Sort(row * 6, 6, Comparer<Region>.Create((a, b) => a.Bounds.xMin.CompareTo(b.Bounds.xMin)));
                foreach (var region in regions)
                {
                    // Convex boundary of this connected body only. It excludes adjacent
                    // silhouettes in overlapping bounding-box corners without changing pixels.
                    var points = new List<Vector2>();
                    var b = region.Bounds;
                    for (int y = b.yMin; y < b.yMax; y++)
                    {
                        int left = b.xMax, right = b.xMin - 1;
                        for (int x = b.xMin; x < b.xMax; x++)
                            if (labels[y * width + x] == region.Id) { left = Math.Min(left, x); right = Math.Max(right, x); }
                        if (right < left) continue;
                        points.Add(new Vector2(left, y)); points.Add(new Vector2(left, y + 1));
                        points.Add(new Vector2(right + 1, y)); points.Add(new Vector2(right + 1, y + 1));
                    }
                    region.Hull = ConvexHull(points);
                }
                Debug.Log("[Forest Dragons] " + path + ": " + width + "x" + height + ", 36 regions.");
                return regions.ToArray();
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        private static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        private static Vector2[] ConvexHull(List<Vector2> points)
        {
            points = points.Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToList();
            var hull = new List<Vector2>();
            foreach (var p in points) { while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
            int lower = hull.Count;
            for (int i = points.Count - 2; i >= 0; i--) { var p = points[i]; while (hull.Count > lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
            hull.RemoveAt(hull.Count - 1);
            return hull.ToArray();
        }
        private static void ImportSheet(int kind, Region[] regions, float scale)
        {
            string path = SheetRoot + "/dragon_" + kind + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Undo.RecordObject(importer, "Slice forest dragon sheet");
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.isReadable = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None; importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048; importer.spritePixelsPerUnit = 32;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.Tight; importer.SetTextureSettings(settings);
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var old = provider.GetSpriteRects().ToDictionary(r => r.name);
            var rects = new List<SpriteRect>();
            for (int n = 0; n < 37; n++)
            {
                bool portrait = n == 36; var region = regions[portrait ? 0 : n]; var b = region.Bounds;
                string name = "dragon_" + kind + "_" + (portrait ? "portrait" : DragonAnimationTiming.Poses[n / 6] + "_" + n % 6);
                // Exact component bounds; custom tight mesh removes foreign corner pixels.
                var rect = new Rect(b.xMin, b.yMin, b.width, b.height);
                var pivot = portrait ? new Vector2(.5f, .5f) : new Vector2(Mathf.Clamp01(DragonAnimationTiming.FrontAnchorPixels / scale / b.width), 0);
                rects.Add(new SpriteRect { name = name, rect = rect, pivot = pivot, alignment = SpriteAlignment.Custom,
                    spriteID = old.TryGetValue(name, out var existing) ? existing.spriteID : GUID.Generate() });
            }
            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            var outlines = provider.GetDataProvider<ISpriteOutlineDataProvider>();
            for (int n = 0; n < rects.Count; n++)
            {
                var rect = rects[n]; var hull = regions[n == 36 ? 0 : n].Hull.Select(p => p - rect.rect.center).ToArray();
                outlines.SetOutlines(rect.spriteID, new List<Vector2[]> { hull });
                outlines.SetTessellationDetail(rect.spriteID, 0);
            }
            provider.Apply(); importer.SaveAndReimport();
        }
        private static void UpgradeController(int kind, Dictionary<string, Sprite> sprites)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(DragonAnimationAuthoring.DragonControllerPath(kind));
            if (controller == null) throw new InvalidOperationException("Missing authored dragon controller " + kind);
            Undo.RecordObject(controller, "Upgrade forest dragon controller");
            var machine = controller.layers[0].stateMachine; Undo.RecordObject(machine, "Forest dragon states");
            foreach (string pose in DragonAnimationTiming.Poses)
            {
                string name = "dragon_" + kind + "_" + pose, path = DragonAnimationAuthoring.DirectoryPath + "/" + name + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path); }
                Undo.RecordObject(clip, "Forest dragon sprite clip"); clip.frameRate = 60;
                var keys = new ObjectReferenceKeyframe[DragonAnimationTiming.KeyCount(pose)];
                for (int frame = 0; frame < keys.Length; frame++) keys[frame] = new ObjectReferenceKeyframe {
                    time = DragonAnimationTiming.FrameTime((DragonKind)kind, pose, frame), value = sprites[name + "_" + DragonAnimationTiming.SpriteFrame(pose, frame)] };
                AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(Image), "m_Sprite"), keys);
                var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = DragonAnimationTiming.Loops(pose);
                AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == pose);
                if (state == null) state = machine.AddState(pose);
                Undo.RecordObject(state, "Assign forest dragon clip"); state.motion = clip; state.writeDefaultValues = false;
                EditorUtility.SetDirty(state); if (pose == "idle") machine.defaultState = state;
            }
            EditorUtility.SetDirty(machine); EditorUtility.SetDirty(controller);
        }
        private static void ConfigureActor(string path, Sprite[] previews, float[] scales, Material material)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var actor = root.GetComponent<DragonActorView>();
                Undo.RecordObject(actor, "Forest dragon visuals"); Undo.RecordObject(actor.Image, "Forest dragon image");
                Undo.RecordObject(actor.Image.rectTransform, "Forest dragon ground registration");
                actor.ConfigureDragonAppearanceEditor(previews, scales, material);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void ConfigureUi(string path, Sprite[] portraits, Sprite[] previews, float[] scales, Material material)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<DragonMmaView>(); Undo.RecordObject(view, "Forest dragon portraits");
                var so = new SerializedObject(view); var array = so.FindProperty("dragonPortraits"); array.arraySize = 5;
                for (int i = 0; i < 5; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = portraits[i];
                so.ApplyModifiedProperties();
                if (path.EndsWith("/WebOverlayRoot.prefab"))
                {
                    var actor = (DragonActorView)so.FindProperty("dragon").objectReferenceValue;
                    var webScales = Enumerable.Range(0, 5).Select(i => scales[i] * WebBattleHeights[i] / Heights[i]).ToArray();
                    Undo.RecordObject(actor, "Web battle dragon scale"); Undo.RecordObject(actor.Image, "Web battle dragon image");
                    Undo.RecordObject(actor.Image.rectTransform, "Web battle dragon geometry");
                    actor.ConfigureDragonAppearanceEditor(previews, webScales, material);
                    var fighter = (DragonActorView)so.FindProperty("hunter").objectReferenceValue;
                    Undo.RecordObject(fighter.Image.rectTransform, "Web fighter victory clearance");
                    fighter.Image.rectTransform.sizeDelta = new Vector2(192, 128) * .85f;
                    // Actor import owns sprite geometry only. The combat-directing
                    // authoring pass owns nameplate placement, so repeated character
                    // imports cannot silently undo the approved battle layout.
                }
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    if (image.GetComponentInParent<DragonActorView>() != null || image.sprite == null || !image.sprite.name.StartsWith("dragon_")) continue;
                    string[] name = image.sprite.name.Split('_');
                    if (name.Length < 2 || !int.TryParse(name[1], out int kind) || kind < 0 || kind > 4) continue;
                    Undo.RecordObject(image, "Forest dragon portrait image");
                    image.sprite = portraits[kind]; image.material = material; image.useSpriteMesh = true;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
