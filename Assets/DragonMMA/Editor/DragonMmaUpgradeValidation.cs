using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DragonMMA.EditorTools
{
    public static class DragonMmaUpgradeValidation
    {
        private const string IsolationKey = "DragonMma.IsolatedSave";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void UseTestSave()
        {
            string path = SessionState.GetString(IsolationKey, "");
            if (!string.IsNullOrEmpty(path)) DragonSaveSystem.OverridePath = path;
        }

        public static string PrepareIsolatedPlay()
        {
            string path = Path.GetFullPath("Artifacts/QA-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "/save.json");
            SessionState.SetString(IsolationKey, path);
            return path;
        }
        public static void EndIsolation()
        {
            SessionState.EraseString(IsolationKey);
            DragonSaveSystem.OverridePath = null;
        }

        public static void Capture(string path, int width = 1920, int height = 720)
        {
            var view = UnityEngine.Object.FindFirstObjectByType<DragonMmaView>();
            if (view == null) throw new InvalidOperationException("Open the authored overlay scene before capture.");
            var canvas = view.Canvas;
            var camera = Camera.main;
            RenderMode oldMode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            float oldDistance = canvas.planeDistance;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.antiAliasing = 1;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 5;
                view.RefreshLayout(width, height);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllBytes(path, texture.EncodeToPNG());
                var pixel = (Color32)texture.GetPixel(width / 2, height - 3);
                Debug.Log($"[Dragon MMA Capture] {Path.GetFullPath(path)} clearRGB={pixel.r},{pixel.g},{pixel.b}");
            }
            finally
            {
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                view.RefreshLayout(Screen.width, Screen.height);
                target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        public static void WriteContactSheet()
        {
            const int width = 1440, height = 1160;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(28, 48, 49, 255);
            string[] poses = { "idle", "walk", "punch", "kick", "hurt", "clinch", "takedown", "victory" };
            for (int row = 0; row < poses.Length; row++)
                for (int frame = 0; frame < 4; frame++)
                    Stamp(pixels, width, height, $"fighter_{poses[row]}_{frame}", 30 + frame * 170, height - 130 - row * 126, 4);
            for (int kind = 0; kind < 5; kind++)
                for (int frame = 0; frame < 3; frame++)
                    Stamp(pixels, width, height, $"dragon_{kind}_{new[] { "idle", "attack", "hurt" }[frame]}_1", 750 + frame * 210, height - 130 - kind * 145, 4);
            Stamp(pixels, width, height, "tree", 760, 70, 3);
            Stamp(pixels, width, height, "hut", 975, 154, 3);
            Stamp(pixels, width, height, "cart", 1030, 22, 3);
            Stamp(pixels, width, height, "bag", 1300, 100, 3);
            texture.SetPixels32(pixels); texture.Apply();
            Directory.CreateDirectory("Artifacts");
            File.WriteAllBytes("Artifacts/pixel-contact.png", texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void Stamp(Color32[] output, int width, int height, string name, int x, int y, int scale)
        {
            var sprite = DragonArtCatalog.Load(name);
            if (sprite == null) throw new InvalidOperationException("Missing packed art sprite: " + name);
            string path = AssetDatabase.GetAssetPath(sprite);
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(path));
            var sheetPixels = source.GetPixels32();
            int sourceX = Mathf.RoundToInt(sprite.rect.x), sourceY = Mathf.RoundToInt(sprite.rect.y);
            int sourceWidth = Mathf.RoundToInt(sprite.rect.width), sourceHeight = Mathf.RoundToInt(sprite.rect.height);
            for (int sy = 0; sy < sourceHeight; sy++)
                for (int sx = 0; sx < sourceWidth; sx++)
                {
                    Color32 c = sheetPixels[(sourceY + sy) * source.width + sourceX + sx];
                    if (c.a == 0) continue;
                    for (int dy = 0; dy < scale; dy++)
                        for (int dx = 0; dx < scale; dx++)
                        {
                            int px = x + sx * scale + dx, py = y + sy * scale + dy;
                            if (px >= 0 && px < width && py >= 0 && py < height) output[py * width + px] = c;
                        }
                }
            UnityEngine.Object.DestroyImmediate(source);
        }

        public static void SeedShowcase()
        {
            if (string.IsNullOrEmpty(DragonSaveSystem.OverridePath)) throw new InvalidOperationException("Use an isolated test save before seeding.");
            var game = UnityEngine.Object.FindFirstObjectByType<DragonMmaGame>();
            var data = game.DebugData;
            data.storage.Clear();
            for (int i = 0; i < 13; i++) data.storage.Add(new DragonInstance((DragonKind)(i % 5)));
            data.coins = 160;
            game.DebugSession.UnlockTraining();
            var trainee = data.storage.Find(d => d.kind == 1);
            game.DebugSession.AssignTraining(trainee.id, DragonMmaGame.Now - 120);
            game.DebugSession.ProcessTimers(DragonMmaGame.Now);
            var seller = data.storage.Find(d => d.kind == 0);
            game.DebugSession.RegisterSale(seller.id, 0, DragonMmaGame.Now - 30);
            game.DebugSession.ProcessTimers(DragonMmaGame.Now);
            game.DebugForceEncounter(DragonKind.Stonehorn);
            game.DebugSession.Cheer(34);
            game.DebugView.Refresh(true);
        }
    }
}

