using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DragonMMA.EditorTools
{
    // Authors a separate web prefab once. Subsequent builds preserve Inspector edits.
    public static class DragonMmaWebGlBuild
    {
        public const string WebPrefab = "Assets/DragonMMA/Prefabs/UI/WebOverlayRoot.prefab";
        public const string WebScene = "Assets/DragonMMA/Scenes/WebGLScene.unity";

        public static void BuildWithLabelRepair()
        {
            AuthorWebScene();
            var root = PrefabUtility.LoadPrefabContents(WebPrefab);
            try { FitButtonLabels(root.transform); PrefabUtility.SaveAsPrefabAsset(root, WebPrefab); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Build();
        }

        [MenuItem("Dragon MMA/Build itch.io WebGL")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before building.");
            string output = Argument("-dragonWebOutput") ?? Path.GetFullPath("../webGL");
            AuthorWebScene();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.WebGL.template = "PROJECT:DragonItch";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.initialMemorySize = 64;
            PlayerSettings.WebGL.maximumMemorySize = 512;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            PlayerSettings.defaultWebScreenWidth = 960;
            PlayerSettings.defaultWebScreenHeight = 540;
            PlayerSettings.runInBackground = false;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            AssetDatabase.SaveAssets();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { WebScene }, locationPathName = output,
                target = BuildTarget.WebGL, options = BuildOptions.None
            });
            var summary = report.summary;
            string result = $"result={summary.result} bytes={summary.totalSize} errors={summary.totalErrors} warnings={summary.totalWarnings} output={Path.GetFullPath(output)}";
            Directory.CreateDirectory("Artifacts");
            File.WriteAllText("Artifacts/webgl-build-result.txt", result);
            if (summary.result != BuildResult.Succeeded) throw new InvalidOperationException("[Dragon Web Build] " + result);
            Directory.CreateDirectory(Path.Combine(output, "Licenses"));
            File.Copy("Assets/DragonMMA/Fonts/OFL.txt", Path.Combine(output, "Licenses/NanumGothic-OFL.txt"), true);
            Debug.Log("[Dragon Web Build] SUCCESS " + result);
        }

        public static void AuthorWebScene()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(WebPrefab) == null)
            {
                var root = PrefabUtility.LoadPrefabContents("Assets/DragonMMA/Prefabs/UI/OverlayRoot.prefab");
                try
                {
                    root.name = "WebOverlayRoot";
                    Layout(root.transform);
                    var view = new SerializedObject(root.GetComponent<DragonMmaView>());
                    view.FindProperty("huntReferenceHeight").floatValue = 420;
                    view.FindProperty("expandedReferenceHeight").floatValue = 1080;
                    view.FindProperty("cheerHintFormat").stringValue = "전투 영역 터치 / SPACE · 최대 +{1:0.##}%p";
                    view.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, WebPrefab);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WebScene) != null) return;
            var scene = EditorSceneManager.OpenScene("Assets/DragonMMA/Scenes/DesktopOverlayScene.unity");
            var game = Object.FindFirstObjectByType<DragonMmaGame>();
            var oldView = Object.FindFirstObjectByType<DragonMmaView>(FindObjectsInactive.Include);
            var web = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WebPrefab), scene);
            var serialized = new SerializedObject(game);
            serialized.FindProperty("view").objectReferenceValue = web.GetComponent<DragonMmaView>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Object.DestroyImmediate(oldView.gameObject);
            var nativeWindow = game.GetComponent<DesktopOverlayWindow>();
            if (nativeWindow != null) Object.DestroyImmediate(nativeWindow);
            Camera.main.backgroundColor = new Color32(23, 37, 43, 255);
            web.GetComponent<DragonMmaView>().PreviewEditorPage("storage");
            if (!EditorSceneManager.SaveScene(scene, WebScene)) throw new IOException("Could not save " + WebScene);
            AssetDatabase.SaveAssets();
        }

        private static void Layout(Transform root)
        {
            // Keep the existing serialized object graph and bindings; only author web overrides.
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                text.fontSize = Math.Max(30, text.fontSize);
                text.resizeTextForBestFit = true; text.resizeTextMinSize = 26; text.resizeTextMaxSize = 38;
                text.verticalOverflow = VerticalWrapMode.Overflow;
            }
            var hunt = At(root, "Hunt"); Rect(hunt, 0, 0, 1920, 420, true);
            foreach (Transform child in hunt)
                if (child.name != "Bottom toolbar" && child.name != "Field journal" && child.name != "Battle status" && child.name != "Collection objective" && child.name != "Notification")
                    ((RectTransform)child).anchoredPosition += new Vector2(0, 90);
            Rect(At(hunt, "Bottom toolbar"), 0, 0, 1920, 148);
            string[] buttons = { "Camp", "Training", "Market", "Skills", "Return", "Settings fixed tab" };
            for (int i = 0; i < buttons.Length; i++) Rect(At(hunt, "Bottom toolbar/" + buttons[i]), 14 + i * 316, 8, 300, 132);
            Hide(hunt, "Bottom toolbar/Exit", "Bottom toolbar/Input hint");
            Rect(At(hunt, "Bottom toolbar/Capacity"), 18, 148, 420, 48);
            Rect(At(hunt, "Bottom toolbar/Toolbar rule"), 0, 146, 1920, 2);
            Rect(At(hunt, "Field journal"), 24, 322, 500, 92);
            Label(hunt, "Field journal/Brand", 16, 46, 466, 42, 40);
            Label(hunt, "Field journal/Wallet", 16, 4, 466, 42, 34);
            Rect(At(hunt, "Battle status"), 630, 322, 680, 92);
            Label(hunt, "Battle status/Phase", 16, 48, 390, 42, 34);
            Label(hunt, "Battle status/Live odds", 420, 48, 244, 42, 40);
            Rect(At(hunt, "Battle status/Progress track0"), 16, 43, 648, 4);
            Label(hunt, "Battle status/Technique", 16, 2, 510, 38, 30);
            Rect(At(hunt, "Battle status/Progress track1"), 540, 10, 124, 16);
            Rect(At(hunt, "Collection objective"), 1370, 322, 526, 92);
            Label(hunt, "Collection objective/Chapter", 16, 46, 494, 42, 30);
            Label(hunt, "Collection objective/Next goal", 16, 4, 494, 42, 30);
            Rect(At(hunt, "Battle input patch"), 650, 150, 680, 170);
            Rect(At(hunt, "Notification"), 580, 272, 800, 48);
            Label(hunt, "Notification/Notification text", 12, 0, 776, 48, 32);
            foreach (string name in new[] { "Hunter name plate", "Dragon name plate" })
            {
                var plate = (RectTransform)At(hunt, name); plate.sizeDelta = new Vector2(280, 48);
                var text = plate.GetComponentInChildren<Text>(true); text.fontSize = 30; text.resizeTextMinSize = 26;
                var tr = text.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(4, 0); tr.offsetMax = new Vector2(-4, 0);
            }
            var baseUi = At(root, "BaseUI"); Rect(baseUi, 0, 422, 1888, 640, true);
            Label(baseUi, "Base title", 22, 542, 750, 70, 44);
            Hide(baseUi, "Live tag");
            for (int i = 0; i < 4; i++) Rect(At(baseUi, "Tab " + new[] { "storage", "training", "market", "skills" }[i]), 902 + i * 174, 510, 162, 132);
            Rect(At(baseUi, "Close base"), 1638, 510, 226, 132);
            Rect(At(baseUi, "Base divider"), 22, 502, 1842, 2);
            foreach (var page in baseUi.GetComponentsInChildren<DragonUiBindings>(true)) Rect(page.transform, 22, 12, 1842, 486);

            var storage = At(baseUi, "StoragePage");
            Label(storage, "Storage heading", 0, 422, 700, 60, 44); Hide(storage, "Storage limits");
            Rect(At(storage, "Previous page"), 710, 354, 132, 132);
            Label(storage, "Page", 848, 405, 138, 60, 32);
            Rect(At(storage, "Next page"), 992, 354, 132, 132);
            for (int i = 0; i < 10; i++)
            {
                var card = At(storage, "StorageCard" + i); Rect(card, i % 5 * 216, i < 5 ? 216 : 72, 208, 132);
                Rect(At(card, "Portrait"), 10, 66, 64, 48); Label(card, "Name", 8, 8, 192, 50, 34);
                Hide(card, "Individual state"); Rect(At(storage, "Roamer" + i), 28 + i * 98, 12, 64, 48);
            }
            Label(storage, "Empty state", 24, 150, 1000, 180, 44);
            var detail = At(storage, "Selected dragon"); Rect(detail, 1100, 70, 730, 410);
            Label(detail, "Detail name", 20, 330, 690, 70, 48);
            Label(detail, "Detail stats", 20, 216, 690, 108, 36);
            Rect(At(detail, "Assign training"), 20, 60, 335, 132); Rect(At(detail, "Register sale"), 365, 60, 335, 132);
            Hide(detail, "Display room locked");

            foreach (string name in new[] { "TrainingLockedPage", "TrainingEmptyPage", "TrainingActivePage" })
            {
                var page = At(baseUi, name); Rect(At(page, "Dojo scene"), 0, 80, 520, 380);
                Rect(At(page, "Dojo scene/Dojo"), 16, 65, 360, 270);
                Rect(At(page, "Dojo scene/Punching bag"), 405, 110, 72, 140);
                Label(page, "Training title", 560, 422, 1240, 64, 48);
                if (name == "TrainingLockedPage")
                {
                    Rect(At(page, "Dojo scene/Trainee"), 340, 120, 160, 120);
                    Label(page, "Dojo scene/Practice caption", 14, 4, 492, 64, 32);
                    Label(page, "Unlock description", 560, 225, 1240, 180, 38);
                    Rect(At(page, "Unlock training"), 560, 58, 680, 132);
                }
                else if (name == "TrainingEmptyPage")
                {
                    Label(page, "Dojo scene/Dojo caption", 14, 4, 492, 64, 32);
                    Label(page, "Training empty", 560, 225, 1240, 180, 38);
                    Rect(At(page, "Go to storage"), 560, 58, 680, 132);
                }
                else
                {
                    Rect(At(page, "Dojo scene/Trainee"), 340, 120, 160, 120);
                    Label(page, "Dojo scene/Practice caption", 14, 4, 492, 64, 32);
                    Label(page, "Partner", 560, 348, 1240, 64, 42);
                    Rect(At(page, "Progress track"), 560, 292, 1240, 24);
                    Label(page, "Core charge", 560, 222, 1240, 66, 36);
                    Rect(At(page, "Recall trainee"), 560, 64, 380, 132);
                    Rect(At(page, "Replace trainee"), 965, 64, 380, 132);
                    Label(page, "One-time rule", 1370, 64, 440, 132, 32);
                }
            }
            var market = At(baseUi, "MarketPage"); Label(market, "Market heading", 0, 422, 800, 64, 48); Hide(market, "Market note", "Trading note");
            for (int i = 0; i < 2; i++)
            {
                var slot = At(market, "Market slot " + i); Rect(slot, i * 930, 64, 900, 336);
                Label(slot, "Slot number", 22, 276, 800, 52, 32); Rect(At(slot, "Selling dragon"), 40, 150, 160, 120);
                Label(slot, "Listing name", 240, 234, 620, 70, 44); Label(slot, "Sale clock", 240, 168, 620, 64, 36);
                Rect(At(slot, "Claim sale " + i), 240, 12, 620, 132);
                Label(slot, "Empty listing", 40, 220, 800, 64, 44); Rect(At(slot, "Choose seller"), 40, 24, 800, 132);
            }
            var skills = At(baseUi, "SkillsPage"); Label(skills, "Collection heading", 0, 434, 1800, 50, 42);
            for (int i = 0; i < 5; i++)
            {
                var skill = At(skills, "Skill " + i); Rect(skill, i * 370, 18, 354, 410);
                Rect(At(skill, "Species stripe"), 0, 406, 354, 4); Rect(At(skill, "Skill dragon"), 22, 230, 106, 80);
                Label(skill, "Species", 18, 338, 320, 64, 40); Label(skill, "Power reward", 130, 224, 212, 84, 32);
                Label(skill, "Technique name", 18, 142, 320, 80, 36); Label(skill, "Unlock state", 18, 24, 320, 112, 34);
            }
            var settings = At(baseUi, "SettingsPage");
            Label(settings, "Settings title", 8, 414, 1700, 64, 48, "브라우저 설정");
            Label(settings, "Settings detail", 8, 190, 1800, 200, 38, "응원: 전투 영역 터치 / 클릭 또는 SPACE · 시설 버튼은 응원에 포함되지 않습니다.\n탭을 닫거나 화면을 숨기면 사냥은 멈춥니다. 등록된 판매·수련 타이머만 완료됩니다.\n진행 상황은 현재 기기·브라우저에 저장됩니다. 다른 기기와 자동 공유되지 않습니다.");
            Hide(settings, "Toggle topmost"); Rect(At(settings, "Save now"), 24, 50, 480, 132);
            Label(settings, "Save safety", 540, 50, 1250, 132, 36, "브라우저 데이터 삭제 · 시크릿 모드 사용 시 저장이 사라질 수 있습니다.\n종료 전 ‘지금 저장’을 눌러주세요.");
            var quit = At(baseUi, "QuitPage"); Label(quit, "Exit title", 24, 414, 1700, 64, 48);
            Label(quit, "Exit note", 24, 220, 1740, 150, 36, "진행 상황을 브라우저에 저장합니다. 종료하려면 브라우저 탭을 닫아주세요.");
            Rect(At(quit, "Confirm quit"), 24, 50, 650, 132); At(quit, "Confirm quit/Label").GetComponent<Text>().text = "저장하고 돌아가기";
            Rect(At(quit, "Cancel quit"), 700, 50, 650, 132);
            FitButtonLabels(root);
        }
        private static void FitButtonLabels(Transform root)
        {
            // The desktop seed included a completed showcase trainee on the locked page.
            At(root, "BaseUI/TrainingLockedPage/Dojo scene/Trainee").gameObject.SetActive(false);
            At(root, "BaseUI/TrainingLockedPage/Dojo scene/Practice caption").GetComponent<Text>().text = "아직 잠겨 있는 숲 수련방";
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                var label = button.transform.Find("Label"); if (label == null) continue;
                var rect = (RectTransform)label; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(12, 8); rect.offsetMax = new Vector2(-12, -8);
                var text = label.GetComponent<Text>(); text.alignment = TextAnchor.MiddleCenter;
                text.verticalOverflow = VerticalWrapMode.Truncate;
            }
            foreach (string name in new[] { "Hunter name plate", "Dragon name plate" })
            {
                var plate = (RectTransform)At(root, "Hunt/" + name); plate.sizeDelta = new Vector2(380, 48);
                plate.anchoredPosition = new Vector2(name == "Hunter name plate" ? 630 : 1030, 180);
                var text = plate.GetComponentInChildren<Text>(true); text.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }
        private static Transform At(Transform root, string path) => root.Find(path) ?? throw new InvalidOperationException(root.name + "/" + path + " is missing");
        private static void Hide(Transform root, params string[] paths) { foreach (var path in paths) At(root, path).gameObject.SetActive(false); }
        private static void Rect(Transform transform, float x, float y, float w, float h, bool centered = false)
        {
            var rect = (RectTransform)transform; rect.anchorMin = rect.anchorMax = rect.pivot = centered ? new Vector2(.5f, 0) : Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h);
        }
        private static void Label(Transform root, string path, float x, float y, float w, float h, int font, string value = null)
        {
            var transform = At(root, path); Rect(transform, x, y, w, h); var text = transform.GetComponent<Text>();
            text.fontSize = font; text.resizeTextMaxSize = font; text.resizeTextMinSize = Math.Min(font, 30);
            if (value != null) text.text = value;
        }
        private static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == key) return args[i + 1];
            return null;
        }
    }
}
