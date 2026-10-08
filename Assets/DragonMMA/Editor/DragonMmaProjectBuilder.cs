using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace DragonMMA.EditorTools
{
    public static class DragonMmaProjectBuilder
    {
        private const string Root = "Assets/DragonMMA";
        private const string ArtRoot = Root + "/Resources/DragonMMA/Art";
        private const string ScenePath = Root + "/Scenes/DesktopOverlayScene.unity";

        [MenuItem("Dragon MMA/Legacy/Create Initial Project (new scene only)")]
        public static void BuildProject()
        {
            if (File.Exists(ScenePath))
            {
                Debug.LogWarning("Scene already exists. Edit its OverlayRoot/prefabs; the initial builder will not overwrite authored scenes.");
                return;
            }
            EnsureFolders();
            DragonPixelArtUpgrade.Generate();
            CreateScene();
            DragonMmaSceneAuthoring.ConvertCurrentScene();
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Dragon MMA] MVP project, pixel art, and DesktopOverlayScene created.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "DragonMMA");
            EnsureFolder(Root, "Resources");
            EnsureFolder(Root + "/Resources", "DragonMMA");
            EnsureFolder(Root + "/Resources/DragonMMA", "Art");
            EnsureFolder(Root, "Scenes");
            EnsureFolder(Root, "Scripts");
            EnsureFolder(Root, "Tests");
            EnsureFolder(Root + "/Tests", "Editor");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }

        private static void GenerateArt()
        {
            string[] sets = { "idle", "walk", "punch", "kick", "hurt" };
            for (int pose = 0; pose < sets.Length; pose++)
            {
                for (int frame = 0; frame < 2; frame++)
                    SaveTexture($"fighter_{sets[pose]}_{frame}", DrawFighter(pose, frame), 40, 30, 20f);
            }

            Color[] bodies =
            {
                C("#65C96B"), C("#E49B3E"), C("#6FB8C9"), C("#9270C4"), C("#D85E55")
            };
            for (int i = 0; i < bodies.Length; i++)
                SaveTexture($"dragon_{i}", DrawDragon(i, bodies[i]), 48, 32, 20f);

            SaveTexture("cart", DrawCart(), 48, 32, 20f);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 20f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }

        private static Color32[] DrawFighter(int pose, int frame)
        {
            const int width = 40;
            const int height = 30;
            Color32[] pixels = Clear(width, height);
            Color32 outline = C("#17252B");
            Color32 skin = C("#D99362");
            Color32 skinLight = C("#F0B27D");
            Color32 hair = C("#3C2726");
            Color32 gloves = C("#E5533D");
            Color32 shorts = C("#2B6F8C");
            Color32 trim = C("#F6D25C");
            int bob = frame == 0 ? 0 : 1;

            Fill(pixels, width, height, 14, 20 + bob, 12, 8, outline);
            Fill(pixels, width, height, 15, 21 + bob, 10, 6, skin);
            Fill(pixels, width, height, 15, 26 + bob, 10, 2, hair);
            Fill(pixels, width, height, 16, 23 + bob, 3, 2, skinLight);
            Pixel(pixels, width, height, 22, 24 + bob, outline);

            Fill(pixels, width, height, 13, 11 + bob, 14, 9, outline);
            Fill(pixels, width, height, 14, 12 + bob, 12, 7, C("#EDE2C4"));
            Fill(pixels, width, height, 14, 9 + bob, 12, 4, outline);
            Fill(pixels, width, height, 15, 9 + bob, 10, 3, shorts);
            Fill(pixels, width, height, 18, 11 + bob, 4, 1, trim);

            if (pose == 2)
            {
                Fill(pixels, width, height, 25, 15 + bob, 10 + frame * 2, 4, outline);
                Fill(pixels, width, height, 26, 16 + bob, 8 + frame * 2, 2, skin);
                Fill(pixels, width, height, 34 + frame * 2, 15 + bob, 4, 4, gloves);
                Fill(pixels, width, height, 8, 13 + bob, 7, 4, outline);
                Fill(pixels, width, height, 9, 14 + bob, 5, 2, skin);
            }
            else if (pose == 3)
            {
                Fill(pixels, width, height, 8, 14 + bob, 7, 5, outline);
                Fill(pixels, width, height, 9, 15 + bob, 5, 3, gloves);
                Fill(pixels, width, height, 24, 6 + frame, 13, 5, outline);
                Fill(pixels, width, height, 25, 7 + frame, 11, 3, skin);
            }
            else
            {
                int armShift = pose == 1 && frame == 1 ? 2 : 0;
                Fill(pixels, width, height, 8 - armShift, 13 + bob, 7, 5, outline);
                Fill(pixels, width, height, 9 - armShift, 14 + bob, 5, 3, skin);
                Fill(pixels, width, height, 25, 13 + bob, 7 + armShift, 5, outline);
                Fill(pixels, width, height, 26, 14 + bob, 5 + armShift, 3, skin);
                Fill(pixels, width, height, 7 - armShift, 13 + bob, 4, 4, gloves);
                Fill(pixels, width, height, 30 + armShift, 13 + bob, 4, 4, gloves);
            }

            int leftLegX = pose == 1 && frame == 1 ? 11 : 14;
            int rightLegX = pose == 1 && frame == 1 ? 24 : 22;
            Fill(pixels, width, height, leftLegX, 3, 6, 7, outline);
            Fill(pixels, width, height, leftLegX + 1, 4, 4, 6, skin);
            Fill(pixels, width, height, rightLegX, 3, 6, 7, outline);
            Fill(pixels, width, height, rightLegX + 1, 4, 4, 6, skin);
            Fill(pixels, width, height, leftLegX - 2, 1, 8, 3, outline);
            Fill(pixels, width, height, rightLegX, 1, 8, 3, outline);

            if (pose == 4)
            {
                ShiftDiagonal(pixels, width, height);
            }
            return pixels;
        }

        private static void ShiftDiagonal(Color32[] pixels, int width, int height)
        {
            Color32[] copy = (Color32[])pixels.Clone();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Color32 color = copy[y * width + x];
                if (color.a == 0) continue;
                int nx = x + (y < 15 ? 2 : 0);
                int ny = Mathf.Max(0, y - x / 16);
                Pixel(pixels, width, height, nx, ny, color);
            }
        }

        private static Color32[] DrawDragon(int variant, Color32 body)
        {
            const int width = 48;
            const int height = 32;
            Color32[] pixels = Clear(width, height);
            Color32 outline = C("#17252B");
            Color32 belly = C("#F0D58A");
            Color32 horn = C("#E9E2C9");
            Color32 dark = Darken(body, 0.68f);

            Fill(pixels, width, height, 9, 8, 29, 16, outline);
            Fill(pixels, width, height, 10, 9, 27, 14, body);
            Fill(pixels, width, height, 31, 14, 12, 10, outline);
            Fill(pixels, width, height, 32, 15, 10, 8, body);
            Fill(pixels, width, height, 14, 9, 14, 5, belly);
            Fill(pixels, width, height, 4, 13, 8, 6, outline);
            Fill(pixels, width, height, 5, 14, 7, 4, dark);
            Fill(pixels, width, height, 12, 4, 7, 6, outline);
            Fill(pixels, width, height, 13, 5, 5, 5, dark);
            Fill(pixels, width, height, 28, 4, 7, 6, outline);
            Fill(pixels, width, height, 29, 5, 5, 5, dark);
            Pixel(pixels, width, height, 39, 20, C("#FFF5C2"));
            Pixel(pixels, width, height, 40, 20, outline);

            int horns = variant + 1;
            for (int i = 0; i < horns && i < 4; i++)
            {
                Fill(pixels, width, height, 32 + i * 2, 24 + (i % 2), 2, 4, horn);
                Pixel(pixels, width, height, 32 + i * 2, 28 + (i % 2), outline);
            }
            if (variant == 2) Fill(pixels, width, height, 1, 9, 7, 4, dark);
            if (variant == 3) Fill(pixels, width, height, 18, 23, 8, 5, horn);
            if (variant == 4)
            {
                Fill(pixels, width, height, 6, 6, 5, 5, dark);
                Fill(pixels, width, height, 38, 8, 7, 6, dark);
            }
            return pixels;
        }

        private static Color32[] DrawCart()
        {
            const int width = 48;
            const int height = 32;
            Color32[] pixels = Clear(width, height);
            Color32 outline = C("#17252B");
            Color32 wood = C("#A95C32");
            Color32 light = C("#D98A48");
            Fill(pixels, width, height, 6, 9, 34, 13, outline);
            Fill(pixels, width, height, 8, 11, 30, 9, wood);
            Fill(pixels, width, height, 10, 16, 26, 2, light);
            Fill(pixels, width, height, 39, 17, 8, 3, outline);
            Fill(pixels, width, height, 9, 4, 9, 7, outline);
            Fill(pixels, width, height, 11, 6, 5, 5, C("#4D5C63"));
            Fill(pixels, width, height, 29, 4, 9, 7, outline);
            Fill(pixels, width, height, 31, 6, 5, 5, C("#4D5C63"));
            return pixels;
        }

        private static void SaveTexture(string fileName, Color32[] pixels, int width, int height, float pixelsPerUnit)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.SetPixels32(pixels);
            texture.Apply();
            string path = ArtRoot + "/" + fileName + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static Color32[] Clear(int width, int height)
        {
            return new Color32[width * height];
        }

        private static void Fill(Color32[] pixels, int width, int height, int x, int y, int rectWidth, int rectHeight, Color32 color)
        {
            for (int py = y; py < y + rectHeight; py++)
            for (int px = x; px < x + rectWidth; px++)
                Pixel(pixels, width, height, px, py, color);
        }

        private static void Pixel(Color32[] pixels, int width, int height, int x, int y, Color32 color)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            pixels[y * width + x] = color;
        }

        private static Color32 C(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color color);
            return color;
        }

        private static Color32 Darken(Color32 color, float multiplier)
        {
            return new Color32((byte)(color.r * multiplier), (byte)(color.g * multiplier), (byte)(color.b * multiplier), color.a);
        }

        private static void CreateScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = DesktopOverlayWindow.TransparencyKey;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            GameObject bootstrap = new GameObject("Dragon MMA Bootstrap");
            bootstrap.AddComponent<DesktopOverlayWindow>();
            bootstrap.AddComponent<DragonMmaGame>();

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeGameObject = bootstrap;
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Dragon MMA Studio";
            PlayerSettings.productName = "Dragon MMA Idle";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 360;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.useFlipModelSwapchain = false;
            PlayerSettings.allowFullscreenSwitch = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
            PlayerSettings.runInBackground = true;
        }
    }
}
