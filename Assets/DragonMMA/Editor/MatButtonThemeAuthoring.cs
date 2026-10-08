using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DragonMMA.EditorTools
{
    /// <summary>Updates existing button visuals, not game logic, pages, characters or bindings.</summary>
    public static class MatButtonThemeAuthoring
    {
        public const string SpritePath = "Assets/DragonMMA/UIArt/mat_button_plate.png";
        public const string DesktopPrefab = "Assets/DragonMMA/Prefabs/UI/OverlayRoot.prefab";
        public const string WebPrefab = "Assets/DragonMMA/Prefabs/UI/WebOverlayRoot.prefab";

        [MenuItem("Dragon MMA/UI/Apply Mat Button Theme")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before authoring button prefabs.");
            ImportPlate();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (sprite == null) throw new InvalidOperationException("Mat button sprite is missing.");
            ApplyToPrefab(DesktopPrefab, sprite, false);
            ApplyToPrefab(WebPrefab, sprite, true);
            AssetDatabase.SaveAssets();
        }

        private static void ImportPlate()
        {
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.spriteBorder = new Vector4(360, 140, 360, 140);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static void ApplyToPrefab(string path, Sprite sprite, bool web)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (!web) FitDesktopToolbar(root.transform);
                int count = 0;
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    // Selection cards retain their existing selection tint and layout.
                    if (button.name.StartsWith("StorageCard", StringComparison.Ordinal)) continue;
                    Style(button, sprite, web);
                    count++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[Mat Buttons] {path}: themed {count} buttons; callbacks and bindings preserved.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void FitDesktopToolbar(Transform root)
        {
            var bar = root.Find("Hunt/Bottom toolbar");
            string[] names = { "Camp", "Training", "Market", "Skills", "Return", "Settings fixed tab", "Exit" };
            float[] x = { 20, 202, 340, 478, 1462, 1684, 1802 };
            float[] widths = { 170, 126, 126, 144, 210, 106, 106 };
            for (int i = 0; i < names.Length; i++) SetRect(bar.Find(names[i]), x[i], 6, widths[i], 44);
            SetRect(bar.Find("Input hint"), 644, 8, 600, 38);
            SetRect(bar.Find("Capacity"), 1254, 8, 196, 38);
            string[] tabs = { "Tab storage", "Tab training", "Tab market", "Tab skills", "Close base" };
            foreach (string name in tabs)
            {
                var rect = (RectTransform)root.Find("BaseUI/" + name);
                if (rect == null) throw new InvalidOperationException("Missing button: " + name);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 298);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, 44);
            }
        }

        private static void SetRect(Transform transform, float x, float y, float width, float height)
        {
            if (transform == null) throw new InvalidOperationException("Toolbar binding was renamed; review layout before applying the theme.");
            var rect = (RectTransform)transform;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Style(Button button, Sprite sprite, bool web)
        {
            var root = (RectTransform)button.transform;
            var label = root.Find("Label")?.GetComponent<Text>();
            if (label == null) throw new InvalidOperationException("Missing direct button label: " + button.name);
            var hitbox = button.GetComponent<Image>();
            if (hitbox == null) throw new InvalidOperationException("Missing button raycast image: " + button.name);
            hitbox.color = Color.clear;
            hitbox.raycastTarget = true;
            hitbox.alphaHitTestMinimumThreshold = 0;

            var visual = root.Find("Mat visual") as RectTransform;
            if (visual == null)
            {
                var child = new GameObject("Mat visual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                visual = (RectTransform)child.transform;
                visual.SetParent(root, false);
            }
            visual.SetAsFirstSibling();
            visual.anchorMin = Vector2.zero; visual.anchorMax = Vector2.one;
            visual.pivot = new Vector2(.5f, .5f);
            visual.offsetMin = visual.offsetMax = Vector2.zero;
            visual.localScale = Vector3.one;
            var image = visual.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.color = Color.white;
            image.raycastTarget = false;
            image.preserveAspect = false;
            // Cap corner width on narrow touch buttons while preserving the taped edges.
            float width = Mathf.Max(1, root.sizeDelta.x), height = Mathf.Max(1, root.sizeDelta.y);
            image.pixelsPerUnitMultiplier = Mathf.Max(sprite.rect.height / height, (sprite.border.x + sprite.border.z) / (width * .44f));
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = new Color(.94f, .94f, .94f, 1);
            colors.highlightedColor = new Color(1, .98f, .91f, 1);
            colors.pressedColor = new Color(.72f, .72f, .72f, 1);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(.47f, .47f, .47f, .8f);
            colors.colorMultiplier = 1;
            colors.fadeDuration = .075f;
            button.colors = colors;

            var textRect = label.rectTransform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(.5f, .5f);
            float inset = Mathf.Min(height * .28f, width * .10f);
            textRect.offsetMin = new Vector2(inset, 3);
            textRect.offsetMax = new Vector2(-inset, -3);
            textRect.localScale = Vector3.one;
            label.alignment = TextAnchor.MiddleCenter;
            label.fontStyle = FontStyle.Bold;
            label.fontSize = web ? 30 : 18;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = web && height >= 132 ? 22 : 12;
            label.resizeTextMaxSize = label.fontSize;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.color = new Color(.96f, .93f, .84f, 1);
            label.raycastTarget = false;
            var shadow = label.GetComponent<Shadow>();
            if (shadow == null) shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.04f, .035f, .03f, .9f);
            shadow.effectDistance = web ? new Vector2(1, -2) : new Vector2(0, -1);
            shadow.useGraphicAlpha = true;
            var feedback = button.GetComponent<MatButtonPressFeedback>();
            if (feedback == null) feedback = button.gameObject.AddComponent<MatButtonPressFeedback>();
            feedback.ConfigureEditor(visual, label, Mathf.Clamp(height * .06f, 2, 6));
        }
    }
}
