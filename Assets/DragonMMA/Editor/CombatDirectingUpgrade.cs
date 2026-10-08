using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DragonMMA.EditorTools
{
    /// <summary>
    /// Idempotent authoring entry point for the combat-directing upgrade.
    /// It deliberately edits only the two authored overlay prefabs and the
    /// presentation assets owned by Dragon MMA.
    /// </summary>
    public static class CombatDirectingUpgrade
    {
        private static readonly string[] OverlayPaths =
        {
            "Assets/DragonMMA/Prefabs/UI/OverlayRoot.prefab",
            "Assets/DragonMMA/Prefabs/UI/WebOverlayRoot.prefab"
        };

        public static void ReportLayout()
        {
            WriteLayoutReport("layout-before.txt");
        }

        [MenuItem("Dragon MMA/Combat Directing/02 Apply Feedback Assets")]
        public static void ApplyFeedbackAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before changing combat feedback assets.");

            const string audioPath = "Assets/DragonMMA/Audio/fighter_knockdown.wav";
            WriteKnockdownSound(audioPath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
            if (clip == null) throw new InvalidDataException("Unity could not import " + audioPath);

            foreach (string path in OverlayPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var data = new SerializedObject(root.GetComponent<DragonMmaView>());
                    data.FindProperty("knockdownSound").objectReferenceValue = clip;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Combat Directing] Distinct knockdown audio assigned to desktop and web overlays.");
        }

        [MenuItem("Dragon MMA/Combat Directing/01 Apply Battle Layout")]
        public static void ApplyBattleLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Stop Play Mode before changing the authored combat layout.");

            EnsureCombatVfxAssets();
            ConfigureLayout(OverlayPaths[0], false);
            ConfigureLayout(OverlayPaths[1], true);
            AssetDatabase.SaveAssets();
            WriteLayoutReport("layout-after.txt");
            Debug.Log("[Combat Directing] Perceptual-quality battle stage and layered feedback applied to desktop and web overlays.");
        }

        private static void ConfigureLayout(string path, bool web)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<DragonMmaView>();
                var bindings = root.GetComponent<DragonUiBindings>();
                var viewData = new SerializedObject(view);
                var fighter = (RectTransform)((DragonActorView)viewData.FindProperty("hunter").objectReferenceValue).transform;
                var dragon = (RectTransform)((DragonActorView)viewData.FindProperty("dragon").objectReferenceValue).transform;
                var hunt = bindings.Get<RectTransform>("Hunt");
                Sprite burst = AssetDatabase.LoadAssetAtPath<Sprite>(CombatAnimationPolishUpgrade.WeakPath) ??
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DragonMMA/UIArt/combat_impact_burst.png");
                Sprite heavy = AssetDatabase.LoadAssetAtPath<Sprite>(CombatAnimationPolishUpgrade.HeavyPath) ?? burst;
                Sprite ring = AssetDatabase.LoadAssetAtPath<Sprite>(CombatAnimationPolishUpgrade.GuardPath) ??
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DragonMMA/UIArt/combat_impact_ring.png");
                Sprite dust = AssetDatabase.LoadAssetAtPath<Sprite>(CombatAnimationPolishUpgrade.KnockdownPath) ??
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DragonMMA/UIArt/combat_impact_dust.png");
                Sprite streak = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/DragonMMA/UIArt/combat_speed_streak.png");

                float stageX, stageY, stageWidth, stageHeight;

                if (web)
                {
                    viewData.FindProperty("huntReferenceHeight").floatValue = 720;
                    Place(hunt, Vector2.zero, new Vector2(1920, 720));
                    Place(bindings.Get<RectTransform>("Hunt/Field journal"), new Vector2(24, 622), new Vector2(500, 74));
                    Place(bindings.Get<RectTransform>("Hunt/Battle status"), new Vector2(620, 622), new Vector2(680, 74));
                    Place(bindings.Get<RectTransform>("Hunt/Collection objective"), new Vector2(1394, 622), new Vector2(502, 74));
                    Place(bindings.Get<RectTransform>("Hunt/Battle input patch"), new Vector2(410, 174), new Vector2(1100, 430));
                    Place(bindings.Get<RectTransform>("Hunt/Hunter name plate"), new Vector2(600, 154), new Vector2(350, 44));
                    Place(bindings.Get<RectTransform>("Hunt/Dragon name plate"), new Vector2(980, 154), new Vector2(350, 44));
                    fighter.anchoredPosition = new Vector2(735, 210);
                    dragon.anchoredPosition = new Vector2(1050, 210);
                    fighter.localScale = Vector3.one * 1.68f;
                    dragon.localScale = Vector3.one * 1.86f;
                    stageX = 410; stageY = 174; stageWidth = 1100; stageHeight = 430;
                }
                else
                {
                    viewData.FindProperty("huntReferenceHeight").floatValue = 520;
                    Place(hunt, Vector2.zero, new Vector2(1920, 520));
                    Place(bindings.Get<RectTransform>("Hunt/Field journal"), new Vector2(24, 440), new Vector2(474, 64));
                    Place(bindings.Get<RectTransform>("Hunt/Battle status"), new Vector2(674, 440), new Vector2(620, 64));
                    Place(bindings.Get<RectTransform>("Hunt/Collection objective"), new Vector2(1430, 440), new Vector2(466, 64));
                    Place(bindings.Get<RectTransform>("Hunt/Battle input patch"), new Vector2(470, 104), new Vector2(980, 330));
                    Place(bindings.Get<RectTransform>("Hunt/Hunter name plate"), new Vector2(625, 68), new Vector2(310, 36));
                    Place(bindings.Get<RectTransform>("Hunt/Dragon name plate"), new Vector2(985, 68), new Vector2(310, 36));
                    fighter.anchoredPosition = new Vector2(715, 118);
                    dragon.anchoredPosition = new Vector2(1035, 118);
                    fighter.localScale = Vector3.one * 1.86f;
                    dragon.localScale = Vector3.one * 1.86f;
                    stageX = 470; stageY = 104; stageWidth = 980; stageHeight = 330;
                }

                var backdrop = EnsureImage(hunt, "Combat focus backdrop", new Vector2(stageX, stageY), new Vector2(stageWidth, stageHeight),
                    null, new Color(.025f, .075f, .085f, .72f));
                var frameTop = EnsureImage(hunt, "Combat frame top", new Vector2(stageX, stageY + stageHeight - 5), new Vector2(stageWidth, 5),
                    null, new Color(.30f, .86f, .82f, .82f));
                var frameBottom = EnsureImage(hunt, "Combat frame bottom", new Vector2(stageX, stageY), new Vector2(stageWidth, 3),
                    null, new Color(1f, .48f, .18f, .64f));
                var beatLabel = EnsureText(hunt, "Combat beat label", new Vector2(stageX + 20, stageY + stageHeight - 39),
                    new Vector2(stageWidth - 40, 30), web ? 22 : 19);
                beatLabel.text = "탐색 · 거리를 재라";
                var fighterShadow = EnsureImage(hunt, "Fighter shadow", new Vector2(fighter.anchoredPosition.x - (web ? 83 : 92), stageY + 7),
                    new Vector2(web ? 170 : 185, 52), dust, new Color(.02f, .025f, .02f, .58f));
                var dragonShadow = EnsureImage(hunt, "Dragon shadow", new Vector2(dragon.anchoredPosition.x - (web ? 74 : 82), stageY + 7),
                    new Vector2(web ? 150 : 165, 48), dust, new Color(.02f, .025f, .02f, .58f));
                fighterShadow.preserveAspect = false; dragonShadow.preserveAspect = false;

                int fighterIndex = fighter.GetSiblingIndex();
                backdrop.transform.SetSiblingIndex(fighterIndex);
                fighterShadow.transform.SetSiblingIndex(fighter.GetSiblingIndex());
                dragonShadow.transform.SetSiblingIndex(dragon.GetSiblingIndex());

                var impact = bindings.Get<Image>("Hunt/Contact sparks");
                impact.sprite = burst; impact.preserveAspect = true; impact.raycastTarget = false;
                Place(impact.rectTransform, new Vector2(stageX + stageWidth * .5f, stageY + stageHeight * .52f),
                    Vector2.one * (web ? 150 : 132));
                var impactRing = EnsureImage(hunt, "Impact ring", impact.rectTransform.anchoredPosition,
                    Vector2.one * (web ? 126 : 112), ring, Color.white);
                var impactEcho = EnsureImage(hunt, "Impact echo", impact.rectTransform.anchoredPosition,
                    Vector2.one * (web ? 142 : 126), heavy, new Color(1f, .38f, .16f, .65f));
                var impactDust = EnsureImage(hunt, "Impact dust", new Vector2(stageX + stageWidth * .5f, stageY + 15),
                    new Vector2(web ? 190 : 170, web ? 70 : 62), dust, new Color(.82f, .68f, .48f, .75f));
                impactRing.gameObject.SetActive(false); impactEcho.gameObject.SetActive(false); impactDust.gameObject.SetActive(false);

                var lines = new Image[5];
                float[] lineY = { .26f, .36f, .48f, .61f, .72f };
                float[] lineAngle = { 10, 5, 0, -5, -10 };
                for (int i = 0; i < lines.Length; i++)
                {
                    float lineWidth = (web ? 330 : 285) - i * 16;
                    lines[i] = EnsureImage(hunt, "Speed line " + i,
                        new Vector2(stageX + 44 + i * 16, stageY + stageHeight * lineY[i]), new Vector2(lineWidth, web ? 28 : 24),
                        streak, new Color(.55f, .90f, 1f, .5f));
                    lines[i].rectTransform.localRotation = Quaternion.Euler(0, 0, lineAngle[i]);
                    lines[i].preserveAspect = false;
                    lines[i].gameObject.SetActive(false);
                }

                AssignObjectArray(viewData.FindProperty("combatFocusObjects"), new UnityEngine.Object[]
                    { backdrop.gameObject, frameTop.gameObject, frameBottom.gameObject, beatLabel.gameObject, fighterShadow.gameObject, dragonShadow.gameObject });
                viewData.FindProperty("combatBeatLabel").objectReferenceValue = beatLabel;
                viewData.FindProperty("impactRing").objectReferenceValue = impactRing;
                viewData.FindProperty("impactEcho").objectReferenceValue = impactEcho;
                viewData.FindProperty("impactDust").objectReferenceValue = impactDust;
                viewData.FindProperty("weakImpactSprite").objectReferenceValue = burst;
                viewData.FindProperty("heavyImpactSprite").objectReferenceValue = heavy;
                viewData.FindProperty("guardImpactSprite").objectReferenceValue = ring;
                viewData.FindProperty("knockdownImpactSprite").objectReferenceValue = dust;
                AssignObjectArray(viewData.FindProperty("speedLines"), lines);
                viewData.ApplyModifiedPropertiesWithoutUndo();
                bindings.CaptureEditorBindings();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchoredPosition = position;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }

        private static Image EnsureImage(RectTransform parent, string name, Vector2 position, Vector2 size, Sprite sprite, Color color)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            if (existing == null) go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            Place(rect, position, size);
            var image = go.GetComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            image.preserveAspect = sprite != null;
            return image;
        }

        private static Text EnsureText(RectTransform parent, string name, Vector2 position, Vector2 size, int fontSize)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            if (existing == null) go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            Place(rect, position, size);
            var text = go.GetComponent<Text>();
            foreach (var sample in parent.GetComponentsInChildren<Text>(true))
                if (sample != text && sample.font != null) { text.font = sample.font; break; }
            text.fontSize = fontSize; text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.74f, .94f, 1f, 1);
            text.raycastTarget = false;
            return text;
        }

        private static void AssignObjectArray(SerializedProperty property, UnityEngine.Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void AssignObjectArray(SerializedProperty property, Image[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void EnsureCombatVfxAssets()
        {
            const string folder = "Assets/DragonMMA/UIArt";
            Directory.CreateDirectory(folder);
            WriteEffectTexture(folder + "/combat_impact_burst.png", EffectTexture.Burst);
            WriteEffectTexture(folder + "/combat_impact_ring.png", EffectTexture.Ring);
            WriteEffectTexture(folder + "/combat_impact_dust.png", EffectTexture.Dust);
            WriteEffectTexture(folder + "/combat_speed_streak.png", EffectTexture.Streak);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in new[]
            {
                folder + "/combat_impact_burst.png", folder + "/combat_impact_ring.png",
                folder + "/combat_impact_dust.png", folder + "/combat_speed_streak.png"
            })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spritePivot = new Vector2(.5f, .5f);
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        private enum EffectTexture { Burst, Ring, Dust, Streak }

        private static void WriteEffectTexture(string path, EffectTexture effect)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            if (effect == EffectTexture.Ring)
            {
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f));
                        float alpha = Mathf.Clamp01(1 - Mathf.Abs(d - 45f) / 3.5f);
                        if (alpha > 0) pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                    }
            }
            else if (effect == EffectTexture.Dust)
            {
                AddEllipse(pixels, size, 64, 50, 48, 15, 125);
                AddEllipse(pixels, size, 40, 60, 19, 20, 160);
                AddEllipse(pixels, size, 69, 67, 25, 26, 190);
                AddEllipse(pixels, size, 94, 59, 17, 18, 145);
            }
            else if (effect == EffectTexture.Streak)
            {
                for (int x = 4; x < 124; x++)
                {
                    float p = x / 124f;
                    int half = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sin(p * Mathf.PI) * 6));
                    byte alpha = (byte)(Mathf.Sin(p * Mathf.PI) * 235);
                    for (int y = 64 - half; y <= 64 + half; y++) pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            else
            {
                for (int ray = 0; ray < 12; ray++)
                {
                    float angle = ray * Mathf.PI * 2 / 12f + (ray % 2) * .10f;
                    int end = ray % 3 == 0 ? 61 : 50;
                    for (int radius = 5; radius < end; radius++)
                    {
                        int width = Mathf.Max(1, Mathf.RoundToInt((1 - radius / (float)end) * 4));
                        int cx = Mathf.RoundToInt(64 + Mathf.Cos(angle) * radius);
                        int cy = Mathf.RoundToInt(64 + Mathf.Sin(angle) * radius);
                        AddDisc(pixels, size, cx, cy, width, (byte)Mathf.Lerp(255, 70, radius / (float)end));
                    }
                }
                AddDisc(pixels, size, 64, 64, 13, 255);
                AddDisc(pixels, size, 64, 64, 22, 115);
            }
            texture.SetPixels32(pixels); texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void AddDisc(Color32[] pixels, int size, int cx, int cy, int radius, byte alpha)
        {
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (x * x + y * y <= radius * radius) Blend(pixels, size, cx + x, cy + y, alpha);
        }

        private static void AddEllipse(Color32[] pixels, int size, int cx, int cy, int rx, int ry, byte alpha)
        {
            for (int y = -ry; y <= ry; y++)
                for (int x = -rx; x <= rx; x++)
                {
                    float d = x * x / (float)(rx * rx) + y * y / (float)(ry * ry);
                    if (d <= 1) Blend(pixels, size, cx + x, cy + y, (byte)(alpha * (1 - d * .65f)));
                }
        }

        private static void Blend(Color32[] pixels, int size, int x, int y, byte alpha)
        {
            if (x < 0 || y < 0 || x >= size || y >= size) return;
            int index = y * size + x;
            if (alpha > pixels[index].a) pixels[index] = new Color32(255, 255, 255, alpha);
        }

        private static void WriteKnockdownSound(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            const int rate = 44100;
            const float duration = .16f;
            int count = Mathf.CeilToInt(rate * duration);
            var random = new System.Random(104729);
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                for (int i = 0; i < count; i++)
                {
                    float seconds = i / (float)rate, progress = i / (float)count;
                    double envelope = Math.Min(1, seconds / .003) * Math.Pow(1 - progress, 2.4);
                    double body = Math.Sin(2 * Math.PI * (62 - 28 * progress) * seconds);
                    double crack = (random.NextDouble() * 2 - 1) * Math.Pow(1 - progress, 5);
                    double sample = (body * .72 + crack * .28) * envelope * .82;
                    writer.Write((short)(Math.Clamp(sample, -1, 1) * short.MaxValue));
                }
            }
        }

        private static void WriteLayoutReport(string fileName)
        {
            var report = new StringBuilder();
            foreach (string path in OverlayPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var view = root.GetComponent<DragonMmaView>();
                    var bindings = root.GetComponent<DragonUiBindings>();
                    var viewData = new SerializedObject(view);
                    report.AppendLine(path);
                    report.AppendLine("  huntReferenceHeight=" + viewData.FindProperty("huntReferenceHeight").floatValue.ToString("0.###"));
                    Append(report, "field journal", bindings.Get<RectTransform>("Hunt/Field journal"));
                    Append(report, "battle status", bindings.Get<RectTransform>("Hunt/Battle status"));
                    Append(report, "collection objective", bindings.Get<RectTransform>("Hunt/Collection objective"));
                    Append(report, "bottom toolbar", bindings.Get<RectTransform>("Hunt/Bottom toolbar"));
                    Append(report, "battle input", bindings.Get<RectTransform>("Hunt/Battle input patch"));
                    Append(report, "fighter plate", bindings.Get<RectTransform>("Hunt/Hunter name plate"));
                    Append(report, "dragon plate", bindings.Get<RectTransform>("Hunt/Dragon name plate"));
                    Append(report, "impact", bindings.Get<RectTransform>("Hunt/Contact sparks"));
                    Append(report, "fighter", (RectTransform)((DragonActorView)viewData.FindProperty("hunter").objectReferenceValue).transform);
                    Append(report, "dragon", (RectTransform)((DragonActorView)viewData.FindProperty("dragon").objectReferenceValue).transform);
                    report.AppendLine();
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Directory.CreateDirectory("Artifacts/CombatDirecting-20261007");
            File.WriteAllText("Artifacts/CombatDirecting-20261007/" + fileName, report.ToString());
            Debug.Log(report.ToString());
        }

        private static void Append(StringBuilder report, string name, RectTransform rect)
        {
            report.AppendLine($"  {name}: anchor=({rect.anchorMin.x:0.###},{rect.anchorMin.y:0.###})-({rect.anchorMax.x:0.###},{rect.anchorMax.y:0.###}) " +
                $"position=({rect.anchoredPosition.x:0.###},{rect.anchoredPosition.y:0.###}) size=({rect.rect.width:0.###},{rect.rect.height:0.###}) " +
                $"scale=({rect.localScale.x:0.###},{rect.localScale.y:0.###}) pivot=({rect.pivot.x:0.###},{rect.pivot.y:0.###})");
        }
    }
}
