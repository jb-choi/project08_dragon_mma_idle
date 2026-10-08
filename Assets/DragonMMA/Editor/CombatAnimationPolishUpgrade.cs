using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DragonMMA.EditorTools
{
    public static class CombatAnimationPolishUpgrade
    {
        public const string ImpactAtlasPath = "Assets/DragonMMA/ArtSource/combat_impact_quality_v2.png";
        public const string WeakPath = "Assets/DragonMMA/UIArt/combat_impact_weak_v2.png";
        public const string HeavyPath = "Assets/DragonMMA/UIArt/combat_impact_heavy_v2.png";
        public const string GuardPath = "Assets/DragonMMA/UIArt/combat_impact_guard_v2.png";
        public const string KnockdownPath = "Assets/DragonMMA/UIArt/combat_impact_dust_v2.png";
        private const int OutputSize = 256;

        [MenuItem("Dragon MMA/Combat/Apply Animation And Impact Polish")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before rebuilding animation and impact assets.");
            if (!File.Exists(ImpactAtlasPath)) throw new FileNotFoundException("Missing combat impact atlas", ImpactAtlasPath);

            WriteImpactSprites();
            FighterWalkQualityUpgrade.Apply();
            ConfigurePrefab("Assets/DragonMMA/Prefabs/UI/OverlayRoot.prefab");
            ConfigurePrefab("Assets/DragonMMA/Prefabs/UI/WebOverlayRoot.prefab");
            AssetDatabase.SaveAssets();

            Directory.CreateDirectory("Artifacts/CombatAnimationPolish-20261008");
            File.WriteAllText("Artifacts/CombatAnimationPolish-20261008/apply-report.txt",
                "Combat animation polish applied\n" +
                "Walk source: " + FighterWalkQualityUpgrade.SourcePath + "\n" +
                "Impact source: " + ImpactAtlasPath + "\n" +
                "Fighter punch/cross/kick root travel: 0 px\n" +
                "Combat stance offset: " + AnimationMotionQuality.FighterCombatStanceOffset + "\n" +
                "Walk ground speed: " + AnimationMotionQuality.RecommendedWalkGroundSpeed + " px/s\n");
            Debug.Log("[Combat Animation Polish] Walk, in-place strikes and authored impact sprites applied.");
        }

        public static void ApplyBatch()
        {
            try { Apply(); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        private static void WriteImpactSprites()
        {
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!atlas.LoadImage(File.ReadAllBytes(ImpactAtlasPath)))
                    throw new InvalidDataException("Could not decode " + ImpactAtlasPath);
                WriteQuadrant(atlas, 0, 1, WeakPath);
                WriteQuadrant(atlas, 1, 1, HeavyPath);
                WriteQuadrant(atlas, 0, 0, GuardPath);
                WriteQuadrant(atlas, 1, 0, KnockdownPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(atlas); }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in new[] { WeakPath, HeavyPath, GuardPath, KnockdownPath })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spritePivot = new Vector2(.5f, .5f);
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        private static void WriteQuadrant(Texture2D atlas, int column, int row, string path)
        {
            int cellWidth = atlas.width / 2, cellHeight = atlas.height / 2;
            int contentWidth = OutputSize;
            int contentHeight = Mathf.Max(1, Mathf.RoundToInt(OutputSize * cellHeight / (float)cellWidth));
            int offsetY = (OutputSize - contentHeight) / 2;
            var output = new Texture2D(OutputSize, OutputSize, TextureFormat.RGBA32, false);
            try
            {
                output.SetPixels32(new Color32[OutputSize * OutputSize]);
                int sourceX = column * cellWidth, sourceY = row * cellHeight;
                for (int y = 0; y < contentHeight; y++)
                    for (int x = 0; x < contentWidth; x++)
                    {
                        int sx = sourceX + Mathf.Clamp(Mathf.FloorToInt((x + .5f) * cellWidth / contentWidth), 0, cellWidth - 1);
                        int sy = sourceY + Mathf.Clamp(Mathf.FloorToInt((y + .5f) * cellHeight / contentHeight), 0, cellHeight - 1);
                        Color32 color = atlas.GetPixel(sx, sy);
                        if (color.a < 4) color = default;
                        output.SetPixel(x, y + offsetY, color);
                    }
                output.Apply(false, false);
                File.WriteAllBytes(path, output.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(output); }
        }

        private static void ConfigurePrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<DragonMmaView>();
                var bindings = root.GetComponent<DragonUiBindings>();
                var data = new SerializedObject(view);
                Sprite weak = AssetDatabase.LoadAssetAtPath<Sprite>(WeakPath);
                Sprite heavy = AssetDatabase.LoadAssetAtPath<Sprite>(HeavyPath);
                Sprite guard = AssetDatabase.LoadAssetAtPath<Sprite>(GuardPath);
                Sprite dust = AssetDatabase.LoadAssetAtPath<Sprite>(KnockdownPath);
                if (weak == null || heavy == null || guard == null || dust == null)
                    throw new InvalidDataException("One or more authored impact sprites failed to import.");

                data.FindProperty("jabTravel").floatValue = 0;
                data.FindProperty("powerPunchTravel").floatValue = 0;
                data.FindProperty("upgradedPunchTravel").floatValue = 0;
                data.FindProperty("scrollSpeed").floatValue = AnimationMotionQuality.RecommendedWalkGroundSpeed;
                data.FindProperty("weakImpactSprite").objectReferenceValue = weak;
                data.FindProperty("heavyImpactSprite").objectReferenceValue = heavy;
                data.FindProperty("guardImpactSprite").objectReferenceValue = guard;
                data.FindProperty("knockdownImpactSprite").objectReferenceValue = dust;

                bindings.Get<Image>("Hunt/Contact sparks").sprite = weak;
                var ring = (Image)data.FindProperty("impactRing").objectReferenceValue;
                var echo = (Image)data.FindProperty("impactEcho").objectReferenceValue;
                var ground = (Image)data.FindProperty("impactDust").objectReferenceValue;
                if (ring != null) ring.sprite = guard;
                if (echo != null) echo.sprite = heavy;
                if (ground != null) ground.sprite = dust;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
