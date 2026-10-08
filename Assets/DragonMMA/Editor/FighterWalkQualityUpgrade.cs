using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DragonMMA.EditorTools
{
    public static class FighterWalkQualityUpgrade
    {
        public const string SourcePath = "Assets/DragonMMA/ArtSource/fighter_walk_bounce_v4.png";
        public const string SheetPath = "Assets/DragonMMA/Resources/DragonMMA/Art/fighter_walk_sheet.png";
        private const int FrameWidth = 192, FrameHeight = 128, FloorPixel = 8, TargetHeight = 110;

        private struct Region
        {
            public RectInt Bounds;
            public int PixelCount;
        }

        [MenuItem("Dragon MMA/Fighter/Apply Walk Quality Upgrade")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before rebuilding the fighter walk sheet.");
            if (!File.Exists(SourcePath)) throw new FileNotFoundException("Missing walk-cycle source", SourcePath);

            var source = Read(SourcePath);
            var output = new Texture2D(FrameWidth * 8, FrameHeight, TextureFormat.RGBA32, false);
            try
            {
                var regions = FindEightRegions(source);
                int[] heights = regions.Select(region => region.Bounds.height).OrderBy(height => height).ToArray();
                float sourceHeight = (heights[3] + heights[4]) * .5f;
                float scale = TargetHeight / sourceHeight;
                output.SetPixels32(new Color32[output.width * output.height]);
                var report = new List<string>
                {
                    "Fighter walk quality import",
                    "UTC: " + DateTime.UtcNow.ToString("O"),
                    $"Source: {source.width}x{source.height}; regions={regions.Count}; commonScale={scale:0.0000}",
                    $"Output: {output.width}x{output.height}; frame={FrameWidth}x{FrameHeight}; floor={FloorPixel}px"
                };
                for (int frame = 0; frame < regions.Count; frame++)
                {
                    Color32[] pixels = Rasterize(source, regions[frame].Bounds, scale, frame, out int floorY, out float supportX);
                    output.SetPixels32(frame * FrameWidth, 0, FrameWidth, FrameHeight, pixels);
                    report.Add($"frame {frame}: beat={AnimationMotionQuality.WalkBeat(frame)}; source={regions[frame].Bounds}; pixels={regions[frame].PixelCount}; floor={floorY}; supportX={supportX:0.00}");
                }
                output.Apply(false, false);
                File.WriteAllBytes(SheetPath, output.EncodeToPNG());
                AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
                ValidateImportedSheet();
                report.Add(ValidateBounceMechanics());
                DragonAnimationAuthoring.UpgradeHunterAssets();
                Directory.CreateDirectory("Artifacts/AnimationQuality-20261008");
                File.WriteAllLines("Artifacts/AnimationQuality-20261008/walk-import-report.txt", report);
                AssetDatabase.SaveAssets();
                Debug.Log("[Animation Quality] Fighter walk sheet rebuilt from eight normalized frames; floor, pivot, clip timing and sprite identities validated.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(output);
            }
        }

        public static void ApplyBatch()
        {
            try { Apply(); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        private static Texture2D Read(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (texture.LoadImage(File.ReadAllBytes(path))) return texture;
            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidDataException("Could not decode " + path);
        }

        private static List<Region> FindEightRegions(Texture2D source)
        {
            Color32[] pixels = source.GetPixels32();
            bool[] visited = new bool[pixels.Length];
            var queue = new Queue<int>();
            var found = new List<Region>();
            int threshold = Mathf.Max(2048, pixels.Length / 500);
            for (int seed = 0; seed < pixels.Length; seed++)
            {
                if (visited[seed] || pixels[seed].a < 64) continue;
                visited[seed] = true; queue.Enqueue(seed);
                int count = 0, x0 = source.width, x1 = 0, y0 = source.height, y1 = 0;
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue(), x = index % source.width, y = index / source.width;
                    count++; x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int sx = x + dx, sy = y + dy;
                        if (sx < 0 || sx >= source.width || sy < 0 || sy >= source.height) continue;
                        int next = sy * source.width + sx;
                        if (visited[next] || pixels[next].a < 64) continue;
                        visited[next] = true; queue.Enqueue(next);
                    }
                }
                if (count >= threshold) found.Add(new Region { Bounds = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1), PixelCount = count });
            }
            if (found.Count != 8) throw new InvalidDataException("Expected eight connected fighter silhouettes; found " + found.Count);
            var top = found.OrderByDescending(region => region.Bounds.center.y).Take(4).OrderBy(region => region.Bounds.xMin);
            var bottom = found.OrderBy(region => region.Bounds.center.y).Take(4).OrderBy(region => region.Bounds.xMin);
            return top.Concat(bottom).ToList();
        }

        private static Color32[] Rasterize(Texture2D source, RectInt bounds, float scale, int frame, out int floorY, out float supportX)
        {
            Color32[] sourcePixels = source.GetPixels32();
            var target = new Color32[FrameWidth * FrameHeight];
            float centerX = bounds.center.x;
            for (int y = 0; y < FrameHeight; y++) for (int x = 0; x < FrameWidth; x++)
            {
                int sx = Mathf.RoundToInt(centerX + (x - FrameWidth * .5f) / scale);
                int sy = Mathf.RoundToInt(bounds.yMin + (y - FloorPixel) / scale);
                if (sx < bounds.xMin || sx >= bounds.xMax || sy < bounds.yMin || sy >= bounds.yMax) continue;
                Color32 color = sourcePixels[sy * source.width + sx];
                if (color.a < 128) continue;
                color.a = 255;
                if (color.r < 10 && color.g < 10 && color.b < 10) color = new Color32(18, 17, 20, 255);
                target[y * FrameWidth + x] = color;
            }

            floorY = FindFloor(target);
            if (floorY == FrameHeight) throw new InvalidDataException("Walk frame rasterized without opaque pixels.");
            int desiredFloor = AnimationMotionQuality.WalkFloorPixel(frame);
            int correction = floorY - desiredFloor;
            if (correction != 0)
            {
                var aligned = new Color32[target.Length];
                for (int y = 0; y < FrameHeight; y++)
                {
                    int destinationY = y - correction;
                    if (destinationY < 0 || destinationY >= FrameHeight) continue;
                    Array.Copy(target, y * FrameWidth, aligned, destinationY * FrameWidth, FrameWidth);
                }
                target = aligned;
                floorY = FindFloor(target);
            }
            supportX = SupportCenter(target, floorY);
            if (floorY != desiredFloor) throw new InvalidDataException("Walk frame missed its gait floor. expected=" + desiredFloor + " actual=" + floorY);
            return target;
        }

        public static string ValidateBounceMechanics()
        {
            if (!File.Exists(SheetPath)) throw new FileNotFoundException("Missing walk sheet", SheetPath);
            var texture = Read(SheetPath);
            try
            {
                if (texture.width != FrameWidth * AnimationMotionQuality.WalkFrameCount || texture.height != FrameHeight)
                    throw new InvalidDataException($"Unexpected walk sheet size {texture.width}x{texture.height}.");
                Color32[] pixels = texture.GetPixels32();
                float minBodyY = float.MaxValue, maxBodyY = float.MinValue;
                float minBodyX = float.MaxValue, maxBodyX = float.MinValue;
                var details = new List<string>();
                for (int frame = 0; frame < AnimationMotionQuality.WalkFrameCount; frame++)
                {
                    int floor = FrameHeight, count = 0; float sumX = 0, sumY = 0;
                    for (int y = 0; y < FrameHeight; y++) for (int x = 0; x < FrameWidth; x++)
                    {
                        if (pixels[y * texture.width + frame * FrameWidth + x].a == 0) continue;
                        floor = Mathf.Min(floor, y); sumX += x; sumY += y; count++;
                    }
                    if (count == 0) throw new InvalidDataException("Empty walk frame " + frame);
                    int expectedFloor = AnimationMotionQuality.WalkFloorPixel(frame);
                    if (floor != expectedFloor)
                        throw new InvalidDataException($"Walk frame {frame} ({AnimationMotionQuality.WalkBeat(frame)}) floor={floor}, expected={expectedFloor}. Airborne/contact gait is missing.");
                    float bodyX = sumX / count, bodyY = sumY / count;
                    minBodyX = Mathf.Min(minBodyX, bodyX); maxBodyX = Mathf.Max(maxBodyX, bodyX);
                    minBodyY = Mathf.Min(minBodyY, bodyY); maxBodyY = Mathf.Max(maxBodyY, bodyY);
                    details.Add($"f{frame} {AnimationMotionQuality.WalkBeat(frame)} floor={floor} center=({bodyX:0.00},{bodyY:0.00})");
                }
                float verticalArc = maxBodyY - minBodyY;
                float horizontalDrift = maxBodyX - minBodyX;
                if (verticalArc < 4 || verticalArc > 14)
                    throw new InvalidDataException($"MMA bounce vertical arc must be 4..14px; actual={verticalArc:0.00}px.");
                if (horizontalDrift > 9)
                    throw new InvalidDataException($"Walk body drifts inside the sprite instead of cycling under a stable guard; actual={horizontalDrift:0.00}px.");
                return $"Bounce QA PASS; verticalArc={verticalArc:0.00}px; horizontalDrift={horizontalDrift:0.00}px; " + string.Join(" | ", details);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static float SupportCenter(Color32[] pixels, int floorY)
        {
            int count = 0; float sum = 0;
            for (int y = floorY; y < Mathf.Min(FrameHeight, floorY + 4); y++) for (int x = 0; x < FrameWidth; x++)
                if (pixels[y * FrameWidth + x].a > 0) { sum += x; count++; }
            return count > 0 ? sum / count : -1;
        }

        private static int FindFloor(Color32[] pixels)
        {
            for (int y = 0; y < FrameHeight; y++)
                for (int x = 0; x < FrameWidth; x++) if (pixels[y * FrameWidth + x].a > 0) return y;
            return FrameHeight;
        }

        private static void ValidateImportedSheet()
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
            if (sprites.Length != 8) throw new InvalidDataException("Walk sheet must expose exactly eight sprites.");
            for (int frame = 0; frame < sprites.Length; frame++)
            {
                Sprite sprite = sprites[frame];
                if (sprite.name != "fighter_walk_" + frame || sprite.rect.size != new Vector2(FrameWidth, FrameHeight) ||
                    Vector2.Distance(sprite.pivot, new Vector2(FrameWidth * .5f, FloorPixel)) > .01f)
                    throw new InvalidDataException("Walk sprite metadata changed: " + sprite.name);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
            if (importer.filterMode != FilterMode.Point || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed)
                throw new InvalidDataException("Walk sheet lost pixel-art import settings.");
        }
    }
}
