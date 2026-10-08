using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DragonMMA.EditorTools
{
    // Fighter-only extraction: never calls the legacy global art/scene generators.
    public static class FighterReferenceUpgrade
    {
        public const string ArtRoot = "Assets/DragonMMA/Resources/DragonMMA/Art";
        public const string SourceRoot = "Assets/DragonMMA/ArtSource";
        public const int CanvasSize = 128, CanvasWidth = 192, FloorPixel = 8;
        private static readonly string[][] Rows = {
            new[] { "idle", "walk", "punch", "cross", "kick", "hurt" },
            new[] { "block", "clinch", "takedown", "knockdown", "recovery", "victory" }
        };

        [MenuItem("Dragon MMA/Fighter/Import Reference Atlases")]
        public static void Import()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before upgrading fighter assets.");
            foreach (string file in new[] { "fighter_primary_atlas.png", "fighter_secondary_atlas.png" })
                if (!File.Exists(SourceRoot + "/" + file)) throw new FileNotFoundException("Missing generated atlas", file);
            var primary = Read(SourceRoot + "/fighter_primary_atlas.png");
            // A single scale across every pose: do not stretch each character to its cell.
            var primaryRegions = FindRegions(primary, out var primaryLabels);
            int standingHeight = primaryRegions[0].bounds.height;
            float scale = 110f / standingHeight;
            var secondary = Read(SourceRoot + "/fighter_secondary_atlas.png");
            try
            {
                Extract(primary, Rows[0], scale, primaryRegions, primaryLabels);
                var secondaryRegions = FindRegions(secondary, out var secondaryLabels);
                Extract(secondary, Rows[1], scale, secondaryRegions, secondaryLabels);
            }
            finally { UnityEngine.Object.DestroyImmediate(primary); UnityEngine.Object.DestroyImmediate(secondary); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string pose in FighterCombatTiming.Poses)
                for (int frame = 0; frame < 8; frame++) ImportSprite(ArtRoot + "/fighter_" + pose + "_" + frame + ".png");
            WriteContactSheet();
            ArtSpriteSheetMigration.Migrate();
            DragonAnimationAuthoring.UpgradeHunterAssets();
            ConfigureActor("Assets/DragonMMA/Prefabs/Actors/Hunter.prefab");
            ConfigureActor("Assets/DragonMMA/Prefabs/Actors/CartHelper.prefab");
            WriteSound("jab", .065f, 170, .5f);
            WriteSound("heavy", .105f, 85, .7f);
            WriteSound("guard", .06f, 620, .3f);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureAudio("Assets/DragonMMA/Prefabs/UI/OverlayRoot.prefab");
            ConfigureAudio("Assets/DragonMMA/Prefabs/UI/WebOverlayRoot.prefab");
            AssetDatabase.SaveAssets();
            Debug.Log("[Fighter Upgrade] 96 reference-based frames packed into fighter_sheet, 12 fighter clips, foot pivot and contact audio imported. Dragon assets untouched.");
        }
        private static Texture2D Read(string path)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!t.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.DestroyImmediate(t); throw new InvalidDataException(path); }
            return t;
        }
        private struct Region
        {
            public int id;
            public RectInt bounds;
        }
        private static Region[] FindRegions(Texture2D atlas, out int[] labels)
        {
            var pixels = atlas.GetPixels32(); labels = new int[pixels.Length];
            var regions = new List<Region>(); var queue = new Queue<int>(); int id = 0;
            for (int index = 0; index < pixels.Length; index++)
            {
                if (labels[index] != 0 || pixels[index].a < 200) continue;
                id++; labels[index] = id; queue.Enqueue(index);
                int count = 0, x0 = atlas.width, x1 = 0, y0 = atlas.height, y1 = 0;
                while (queue.Count > 0)
                {
                    int z = queue.Dequeue(), x = z % atlas.width, y = z / atlas.width;
                    count++; x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int sx = x + dx, sy = y + dy;
                        if (sx < 0 || sx >= atlas.width || sy < 0 || sy >= atlas.height) continue;
                        int next = sy * atlas.width + sx;
                        if (labels[next] != 0 || pixels[next].a < 200) continue;
                        labels[next] = id; queue.Enqueue(next);
                    }
                }
                if (count > 1500) regions.Add(new Region { id = id, bounds = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1) });
            }
            if (regions.Count != 48) throw new InvalidDataException("Expected 48 separate fullbody sprites; found " + regions.Count);
            // Generated sheets have imperfect cell padding. Order real connected silhouettes, not guessed tiles.
            regions.Sort((a, b) => b.bounds.yMax.CompareTo(a.bounds.yMax));
            for (int row = 0; row < 6; row++) regions.Sort(row * 8, 8, Comparer<Region>.Create((a, b) => a.bounds.xMin.CompareTo(b.bounds.xMin)));
            return regions.ToArray();
        }
        private static void Extract(Texture2D atlas, string[] rows, float scale, Region[] regions, int[] labels)
        {
            var colors = atlas.GetPixels32();
            for (int row = 0; row < 6; row++)
                for (int frame = 0; frame < 8; frame++)
                {
                    Region region = regions[row * 8 + frame]; RectInt bounds = region.bounds;
                    int bottom = bounds.yMin, footMin = bounds.xMax, footMax = bounds.xMin;
                    int footBand = Mathf.Min(bounds.height, Mathf.CeilToInt(12 / scale));
                    for (int y = bottom; y < bottom + footBand; y++) for (int x = bounds.xMin; x < bounds.xMax; x++)
                        if (labels[y * atlas.width + x] == region.id) { footMin = Math.Min(footMin, x); footMax = Math.Max(footMax, x); }
                    float anchorX = (footMin + footMax) * .5f;
                    if (rows[row] == "knockdown" || rows[row] == "recovery") anchorX = bounds.center.x;
                    var output = new Color32[CanvasWidth * CanvasSize];
                    for (int y = 0; y < CanvasSize; y++)
                        for (int x = 0; x < CanvasWidth; x++)
                        {
                            int sx = Mathf.FloorToInt(anchorX + (x - CanvasWidth / 2f) / scale);
                            int sy = bottom + Mathf.FloorToInt((y - FloorPixel) / scale);
                            if (!bounds.Contains(new Vector2Int(sx, sy)) || labels[sy * atlas.width + sx] != region.id) continue;
                            Color32 c = colors[sy * atlas.width + sx];
                            if (c.a < 200) continue;
                            c.a = 255;
                            // Exact opaque black punches holes in the native color-key window.
                            if (c.r < 10 && c.g < 10 && c.b < 10) c = new Color32(18, 17, 20, 255);
                            output[y * CanvasWidth + x] = c;
                        }
                    var t = new Texture2D(CanvasWidth, CanvasSize, TextureFormat.RGBA32, false);
                    t.SetPixels32(output); t.Apply();
                    File.WriteAllBytes(ArtRoot + "/fighter_" + rows[row] + "_" + frame + ".png", t.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(t);
                }
        }
        private static void ImportSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp; importer.npotScale = TextureImporterNPOTScale.None;
            importer.spritePixelsPerUnit = 32;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(.5f, FloorPixel / (float)CanvasSize);
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
        private static void ConfigureActor(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var actor = root.GetComponent<DragonActorView>(); var visual = actor.Image.rectTransform;
                Undo.RecordObject(visual, "Align fighter foot pivot");
                visual.anchorMin = visual.anchorMax = new Vector2(.5f, 0);
                visual.pivot = new Vector2(.5f, FloorPixel / (float)CanvasSize);
                visual.anchoredPosition = Vector2.zero; visual.sizeDelta = new Vector2(CanvasWidth, CanvasSize);
                actor.Image.preserveAspect = true;
                actor.Image.sprite = DragonArtCatalog.Load("fighter_idle_0");
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void WriteSound(string name, float duration, float frequency, float noiseMix)
        {
            Directory.CreateDirectory("Assets/DragonMMA/Audio");
            string path = "Assets/DragonMMA/Audio/fighter_" + name + ".wav";
            const int rate = 44100; int count = (int)(rate * duration); var random = new System.Random(731);
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                for (int i = 0; i < count; i++)
                {
                    float p = i / (float)count, seconds = i / (float)rate;
                    double envelope = Math.Min(1, seconds / .002) * Math.Pow(1 - p, 3);
                    double noise = random.NextDouble() * 2 - 1;
                    double tone = Math.Sin(2 * Math.PI * frequency * seconds * (1 - .35f * p));
                    writer.Write((short)(Math.Clamp((tone * (1 - noiseMix) + noise * noiseMix) * envelope * .75, -1, 1) * short.MaxValue));
                }
            }
        }
        private static void ConfigureAudio(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<DragonMmaView>();
                var source = root.GetComponent<AudioSource>();
                if (source == null) { source = root.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0; source.volume = .20f; }
                var so = new SerializedObject(view);
                so.FindProperty("combatAudio").objectReferenceValue = source;
                so.FindProperty("jabSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/DragonMMA/Audio/fighter_jab.wav");
                so.FindProperty("heavySound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/DragonMMA/Audio/fighter_heavy.wav");
                so.FindProperty("guardSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/DragonMMA/Audio/fighter_guard.wav");
                so.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        public static void WriteContactSheet()
        {
            int width = CanvasWidth * 8, height = CanvasSize * FighterCombatTiming.Poses.Length;
            var output = new Color32[width * height];
            for (int i = 0; i < output.Length; i++) output[i] = new Color32(39, 53, 61, 255);
            for (int row = 0; row < FighterCombatTiming.Poses.Length; row++)
                for (int frame = 0; frame < 8; frame++)
                {
                    var source = Read(ArtRoot + "/fighter_" + FighterCombatTiming.Poses[row] + "_" + frame + ".png");
                    var colors = source.GetPixels32();
                    for (int y = 0; y < CanvasSize; y++) for (int x = 0; x < CanvasWidth; x++)
                        if (colors[y * CanvasWidth + x].a > 0) output[((FighterCombatTiming.Poses.Length - row - 1) * CanvasSize + y) * width + frame * CanvasWidth + x] = colors[y * CanvasWidth + x];
                    UnityEngine.Object.DestroyImmediate(source);
                }
            var t = new Texture2D(width, height, TextureFormat.RGBA32, false); t.SetPixels32(output); t.Apply();
            Directory.CreateDirectory("Artifacts/FighterUpgrade-20261002");
            File.WriteAllBytes("Artifacts/FighterUpgrade-20261002/fighter-contact-sheet.png", t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
        }
    }
}
