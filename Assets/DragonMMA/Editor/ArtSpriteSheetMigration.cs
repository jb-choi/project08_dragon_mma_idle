using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DragonMMA.EditorTools
{
    /// <summary>
    /// Packs the historical one-PNG-per-sprite art into deterministic multi-sprite
    /// textures, rewrites serialized references, and removes the loose runtime copies.
    /// Sprite names, pixel rectangles, pivots, and pixels are preserved.
    /// </summary>
    public static class ArtSpriteSheetMigration
    {
        public const string ArtRoot = "Assets/DragonMMA/Resources/DragonMMA/Art";
        public const string CombinedFighterSheetPath = ArtRoot + "/fighter_sheet.png";
        public const string LegacyDragonSheetPath = ArtRoot + "/legacy_dragon_sheet.png";
        public const string EnvironmentSheetPath = ArtRoot + "/environment_sheet.png";
        public const string ReferenceSourcePath = "Assets/DragonMMA/ArtSource/fighter_reference_boxing_guard.png";

        public static readonly string[] FighterPoses =
        {
            "idle", "walk", "punch", "cross", "kick", "hurt",
            "block", "clinch", "takedown", "knockdown", "recovery", "victory"
        };

        public static string FighterSheetPath(string pose) => ArtRoot + "/fighter_" + pose + "_sheet.png";

        private static readonly string[] EnvironmentNames =
        {
            "bag", "cart", "coin", "forest_tile", "hut", "impact", "shrub", "tree"
        };

        private sealed class Entry
        {
            public string name;
            public string sourcePath;
            public RectInt rect;
            public Vector2 pivot;
            public Sprite oldSprite;
            public Color32[] pixels;
            public int width;
            public int height;
        }

        [MenuItem("Dragon MMA/Art/Pack Loose Sprites Into Sheets")]
        public static void Migrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before packing art sprites.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new OperationCanceledException("Sprite-sheet migration was canceled before saving open scenes.");

            var replacements = new Dictionary<Sprite, Sprite>();
            int packed = 0;
            packed += BuildFighterSheets(replacements);
            packed += BuildLegacyDragonSheet(replacements);
            packed += BuildEnvironmentSheet(replacements);

            if (replacements.Count > 0)
            {
                ReplaceAnimationReferences(replacements);
                ReplacePrefabReferences(replacements);
                ReplaceSceneReferences(replacements);
                ReplaceScriptableObjectReferences(replacements);
                AssetDatabase.SaveAssets();
                var oldPaths = replacements.Keys.Select(AssetDatabase.GetAssetPath).Distinct().ToArray();
                ValidateNoSerializedOldGuids(oldPaths);
                foreach (string path in oldPaths)
                    if (!string.IsNullOrEmpty(path) && !AssetDatabase.DeleteAsset(path))
                        throw new IOException("Could not remove packed source sprite: " + path);
            }

            MoveReferenceSourceOutOfResources();
            DragonArtCatalog.ClearCache();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateOrThrow();
            WriteReport(packed, replacements.Count);
            Debug.Log($"[Art Sprite Sheets] Migrated {packed} sprite frames into animation-scoped sheets and rewrote {replacements.Count} sprite identities.");
        }

        public static void MigrateBatch()
        {
            Migrate();
        }

        public static Sprite LoadSprite(string name)
        {
            var sprite = DragonArtCatalog.Load(name);
            if (sprite == null) throw new InvalidOperationException("Missing packed art sprite: " + name);
            return sprite;
        }

        public static void ValidateOrThrow()
        {
            foreach (string pose in FighterPoses)
                ValidateSheet(FighterSheetPath(pose), FighterNames(pose), 32, new Vector2(96, 8), new Vector2(192, 128));
            ValidateSheet(LegacyDragonSheetPath, LegacyDragonNames(), 20, new Vector2(20, 15), new Vector2(40, 30));
            ValidateEnvironmentSheet();

            string[] expectedPngs = FighterPoses.Select(pose => "fighter_" + pose + "_sheet.png")
                .Concat(new[] { "environment_sheet.png", "legacy_dragon_sheet.png", "tree_sprite_sheet.png" })
                .ToArray();
            var actualPngs = Directory.GetFiles(ArtRoot, "*.png", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName).OrderBy(name => name).ToArray();
            if (!actualPngs.SequenceEqual(expectedPngs.OrderBy(name => name)))
                throw new InvalidOperationException("Art Resources must contain only the animation and shared sprite sheets. Found: " + string.Join(", ", actualPngs));

            if (!File.Exists(ReferenceSourcePath))
                throw new InvalidOperationException("The fighter reference image was not preserved in ArtSource.");

            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/DragonMMA" }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                        if (key.value == null)
                            throw new InvalidOperationException($"Animation clip {clip.name} contains a missing object reference.");
            }
        }

        private static int BuildFighterSheets(Dictionary<Sprite, Sprite> replacements)
        {
            var names = FighterNames();
            var loosePaths = names.Select(name => ArtRoot + "/" + name + ".png").ToArray();
            int looseCount = loosePaths.Count(File.Exists);
            bool hasCombinedSheet = File.Exists(CombinedFighterSheetPath);
            int animationSheetCount = FighterPoses.Count(pose => File.Exists(FighterSheetPath(pose)));

            if (looseCount == 0 && !hasCombinedSheet)
            {
                if (animationSheetCount != FighterPoses.Length)
                    throw new InvalidDataException($"Incomplete fighter animation sheets: {animationSheetCount}/{FighterPoses.Length} files.");
                return 0;
            }
            if (looseCount != 0 && looseCount != loosePaths.Length)
                throw new InvalidDataException($"Incomplete loose fighter sprite group: {looseCount}/{loosePaths.Length} files.");

            Dictionary<string, Sprite> combinedSprites = null;
            Color32[] combinedPixels = null;
            int combinedWidth = 0;
            if (looseCount == 0)
            {
                combinedSprites = AssetDatabase.LoadAllAssetsAtPath(CombinedFighterSheetPath)
                    .OfType<Sprite>().ToDictionary(sprite => sprite.name);
                var texture = ReadTexture(CombinedFighterSheetPath);
                try { combinedPixels = texture.GetPixels32(); combinedWidth = texture.width; }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }

            int migrated = 0;
            foreach (string pose in FighterPoses)
            {
                var entries = new List<Entry>(8);
                for (int frame = 0; frame < 8; frame++)
                {
                    string name = "fighter_" + pose + "_" + frame;
                    var targetRect = new RectInt(frame * 192, 0, 192, 128);
                    entries.Add(looseCount > 0
                        ? ReadEntry(name, targetRect, new Vector2(.5f, 8f / 128f))
                        : ReadCombinedFighterEntry(name, targetRect, combinedSprites, combinedPixels, combinedWidth));
                }
                migrated += BuildSheet(FighterSheetPath(pose), 1536, 128, 32, entries, replacements);
            }
            return migrated;
        }

        private static Entry ReadCombinedFighterEntry(string name, RectInt targetRect, IReadOnlyDictionary<string, Sprite> sprites, Color32[] sourcePixels, int sourceWidth)
        {
            if (!sprites.TryGetValue(name, out var oldSprite))
                throw new InvalidDataException("Combined fighter sheet is missing " + name);
            var sourceRect = oldSprite.rect;
            if (Mathf.RoundToInt(sourceRect.width) != targetRect.width || Mathf.RoundToInt(sourceRect.height) != targetRect.height)
                throw new InvalidDataException("Combined fighter frame has an unexpected size: " + name);
            int sourceX = Mathf.RoundToInt(sourceRect.x), sourceY = Mathf.RoundToInt(sourceRect.y);
            var pixels = new Color32[targetRect.width * targetRect.height];
            for (int y = 0; y < targetRect.height; y++)
                Array.Copy(sourcePixels, (sourceY + y) * sourceWidth + sourceX, pixels, y * targetRect.width, targetRect.width);
            return new Entry
            {
                name = name, sourcePath = CombinedFighterSheetPath, rect = targetRect,
                pivot = new Vector2(.5f, 8f / 128f), oldSprite = oldSprite,
                pixels = pixels, width = targetRect.width, height = targetRect.height
            };
        }

        private static int BuildLegacyDragonSheet(Dictionary<Sprite, Sprite> replacements)
        {
            var names = LegacyDragonNames();
            if (!HasCompleteLooseSet(names, LegacyDragonSheetPath)) return 0;
            var entries = new List<Entry>(names.Length);
            for (int kind = 0; kind < 5; kind++)
            {
                int cell = 0;
                AddLegacy(entries, kind, null, -1, cell++);
                foreach (string pose in new[] { "idle", "attack", "hurt" })
                    for (int frame = 0; frame < 4; frame++)
                        AddLegacy(entries, kind, pose, frame, cell++);
            }
            return BuildSheet(LegacyDragonSheetPath, 520, 150, 20, entries, replacements);
        }

        private static void AddLegacy(List<Entry> entries, int kind, string pose, int frame, int cell)
        {
            string name = pose == null ? "dragon_" + kind : $"dragon_{kind}_{pose}_{frame}";
            entries.Add(ReadEntry(name, new RectInt(cell * 40, (4 - kind) * 30, 40, 30), new Vector2(.5f, .5f)));
        }

        private static int BuildEnvironmentSheet(Dictionary<Sprite, Sprite> replacements)
        {
            if (!HasCompleteLooseSet(EnvironmentNames, EnvironmentSheetPath)) return 0;
            var layout = new Dictionary<string, RectInt>
            {
                { "tree", new RectInt(0, 0, 64, 100) },
                { "forest_tile", new RectInt(64, 0, 192, 72) },
                { "hut", new RectInt(0, 100, 96, 72) },
                { "cart", new RectInt(96, 100, 64, 40) },
                { "bag", new RectInt(160, 100, 24, 40) },
                { "impact", new RectInt(184, 100, 32, 32) },
                { "coin", new RectInt(216, 100, 12, 12) },
                { "shrub", new RectInt(96, 140, 48, 28) }
            };
            var entries = EnvironmentNames.Select(name => ReadEntry(name, layout[name], new Vector2(.5f, .5f))).ToList();
            return BuildSheet(EnvironmentSheetPath, 256, 256, 20, entries, replacements);
        }

        private static Entry ReadEntry(string name, RectInt rect, Vector2 pivot)
        {
            string path = ArtRoot + "/" + name + ".png";
            var oldSprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (oldSprite == null) throw new InvalidOperationException("Loose sprite was not imported: " + path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidDataException(path);
                if (texture.width != rect.width || texture.height != rect.height)
                    throw new InvalidDataException($"{path} is {texture.width}x{texture.height}; expected {rect.width}x{rect.height}.");
                return new Entry
                {
                    name = name, sourcePath = path, rect = rect, pivot = pivot,
                    oldSprite = oldSprite, pixels = texture.GetPixels32(), width = texture.width, height = texture.height
                };
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static Texture2D ReadTexture(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (texture.LoadImage(File.ReadAllBytes(path))) return texture;
            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidDataException(path);
        }

        private static int BuildSheet(string sheetPath, int width, int height, float ppu, List<Entry> entries, Dictionary<Sprite, Sprite> replacements)
        {
            var sheet = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                sheet.SetPixels32(new Color32[width * height]);
                foreach (var entry in entries)
                    sheet.SetPixels32(entry.rect.x, entry.rect.y, entry.width, entry.height, entry.pixels);
                sheet.Apply(false, false);
                File.WriteAllBytes(sheetPath, sheet.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(sheet); }

            AssetDatabase.ImportAsset(sheetPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureSheet(sheetPath, ppu, entries);
            DragonArtCatalog.ClearCache();
            var byName = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().ToDictionary(sprite => sprite.name);
            foreach (var entry in entries)
            {
                if (!byName.TryGetValue(entry.name, out var replacement))
                    throw new InvalidOperationException("Packed sprite was not imported: " + entry.name);
                replacements[entry.oldSprite] = replacement;
            }
            VerifyPackedPixels(sheetPath, entries);
            return entries.Count;
        }

        private static void ConfigureSheet(string path, float ppu, List<Entry> entries)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing texture importer: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects().GroupBy(rect => rect.name).ToDictionary(group => group.Key, group => group.First());
            var rects = entries.Select(entry => new SpriteRect
            {
                name = entry.name,
                rect = new Rect(entry.rect.x, entry.rect.y, entry.rect.width, entry.rect.height),
                pivot = entry.pivot,
                alignment = SpriteAlignment.Custom,
                border = Vector4.zero,
                spriteID = previous.TryGetValue(entry.name, out var old) ? old.spriteID : GUID.Generate()
            }).ToArray();
            provider.SetSpriteRects(rects);
            var ids = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (ids != null) ids.SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static void VerifyPackedPixels(string sheetPath, IEnumerable<Entry> entries)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(sheetPath))) throw new InvalidDataException(sheetPath);
                var pixels = texture.GetPixels32();
                foreach (var entry in entries)
                {
                    for (int y = 0; y < entry.height; y++)
                        for (int x = 0; x < entry.width; x++)
                            if (!pixels[(entry.rect.y + y) * texture.width + entry.rect.x + x].Equals(entry.pixels[y * entry.width + x]))
                                throw new InvalidDataException("Pixel mismatch after packing " + entry.name);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static bool HasCompleteLooseSet(IEnumerable<string> names, string sheetPath)
        {
            var paths = names.Select(name => ArtRoot + "/" + name + ".png").ToArray();
            int existing = paths.Count(File.Exists);
            if (existing == 0)
            {
                if (!File.Exists(sheetPath)) throw new FileNotFoundException("Neither a sprite sheet nor its loose sources exist.", sheetPath);
                return false;
            }
            if (existing != paths.Length)
                throw new InvalidDataException($"Incomplete loose sprite group for {sheetPath}: {existing}/{paths.Length} files.");
            return true;
        }

        private static void ReplaceAnimationReferences(IReadOnlyDictionary<Sprite, Sprite> replacements)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets" }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                bool changed = false;
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    for (int i = 0; i < keys.Length; i++)
                        if (keys[i].value is Sprite old && replacements.TryGetValue(old, out var replacement))
                        { keys[i].value = replacement; changed = true; }
                    if (changed) AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
                }
                if (changed) EditorUtility.SetDirty(clip);
            }
        }

        private static void ReplacePrefabReferences(IReadOnlyDictionary<Sprite, Sprite> replacements)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = root.GetComponentsInChildren<Component>(true)
                        .Where(component => component != null)
                        .Aggregate(false, (current, component) => ReplaceSerializedReferences(component, replacements) || current);
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static void ReplaceSceneReferences(IReadOnlyDictionary<Sprite, Sprite> replacements)
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var scene = SceneManager.GetSceneByPath(path);
                    bool wasLoaded = scene.IsValid() && scene.isLoaded;
                    if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    bool changed = false;
                    foreach (var root in scene.GetRootGameObjects())
                        foreach (var component in root.GetComponentsInChildren<Component>(true))
                            if (component != null) changed |= ReplaceSerializedReferences(component, replacements);
                    if (changed) EditorSceneManager.SaveScene(scene);
                    if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
                }
            }
            finally
            {
                if (setup.Any(scene => scene.isLoaded && scene.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static void ReplaceScriptableObjectReferences(IReadOnlyDictionary<Sprite, Sprite> replacements)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path).OfType<ScriptableObject>())
                    if (ReplaceSerializedReferences(asset, replacements)) EditorUtility.SetDirty(asset);
            }
        }

        private static bool ReplaceSerializedReferences(UnityEngine.Object target, IReadOnlyDictionary<Sprite, Sprite> replacements)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.GetIterator();
            bool changed = false;
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (!(property.objectReferenceValue is Sprite old) || !replacements.TryGetValue(old, out var replacement)) continue;
                property.objectReferenceValue = replacement;
                changed = true;
            }
            if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static void ValidateNoSerializedOldGuids(IEnumerable<string> oldPaths)
        {
            var oldGuids = oldPaths.Select(AssetDatabase.AssetPathToGUID).Where(guid => !string.IsNullOrEmpty(guid)).ToArray();
            string[] extensions = { ".anim", ".prefab", ".unity", ".asset", ".controller", ".overrideController", ".spriteatlas", ".mat" };
            foreach (string path in Directory.GetFiles("Assets", "*", SearchOption.AllDirectories)
                         .Where(path => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)))
            {
                string text = File.ReadAllText(path);
                string stale = oldGuids.FirstOrDefault(guid => text.Contains("guid: " + guid));
                if (stale != null) throw new InvalidOperationException($"Serialized asset still references loose sprite GUID {stale}: {path}");
            }
        }

        private static void MoveReferenceSourceOutOfResources()
        {
            string oldPath = ArtRoot + "/fighter_reference_boxing_guard.png";
            if (AssetDatabase.LoadMainAssetAtPath(oldPath) == null) return;
            if (AssetDatabase.LoadMainAssetAtPath(ReferenceSourcePath) != null)
                throw new IOException("Reference destination already exists: " + ReferenceSourcePath);
            string error = AssetDatabase.MoveAsset(oldPath, ReferenceSourcePath);
            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
        }

        private static void ValidateSheet(string path, IEnumerable<string> expectedNames, float ppu, Vector2 pivot, Vector2 size)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.spriteImportMode != SpriteImportMode.Multiple || Math.Abs(importer.spritePixelsPerUnit - ppu) > .001f)
                throw new InvalidOperationException("Invalid sprite-sheet importer: " + path);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(sprite => sprite.name);
            var names = expectedNames.ToArray();
            if (sprites.Count != names.Length || names.Any(name => !sprites.ContainsKey(name)))
                throw new InvalidOperationException($"{path} has {sprites.Count} sprites; expected {names.Length} exact names.");
            foreach (string name in names)
            {
                var sprite = sprites[name];
                if (sprite.rect.size != size || Vector2.Distance(sprite.pivot, pivot) > .01f)
                    throw new InvalidOperationException($"{name} has an unexpected rect or pivot.");
            }
        }

        private static void ValidateEnvironmentSheet()
        {
            var importer = AssetImporter.GetAtPath(EnvironmentSheetPath) as TextureImporter;
            if (importer == null || importer.spriteImportMode != SpriteImportMode.Multiple || Math.Abs(importer.spritePixelsPerUnit - 20) > .001f)
                throw new InvalidOperationException("Invalid environment sprite-sheet importer.");
            var sprites = AssetDatabase.LoadAllAssetsAtPath(EnvironmentSheetPath).OfType<Sprite>().ToDictionary(sprite => sprite.name);
            var sizes = new Dictionary<string, Vector2>
            {
                { "bag", new Vector2(24, 40) }, { "cart", new Vector2(64, 40) }, { "coin", new Vector2(12, 12) },
                { "forest_tile", new Vector2(192, 72) }, { "hut", new Vector2(96, 72) }, { "impact", new Vector2(32, 32) },
                { "shrub", new Vector2(48, 28) }, { "tree", new Vector2(64, 100) }
            };
            if (sprites.Count != sizes.Count || sizes.Any(pair => !sprites.ContainsKey(pair.Key) || sprites[pair.Key].rect.size != pair.Value))
                throw new InvalidOperationException("Environment sheet names or rectangles are invalid.");
        }

        private static string[] FighterNames() => FighterPoses.SelectMany(pose => FighterNames(pose)).ToArray();

        private static string[] FighterNames(string pose) => Enumerable.Range(0, 8).Select(frame => $"fighter_{pose}_{frame}").ToArray();

        private static string[] LegacyDragonNames()
        {
            var names = new List<string>(65);
            for (int kind = 0; kind < 5; kind++)
            {
                names.Add("dragon_" + kind);
                foreach (string pose in new[] { "idle", "attack", "hurt" })
                    for (int frame = 0; frame < 4; frame++) names.Add($"dragon_{kind}_{pose}_{frame}");
            }
            return names.ToArray();
        }

        private static void WriteReport(int packed, int replacements)
        {
            const string directory = "Artifacts/ArtSpriteSheets-20261006";
            Directory.CreateDirectory(directory);
            File.WriteAllText(directory + "/migration-report.txt",
                $"UTC: {DateTime.UtcNow:O}\nMigrated sprite frames: {packed}\nRewritten sprite identities: {replacements}\n" +
                "Runtime sheets: 12 fighter animation sheets (8 frames each), legacy_dragon_sheet (65), environment_sheet (8), tree_sprite_sheet (5)\n" +
                "Validation: names, counts, rects, pivots, import settings, animation object references, and Art folder contents passed.\n");
        }
    }
}
