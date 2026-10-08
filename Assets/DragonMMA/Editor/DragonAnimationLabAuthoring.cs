using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DragonMMA.EditorTools
{
    public static class DragonAnimationLabAuthoring
    {
        public const string ScenePath = "Assets/DragonMMA/Scenes/AnimationTestScene.unity";
        private const string ActorPath = "Assets/DragonMMA/Prefabs/Actors/";
        private static readonly Color Background = new Color(.055f, .075f, .105f);
        private static readonly Color Panel = new Color(.09f, .12f, .16f);
        private static Font Font => AssetDatabase.LoadAssetAtPath<Font>("Assets/DragonMMA/Fonts/NanumGothic-Regular.ttf");

        [MenuItem("Dragon MMA/Animation Lab/Create or Open Test Scene")]
        public static void CreateOrOpen()
        {
            RequireCleanEditor();
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); AnimationLabWorkbenchAuthoring.UpgradeScene(); return; }
            Require(Font != null, "Persistent UI font is missing.");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath + "Hunter.prefab") != null, "Hunter prefab missing.");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath + "Dragon.prefab") != null, "Dragon prefab missing.");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Animation Lab Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 5; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            var root = new GameObject("Animation Lab", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 5;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var bg = Image(root.transform, "Background", 0, 0, 1440, 900, Background);
            bg.rectTransform.anchorMax = Vector2.one; bg.rectTransform.sizeDelta = Vector2.zero;
            Text(root.transform, "Title", "DRAGON MMA / ANIMATION LAB", 40, 837, 1300, 40, 29);
            Text(root.transform, "Description", "본게임 공유 프리팹 · 클립 확인 / 전투 상호작용 / 수련 리듬   |   Play 모드에서 조작", 42, 803, 1320, 30, 17);
            var modes = Row(root.transform, "Modes", new[] { "클립 테스트", "전투 상호작용", "수련 리듬" }, 40, 756, 300, 40, 12);
            Text(root.transform, "Shared asset note", "공유 애니메이션을 수정하면 본게임에도 반영됩니다.", 984, 759, 410, 30, 16);
            Image(root.transform, "Preview Stage", 40, 397, 1360, 344, Panel);
            Text(root.transform, "Stage Caption", "PREVIEW   /   2× 확대   /   본게임 접촉 간격 유지", 60, 698, 1050, 26, 16);
            var status = Text(root.transform, "Playback Status", "Play를 눌러 테스트를 시작하세요.", 60, 560, 400, 115, 17);
            Image(root.transform, "Ground Guide", 70, 432, 1300, 2, new Color(.26f, .37f, .41f));
            var stage = Rect(root.transform, "Shared Actors (2x)", 440, 437, 600, 150); stage.localScale = new Vector3(2, 2, 1);
            var fighter = Actor(stage, "Hunter.prefab", "Fighter", new Vector2(100, 0));
            var dragon = Actor(stage, "Dragon.prefab", "Dragon", new Vector2(231, -3));
            Image(root.transform, "Controls Panel", 40, 20, 1360, 362, Panel);
            Text(root.transform, "Species Caption", "DRAGON", 60, 351, 120, 23, 14);
            var species = Row(root.transform, "Species", new[] { "Baby / 새끼", "Headbutt / 박치기", "Logtail / 통나무꼬리", "Stonehorn / 돌뿔", "Giant / 거대" }, 180, 343, 230, 32, 9);
            Text(root.transform, "Fighter Caption", "FIGHTER", 60, 311, 1100, 22, 14);
            var fighters = new Button[FighterCombatTiming.Poses.Length];
            for (int i = 0; i < fighters.Length; i++) fighters[i] = Button(root.transform, "Fighter " + FighterCombatTiming.Poses[i], FighterCombatTiming.Poses[i], 60 + i % 6 * 221, 267 - i / 6 * 40, 210, 34);
            Text(root.transform, "Dragon Caption", "DRAGON CLIPS", 60, 200, 1100, 22, 14);
            var dragons = Row(root.transform, "Dragon poses", DragonAnimationTiming.Poses, 60, 161, 210, 34, 11);
            var transport = new[]
            {
                Button(root.transform, "Play Pause", "일시정지", 60, 108, 110, 36),
                Button(root.transform, "Restart", "처음으로", 180, 108, 110, 36),
                Button(root.transform, "Previous Frame", "−1 프레임", 300, 108, 110, 36),
                Button(root.transform, "Next Frame", "+1 프레임", 420, 108, 110, 36),
                Button(root.transform, "Contact", "접촉 시점", 540, 108, 110, 36),
                Button(root.transform, "Loop", "반복 ON", 660, 108, 110, 36),
                Button(root.transform, "Skills", "기술: 기본", 1170, 108, 210, 36)
            };
            var speeds = Row(root.transform, "Speed", new[] { "0.25×", "0.5×", "1×", "2×" }, 790, 108, 83, 36, 8);
            var scrub = Slider(root.transform, 60, 62, 1320, 24);
            Text(root.transform, "Help", "타임라인 드래그 / 프레임 이동 / 접촉 시점은 일시정지합니다.  ·  프레임 단위: 1/60초  ·  게임 진행·세이브와 무관", 60, 28, 1320, 25, 15);
            root.AddComponent<DragonAnimationLab>().ConfigureEditor(fighter, dragon, canvas, status, scrub, modes, species, fighters, dragons, speeds, transport);
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Require(EditorSceneManager.SaveScene(scene, ScenePath), "Could not save test scene.");
            AnimationLabWorkbenchAuthoring.UpgradeScene();
            Selection.activeGameObject = root;
            ValidateScene();
            Debug.Log("[Animation Lab] Created " + ScenePath + ". Existing game assets and build scenes were not modified.");
        }
        private static void RequireCleanEditor()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Stop Play mode before opening the lab.");
            for (int i = 0; i < SceneManager.sceneCount; i++) Require(!SceneManager.GetSceneAt(i).isDirty, "An open scene has unsaved changes. Save or keep them before switching scenes.");
        }
        private static DragonActorView Actor(Transform parent, string prefab, string name, Vector2 position)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath + prefab), parent);
            go.name = name; var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.anchoredPosition = position; rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
            return go.GetComponent<DragonActorView>();
        }
        private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h); return rect;
        }
        private static Image Image(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        private static Text Text(Transform parent, string name, string content, float x, float y, float w, float h, int size)
        {
            var text = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Text>(); text.font = Font;
            text.text = content; text.fontSize = size; text.color = new Color(.86f, .91f, .94f); text.raycastTarget = false;
            text.alignment = TextAnchor.MiddleLeft; return text;
        }
        private static Button Button(Transform parent, string name, string label, float x, float y, float w, float h)
        {
            var image = Image(parent, name, x, y, w, h, new Color(.16f, .21f, .28f)); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.navigation = new Navigation { mode = Navigation.Mode.None };
            var text = Text(image.transform, "Label", label, 0, 0, w, h, 17); text.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        private static Button[] Row(Transform parent, string name, string[] labels, float x, float y, float w, float h, float gap)
        {
            var buttons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++) buttons[i] = Button(parent, name + " " + i, labels[i], x + i * (w + gap), y, w, h);
            return buttons;
        }
        private static Slider Slider(Transform parent, float x, float y, float w, float h)
        {
            var root = Rect(parent, "Timeline", x, y, w, h);
            Image(root, "Track", 0, 9, w, 6, new Color(.18f, .26f, .32f));
            var area = Rect(root, "Handle Area", 8, 0, w - 16, h);
            var handle = Image(area, "Handle", 0, 0, 16, h, new Color(.4f, .87f, .71f)); handle.raycastTarget = true;
            handle.rectTransform.pivot = new Vector2(.5f, .5f); handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0, .5f);
            var slider = root.gameObject.AddComponent<Slider>(); slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.minValue = 0; slider.maxValue = 1; slider.navigation = new Navigation { mode = Navigation.Mode.None };
            // The full timeline is a raycast target, not only its small thumb.
            var hitArea = root.gameObject.AddComponent<Image>(); hitArea.color = Color.clear; hitArea.raycastTarget = true;
            return slider;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("[Animation Lab] " + message); }
        private static DragonAnimationLab Lab()
        {
            Require(SceneManager.GetActiveScene().path == ScenePath, "Open AnimationTestScene first.");
            var lab = UnityEngine.Object.FindFirstObjectByType<DragonAnimationLab>(); Require(lab != null, "Lab controller missing."); return lab;
        }
        [MenuItem("Dragon MMA/Animation Lab/Validate Scene")]
        public static void ValidateScene()
        {
            var lab = Lab(); var scene = lab.gameObject.scene;
            var components = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).ToArray();
            Require(components.All(c => c != null), "Missing script found.");
            Require(!components.Any(c => c is DragonMmaGame || c is DragonMmaView || c is DesktopOverlayWindow), "Game/save/window component must not exist in lab.");
            Require(lab.Fighter != null && lab.Dragon != null, "Expected two shared baseline actors.");
            Require(components.OfType<AudioListener>().Count(l => l.enabled && l.gameObject.activeInHierarchy) == 1, "Expected one active AudioListener.");
            AnimationLabWorkbenchAuthoring.Validate(lab.AssetPreview);
            Require(components.OfType<EventSystem>().Count() == 1 && components.OfType<InputSystemUIInputModule>().Count() == 1, "Input System UI routing missing.");
            Require(!EditorBuildSettings.scenes.Any(s => s.path == ScenePath), "Development scene must not be in build settings.");
            foreach (var actor in new[] { lab.Fighter, lab.Dragon })
                Require(PrefabUtility.GetCorrespondingObjectFromSource(actor) != null, "Actor must remain connected to original prefab.");
            var serialized = new SerializedObject(lab);
            foreach (string name in new[] { "fighter", "dragon", "canvas", "status", "timeline", "playButton", "restartButton", "previousButton", "nextButton", "contactButton", "loopButton", "unlockButton" })
                Require(serialized.FindProperty(name).objectReferenceValue != null, "Unassigned reference: " + name);
            foreach (string name in new[] { "modeButtons", "speciesButtons", "fighterButtons", "dragonButtons", "speedButtons" })
            {
                var array = serialized.FindProperty(name);
                for (int i = 0; i < array.arraySize; i++) Require(array.GetArrayElementAtIndex(i).objectReferenceValue != null, "Unassigned button: " + name);
            }
            Debug.Log("[Animation Lab] Scene validation PASS: shared prefabs, authored controls, Input System, no save adapter, no build inclusion.");
        }
        [MenuItem("Dragon MMA/Animation Lab/Run Runtime Smoke Test")]
        public static void SmokeTest()
        {
            Require(EditorApplication.isPlaying, "Enter Play mode for runtime validation."); var lab = Lab();
            int samples = 0;
            foreach (string pose in FighterCombatTiming.Poses)
            {
                lab.SelectFighterPose(pose);
                for (int i = 0; i < 5; i++) { lab.Seek(i * FighterCombatTiming.Duration(pose) / 4); Require(lab.Fighter.Image.sprite != null && lab.Fighter.Image.sprite.name.StartsWith("fighter_" + pose + "_"), "Fighter pose did not sample: " + pose); samples++; }
            }
            for (int kind = 0; kind < 5; kind++)
            {
                lab.SetSpecies((DragonKind)kind);
                foreach (string pose in DragonAnimationTiming.Poses)
                {
                    lab.SelectDragonPose(pose);
                    for (int i = 0; i < 5; i++) { lab.Seek(i * DragonAnimationTiming.Duration((DragonKind)kind, pose) / 4); Require(lab.Dragon.Image.sprite != null && lab.Dragon.Image.sprite.name.StartsWith("dragon_" + kind + "_" + pose + "_"), "Dragon pose did not sample: " + kind + "/" + pose); samples++; }
                }
                lab.SetMode(DragonAnimationLab.PreviewMode.Combat);
                foreach (bool unlock in new[] { false, true })
                {
                    lab.SetAllSkills(unlock);
                    for (int i = 0; i < 180; i++) { lab.Seek(i / 60f * lab.Duration); Require(IsFinite(lab.Fighter.transform.localPosition) && IsFinite(lab.Dragon.transform.localPosition), "Non-finite choreography transform."); samples++; }
                }
                lab.SetMode(DragonAnimationLab.PreviewMode.Practice); lab.Seek(.6f + DragonAnimationTiming.Windup((DragonKind)kind) + .05f);
                Require(lab.Dragon.Image.sprite.name.Contains("_attack_"), "Practice did not reach attack.");
            }
            lab.SetMode(DragonAnimationLab.PreviewMode.Combat); lab.SetSpecies(DragonKind.Giant); lab.SetAllSkills(true);
            float thirdCycle = DragonCombatChoreography.CycleSeconds(DragonKind.Giant, 0) + DragonCombatChoreography.CycleSeconds(DragonKind.Giant, 1);
            lab.Seek(thirdCycle + DragonCombatChoreography.CounterStart(DragonKind.Giant, 2) + .12f);
            Require(lab.Fighter.Image.sprite.name.StartsWith("fighter_takedown_"), "Unlocked third-cycle takedown missing.");
            float before = lab.Elapsed; lab.Step(1); Require(Mathf.Abs(lab.Elapsed - before - 1f / 60) < .0001f, "Frame step lost cycle/time.");
            before = lab.Elapsed; lab.Advance(.2f); Require(lab.Elapsed == before, "Paused clock advanced.");
            lab.SetSpeed(.5f); lab.SetPlaying(true); lab.Advance(.2f); Require(Mathf.Abs(lab.Elapsed - before - .1f) < .0001f, "Speed multiplier failed.");
            lab.SetLoop(false); lab.Seek(lab.Duration - .01f); lab.SetPlaying(true); lab.Advance(1); Require(!lab.IsPlaying && lab.Elapsed == lab.Duration, "Non-loop preview did not stop.");
            lab.JumpToContact(); Require(lab.Elapsed < lab.Duration && lab.Dragon.Image.sprite.name.Contains("_attack_"), "Contact jump failed after non-loop end.");
            // Exercise the actual authored UI listeners, not just public control methods.
            var bindings = new SerializedObject(lab);
            ((Button)bindings.FindProperty("speciesButtons").GetArrayElementAtIndex(3).objectReferenceValue).onClick.Invoke(); Require(lab.Kind == DragonKind.Stonehorn, "Species button is disconnected.");
            ((Button)bindings.FindProperty("modeButtons").GetArrayElementAtIndex(0).objectReferenceValue).onClick.Invoke(); Require(lab.Mode == DragonAnimationLab.PreviewMode.Clips, "Mode button is disconnected.");
            ((Slider)bindings.FindProperty("timeline").objectReferenceValue).onValueChanged.Invoke(.5f); Require(Mathf.Abs(lab.Elapsed - lab.Duration * .5f) < .0001f && !lab.IsPlaying, "Scrub callback failed.");
            lab.SetLoop(true); lab.SetSpecies(DragonKind.Baby); lab.SetAllSkills(false); lab.SetMode(DragonAnimationLab.PreviewMode.Combat); lab.SetSpeed(1); lab.SetPlaying(false);
            Debug.Log($"[Animation Lab] Runtime smoke PASS: {samples} samples / 12 fighter poses / 30 dragon poses / combat+practice / pause, step, speed, loop, unlocks and UI callbacks.");
        }
        private static bool IsFinite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.y);
        [MenuItem("Dragon MMA/Animation Lab/Capture Preview")]
        public static void CapturePreview()
        {
            var lab = Lab(); var canvas = lab.GetComponent<Canvas>(); var camera = canvas.worldCamera;
            var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            var target = new RenderTexture(1440, 900, 24); var texture = new Texture2D(1440, 900, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); texture.Apply();
                Directory.CreateDirectory("Artifacts/AnimationLab"); string path = "Artifacts/AnimationLab/preview.png";
                File.WriteAllBytes(path, texture.EncodeToPNG()); Debug.Log("[Animation Lab] Capture: " + Path.GetFullPath(path));
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive; target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(texture); Canvas.ForceUpdateCanvases();
            }
        }
    }
}
