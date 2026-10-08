using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DragonMMA.EditorTools
{
    public static class AnimationLabWorkbenchAuthoring
    {
        private static Font Font => AssetDatabase.LoadAssetAtPath<Font>("Assets/DragonMMA/Fonts/NanumGothic-Regular.ttf");
        private static DragonAnimationLab Lab()
        {
            if (SceneManager.GetActiveScene().path != DragonAnimationLabAuthoring.ScenePath) throw new InvalidOperationException("Open AnimationTestScene first.");
            return UnityEngine.Object.FindFirstObjectByType<DragonAnimationLab>();
        }

        [MenuItem("Dragon MMA/Animation Lab/Upgrade Workbench")]
        public static void UpgradeScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before changing the workbench.");
            var lab = Lab(); if (lab == null) throw new InvalidOperationException("Animation lab controller missing.");
            if (lab.gameObject.scene.isDirty) throw new InvalidOperationException("Save your AnimationTestScene edits before upgrading the workbench.");
            var canvas = lab.GetComponent<Canvas>();
            var camera = canvas.worldCamera;
            bool addedListener = camera.GetComponent<AudioListener>() == null;
            if (addedListener) Undo.AddComponent<AudioListener>(camera.gameObject);
            if (lab.AssetPreview != null)
            {
                bool changed = addedListener;
                var existing = new SerializedObject(lab);
                if (existing.FindProperty("stageCaption").objectReferenceValue == null)
                {
                    var existingModes = existing.FindProperty("modeButtons");
                    lab.ConfigureAssetPreviewEditor(lab.AssetPreview, Enumerable.Range(0, existingModes.arraySize)
                        .Select(i => (Button)existingModes.GetArrayElementAtIndex(i).objectReferenceValue).ToArray());
                    changed = true;
                }
                if (PreviewWindowMissing(lab.AssetPreview))
                {
                    var window = Rect(lab.transform, "Asset Preview Window", 40, 397, 1360, 344);
                    window.gameObject.AddComponent<RectMask2D>();
                    var stageRect = (RectTransform)lab.AssetPreview.StageEditor;
                    Undo.SetTransformParent(stageRect, window, "Mask Preview Stage");
                    stageRect.anchoredPosition = new Vector2(860, 40);
                    changed = true;
                }
                foreach (var slider in lab.GetComponentsInChildren<Slider>(true))
                {
                    if (slider.handleRect == null || Mathf.Approximately(slider.handleRect.sizeDelta.y, 0)) continue;
                    Undo.RecordObject(slider.handleRect, "Fix Preview Slider Handle");
                    slider.handleRect.sizeDelta = new Vector2(slider.handleRect.sizeDelta.x, 0);
                    changed = true;
                }
                if (changed) EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);
                if (lab.gameObject.scene.isDirty) EditorSceneManager.SaveScene(lab.gameObject.scene);
                DragonAnimationLabAuthoring.ValidateScene(); return;
            }
            Undo.RegisterFullObjectHierarchyUndo(lab.gameObject, "Upgrade Animation Workbench");
            var root = lab.transform;
            var combatControls = Rect(root, "Combat Controls", 0, 0, 1440, 900);
            var bindings = new SerializedObject(lab);
            foreach (string property in new[] { "speciesButtons", "fighterButtons", "dragonButtons" })
            {
                var buttons = bindings.FindProperty(property);
                for (int i = 0; i < buttons.arraySize; i++) ((Button)buttons.GetArrayElementAtIndex(i).objectReferenceValue).transform.SetParent(combatControls, false);
            }
            foreach (string name in new[] { "Species Caption", "Fighter Caption", "Dragon Caption" }) root.Find(name).SetParent(combatControls, false);
            var modes = new Button[4]; var oldModes = bindings.FindProperty("modeButtons");
            for (int i = 0; i < 3; i++)
            {
                modes[i] = (Button)oldModes.GetArrayElementAtIndex(i).objectReferenceValue;
                var rect = (RectTransform)modes[i].transform; rect.anchoredPosition = new Vector2(40 + i * 232, 756); rect.sizeDelta = new Vector2(220, 40);
                ((RectTransform)modes[i].GetComponentInChildren<Text>().transform).sizeDelta = rect.sizeDelta;
            }
            modes[3] = Button(root, "Asset Preview Mode", "자유 애니메이션", 736, 756, 220, 40);
            root.Find("Description").GetComponent<Text>().text = "클립 / 전투 / 수련 / 자유 애니메이션   |   캐릭터·환경 프리팹 등록과 조정   |   Play 모드에서 조작";
            root.Find("Help").GetComponent<Text>().text = "타임라인 드래그·프레임 이동은 일시정지 · 자유 모드: 클립 fps 단위 · 등록/기본 배치는 Animation Lab Inspector";
            var previewWindow = Rect(root, "Asset Preview Window", 40, 397, 1360, 344);
            previewWindow.gameObject.AddComponent<RectMask2D>();
            var previewStage = Rect(previewWindow, "Asset Preview Stage", 860, 40, 1, 1);
            var controls = Rect(root, "Asset Preview Controls", 0, 0, 1440, 900);
            Text(controls, "Target Caption", "대상", 60, 343, 110, 32);
            var targets = Dropdown(controls, "Preview Targets", 180, 343, 390, 34);
            Text(controls, "Clip Caption", "애니메이션", 590, 343, 120, 32);
            var clips = Dropdown(controls, "Preview Clips", 710, 343, 650, 34);
            Text(controls, "Zoom Caption", "배율", 60, 290, 110, 30);
            var zoom = Slider(controls, "Preview Scale", 180, 290, 390, .25f, 3f, 1);
            Text(controls, "X Caption", "좌우 위치", 60, 245, 110, 30);
            var x = Slider(controls, "Preview X", 180, 245, 390, -260, 260, 0);
            Text(controls, "Y Caption", "높이", 60, 200, 110, 30);
            var y = Slider(controls, "Preview Y", 180, 200, 390, -80, 160, 0);
            var mirror = Toggle(controls, "Preview Mirror", "좌우 반전", 650, 292, 260, 34);
            var reset = Button(controls, "Preview Reset", "기본 배치로", 970, 292, 240, 34);
            var readout = Text(controls, "Adjustment Readout", "", 650, 243, 680, 36);
            Text(controls, "Registration Hint", "새 대상: Inspector에서 UI 프리팹 또는 Controller/Clip 등록\n공유 클립 편집은 본게임에 반영 · 배치 슬라이더는 프리뷰 전용", 650, 170, 680, 64);
            var preview = Undo.AddComponent<AnimationLabAssetPreview>(lab.gameObject);
            preview.ConfigureEditor(previewStage.gameObject, controls.gameObject, combatControls.gameObject, targets, clips, zoom, x, y, mirror, reset, readout);
            lab.ConfigureAssetPreviewEditor(preview, modes);

            var production = AssetDatabase.LoadAssetAtPath<GameObject>(LeftOakAnimationAuthoring.DesktopPrefabPath);
            var oak = production.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Left oak");
            var placement = Rect(previewStage, "Left oak Placement", 0, 0, 1, 1);
            var tree = UnityEngine.Object.Instantiate(oak.gameObject, placement, false); tree.name = "Left oak";
            var treeRect = (RectTransform)tree.transform;
            treeRect.anchorMin = treeRect.anchorMax = treeRect.pivot = new Vector2(.5f, 0);
            treeRect.anchoredPosition = Vector2.zero; treeRect.sizeDelta = new Vector2(170, 272);
            treeRect.localScale = Vector3.one; treeRect.localRotation = Quaternion.identity;
            tree.GetComponent<Image>().raycastTarget = false;
            preview.AddSubjectEditor(new AnimationLabAssetPreview.Subject { label = "Left oak", placement = placement, animationTarget = tree,
                clips = Clips(tree.GetComponent<Animator>().runtimeAnimatorController) });
            preview.SetVisible(false);
            foreach (var slider in lab.GetComponentsInChildren<Slider>(true))
                if (slider.handleRect != null) slider.handleRect.sizeDelta = new Vector2(slider.handleRect.sizeDelta.x, 0);
            EditorUtility.SetDirty(lab); EditorUtility.SetDirty(preview);
            EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);
            if (!EditorSceneManager.SaveScene(lab.gameObject.scene)) throw new InvalidOperationException("Could not save AnimationTestScene.");
            DragonAnimationLabAuthoring.ValidateScene();
            Selection.activeGameObject = lab.gameObject;
            Debug.Log("[Animation Lab] Workbench upgraded: extensible UI asset/clip previews, placement controls, shared transport and one AudioListener.");
        }

        public static void RegisterPrefab(AnimationLabAssetPreview preview, GameObject prefab, RuntimeAnimatorController controller, AnimationClip extraClip, string label)
        {
            RequireEdit(preview);
            if (prefab == null || !PrefabUtility.IsPartOfPrefabAsset(prefab) || prefab.GetComponent<RectTransform>() == null)
                throw new InvalidOperationException("Select a UI visual prefab with a RectTransform root.");
            if (prefab.GetComponentsInChildren<Component>(true).Any(c => c is DragonMmaGame || c is DragonMmaView || c is DesktopOverlayWindow))
                throw new InvalidOperationException("Register an actor visual prefab, not a game/save/window root.");
            var placement = Rect(preview.StageEditor, prefab.name + " Placement", 0, 0, 1, 1);
            Undo.RegisterCreatedObjectUndo(placement.gameObject, "Register Preview Prefab");
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, placement);
                var rect = (RectTransform)instance.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0);
                rect.anchoredPosition = Vector2.zero; rect.localScale = Vector3.one;
                foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true)) if (!(behaviour is UIBehaviour)) behaviour.enabled = false;
                var animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null) throw new InvalidOperationException("Prefab needs an Animator on the animation binding root.");
                if (controller != null) animator.runtimeAnimatorController = controller;
                var clips = Clips(animator.runtimeAnimatorController);
                if (extraClip != null) clips = clips.Concat(new[] { extraClip }).Distinct().OrderBy(c => c.name).ToArray();
                if (clips.Length == 0) throw new InvalidOperationException("Assign a controller or a clip to preview.");
                ValidateClipBindings(animator.gameObject, clips);
                Undo.RecordObject(preview, "Register Preview Prefab");
                preview.AddSubjectEditor(new AnimationLabAssetPreview.Subject { label = string.IsNullOrWhiteSpace(label) ? prefab.name : label,
                    placement = placement, animationTarget = animator.gameObject, clips = clips });
                EditorUtility.SetDirty(preview); EditorSceneManager.MarkSceneDirty(preview.gameObject.scene);
            }
            catch { Undo.DestroyObjectImmediate(placement.gameObject); throw; }
        }
        public static void RegisterClip(AnimationLabAssetPreview preview, RuntimeAnimatorController controller, AnimationClip clip, string label)
        {
            RequireEdit(preview);
            var clips = controller == null ? Array.Empty<AnimationClip>() : Clips(controller);
            if (clip != null) clips = clips.Concat(new[] { clip }).Distinct().OrderBy(c => c.name).ToArray();
            if (clips.Length == 0) throw new InvalidOperationException("Select an Animator Controller or AnimationClip.");
            if (clips.Any(c => AnimationUtility.GetObjectReferenceCurveBindings(c).Concat(AnimationUtility.GetCurveBindings(c)).Any(b => b.path.Length != 0)))
                throw new InvalidOperationException("Clips with child paths need their matching UI prefab. Use prefab registration.");
            if (clips.Any(c => AnimationUtility.GetObjectReferenceCurveBindings(c).Concat(AnimationUtility.GetCurveBindings(c))
                .Any(b => b.type != typeof(Image) && b.type != typeof(RectTransform) && b.type != typeof(Transform) && b.type != typeof(Animator) && b.type != typeof(GameObject))))
                throw new InvalidOperationException("These clips require components beyond a UI Image. Use the matching UI prefab.");
            Sprite firstSprite = clips.SelectMany(c => AnimationUtility.GetObjectReferenceCurveBindings(c)
                .Where(b => b.type == typeof(Image) && b.propertyName == "m_Sprite")
                .SelectMany(b => AnimationUtility.GetObjectReferenceCurve(c, b))).Select(k => k.value as Sprite).FirstOrDefault(s => s != null);
            if (firstSprite == null) throw new InvalidOperationException("A standalone preview needs an Image sprite clip. Use the matching UI prefab for transform-only clips.");
            var placement = Rect(preview.StageEditor, "Clip Placement", 0, 0, 1, 1);
            Undo.RegisterCreatedObjectUndo(placement.gameObject, "Register Preview Clip");
            var image = Image(placement, "Visual", 0, 0, 220, 220);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = image.rectTransform.pivot = new Vector2(.5f, 0);
            image.preserveAspect = true;
            image.color = Color.white; image.sprite = firstSprite;
            var animator = image.gameObject.AddComponent<Animator>(); animator.runtimeAnimatorController = controller;
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clips[0]))
                if (binding.type == typeof(Image) && binding.propertyName == "m_Sprite") { var keys = AnimationUtility.GetObjectReferenceCurve(clips[0], binding); if (keys.Length > 0) image.sprite = keys[0].value as Sprite; }
            Undo.RecordObject(preview, "Register Preview Clip");
            preview.AddSubjectEditor(new AnimationLabAssetPreview.Subject { label = string.IsNullOrWhiteSpace(label) ? clips[0].name : label,
                placement = placement, animationTarget = image.gameObject, clips = clips });
            EditorUtility.SetDirty(preview); EditorSceneManager.MarkSceneDirty(preview.gameObject.scene);
        }
        private static void RequireEdit(AnimationLabAssetPreview preview)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || preview == null || preview.gameObject.scene.path != DragonAnimationLabAuthoring.ScenePath)
                throw new InvalidOperationException("Register assets in AnimationTestScene while Play mode is stopped.");
        }
        private static AnimationClip[] Clips(RuntimeAnimatorController controller) => controller == null ? Array.Empty<AnimationClip>() : controller.animationClips.Distinct().OrderBy(c => c.name).ToArray();
        private static bool PreviewWindowMissing(AnimationLabAssetPreview preview) => preview.StageEditor.parent.name != "Asset Preview Window";
        private static void ValidateClipBindings(GameObject target, AnimationClip[] clips)
        {
            foreach (var clip in clips)
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip).Concat(AnimationUtility.GetCurveBindings(clip)))
                {
                    var node = string.IsNullOrEmpty(binding.path) ? target.transform : target.transform.Find(binding.path);
                    if (node == null || (binding.type != typeof(GameObject) && node.GetComponent(binding.type) == null))
                        throw new InvalidOperationException($"Clip '{clip.name}' needs '{binding.path}' / {binding.type.Name}. Use its matching UI prefab or correct the animation target.");
                }
        }
        public static void AddControllerClips(AnimationLabAssetPreview preview)
        {
            RequireEdit(preview);
            Validate(preview);
            var updates = preview.Subjects.Select(s =>
            {
                var animator = s.animationTarget == null ? null : s.animationTarget.GetComponent<Animator>();
                var clips = (s.clips ?? Array.Empty<AnimationClip>()).Concat(Clips(animator == null ? null : animator.runtimeAnimatorController))
                    .Where(c => c != null).Distinct().OrderBy(c => c.name).ToArray();
                ValidateClipBindings(s.animationTarget, clips); return clips;
            }).ToArray();
            Undo.RecordObject(preview, "Add Controller Preview Clips");
            for (int i = 0; i < updates.Length; i++) preview.Subjects[i].clips = updates[i];
            EditorUtility.SetDirty(preview); EditorSceneManager.MarkSceneDirty(preview.gameObject.scene);
        }

        public static void Validate(AnimationLabAssetPreview preview)
        {
            if (preview == null || preview.Subjects.Length == 0) throw new InvalidOperationException("Preview subjects missing.");
            var data = new SerializedObject(preview);
            foreach (string property in new[] { "stage", "controls", "combatControls", "subjectDropdown", "clipDropdown", "scaleSlider", "xSlider", "ySlider", "flipToggle", "resetButton", "adjustmentLabel" })
                if (data.FindProperty(property).objectReferenceValue == null) throw new InvalidOperationException("Unassigned preview reference: " + property);
            foreach (var subject in preview.Subjects)
            {
                if (subject.placement == null || subject.animationTarget == null || !subject.animationTarget.transform.IsChildOf(subject.placement) ||
                    !subject.placement.IsChildOf(preview.StageEditor) || subject.clips == null || subject.clips.Length == 0 || subject.clips.Any(c => c == null))
                    throw new InvalidOperationException("Invalid preview subject: " + subject.label);
                if (subject.animationTarget.GetComponentsInChildren<Image>(true).Length == 0) throw new InvalidOperationException("Expected a UI visual target: " + subject.label);
                ValidateClipBindings(subject.animationTarget, subject.clips);
            }
            if (preview.Subjects.Select(s => s.placement).Distinct().Count() != preview.Subjects.Length ||
                preview.Subjects.Select(s => s.animationTarget).Distinct().Count() != preview.Subjects.Length)
                throw new InvalidOperationException("Each subject needs its own placement and animation target. Use the registration button to add another subject.");
        }

        [MenuItem("Dragon MMA/Animation Lab/Run Workbench Smoke Test")]
        public static void SmokeTest()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode before runtime validation.");
            var lab = Lab(); var preview = lab.AssetPreview; Validate(preview);
            lab.SetMode(DragonAnimationLab.PreviewMode.Assets); lab.SetLoop(true); preview.SelectSubject(0); preview.SelectClip(0);
            var sprites = new System.Collections.Generic.HashSet<Sprite>();
            var image = preview.SelectedTarget.GetComponent<Image>();
            for (int i = 0; i < 9; i++) { lab.Seek(i / preview.FrameRate); sprites.Add(image.sprite); }
            if (sprites.Count != 5) throw new InvalidOperationException("Left oak did not expose all five frames.");
            lab.Seek(0); lab.Step(1);
            if (Mathf.Abs(lab.Elapsed - 1f / preview.FrameRate) > .0001f || image.sprite.name != "tree_sway_1") throw new InvalidOperationException("Clip-rate frame stepping failed.");
            float before = lab.Elapsed; lab.Advance(.2f); if (lab.Elapsed != before) throw new InvalidOperationException("Paused preview clock advanced.");
            lab.SetSpeed(.5f); lab.SetPlaying(true); lab.Advance(.2f);
            if (Mathf.Abs(lab.Elapsed - before - .1f) > .0001f) throw new InvalidOperationException("Preview playback speed failed.");
            lab.SetLoop(false); lab.Seek(lab.Duration - .01f); lab.SetPlaying(true); lab.Advance(1);
            if (lab.IsPlaying || lab.Elapsed != lab.Duration) throw new InvalidOperationException("One-shot preview did not stop.");
            preview.SetScale(1.5f); preview.SetX(25); preview.SetY(10); preview.SetFlip(true); lab.Seek(.5f);
            var placement = preview.Subjects[0].placement;
            if (placement.anchoredPosition != new Vector2(25, 10) || placement.localScale != new Vector3(-1.5f, 1.5f, 1)) throw new InvalidOperationException("Clip overwrote placement adjustments.");
            var ui = new SerializedObject(preview);
            ((Slider)ui.FindProperty("scaleSlider").objectReferenceValue).onValueChanged.Invoke(2);
            if (placement.localScale.x != -2) throw new InvalidOperationException("Adjustment slider callback disconnected.");
            ((Button)ui.FindProperty("resetButton").objectReferenceValue).onClick.Invoke();
            if (placement.localScale != Vector3.one || placement.anchoredPosition != Vector2.zero) throw new InvalidOperationException("Preview reset failed.");
            lab.SetLoop(true); lab.SetSpeed(1); lab.SetPlaying(false); lab.Seek(0);
            var bindings = new SerializedObject(lab);
            ((Button)bindings.FindProperty("modeButtons").GetArrayElementAtIndex(1).objectReferenceValue).onClick.Invoke();
            if (lab.Mode != DragonAnimationLab.PreviewMode.Combat || !lab.Fighter.gameObject.activeInHierarchy) throw new InvalidOperationException("Combat mode did not restore actors.");
            ((Button)bindings.FindProperty("modeButtons").GetArrayElementAtIndex(3).objectReferenceValue).onClick.Invoke();
            if (lab.Mode != DragonAnimationLab.PreviewMode.Assets || lab.Fighter.gameObject.activeInHierarchy) throw new InvalidOperationException("Asset mode UI callback failed.");
            lab.Seek(0);
            Debug.Log("[Animation Lab] Workbench smoke PASS: five oak frames, clip-fps step, pause/speed/one-shot, placement/flip/reset, UI callbacks and mode switching.");
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
        private static Image Image(Transform parent, string name, float x, float y, float width, float height)
        {
            var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>(); image.color = new Color(.16f, .21f, .28f); image.raycastTarget = false; return image;
        }
        private static Text Text(Transform parent, string name, string value, float x, float y, float width, float height)
        {
            var text = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Text>(); text.font = Font; text.fontSize = 17;
            text.color = new Color(.86f, .91f, .94f); text.text = value; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false; return text;
        }
        private static Button Button(Transform parent, string name, string label, float x, float y, float width, float height)
        {
            var image = Image(parent, name, x, y, width, height); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.navigation = new Navigation { mode = Navigation.Mode.None };
            Text(image.transform, "Label", label, 0, 0, width, height).alignment = TextAnchor.MiddleCenter; return button;
        }
        private static Slider Slider(Transform parent, string name, float x, float y, float width, float min, float max, float value)
        {
            var root = Rect(parent, name, x, y, width, 30);
            var hit = root.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            Image(root, "Track", 0, 12, width, 6);
            var area = Rect(root, "Handle Area", 8, 0, width - 16, 30);
            var handle = Image(area, "Handle", 0, 0, 16, 30); handle.color = new Color(.4f, .87f, .71f); handle.raycastTarget = true;
            handle.rectTransform.pivot = new Vector2(.5f, .5f);
            handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0, .5f);
            handle.rectTransform.sizeDelta = new Vector2(16, 0);
            var slider = root.gameObject.AddComponent<Slider>(); slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.minValue = min; slider.maxValue = max; slider.SetValueWithoutNotify(value); return slider;
        }
        private static Toggle Toggle(Transform parent, string name, string label, float x, float y, float width, float height)
        {
            var root = Image(parent, name, x, y, width, height); root.raycastTarget = true;
            var check = Image(root.transform, "Checkmark", 6, 6, 22, 22); check.color = new Color(.4f, .87f, .71f);
            var toggle = root.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = root; toggle.graphic = check; toggle.isOn = false;
            Text(root.transform, "Label", label, 40, 0, width - 40, height); return toggle;
        }
        private static Dropdown Dropdown(Transform parent, string name, float x, float y, float width, float height)
        {
            var root = Image(parent, name, x, y, width, height); root.raycastTarget = true;
            var caption = Text(root.transform, "Label", "", 12, 0, width - 48, height);
            Text(root.transform, "Arrow", "▼", width - 32, 0, 28, height);
            var template = Image(root.transform, "Template", 0, -220, width, 220);
            var viewport = Rect(template.transform, "Viewport", 0, 0, width, 220); viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect(viewport, "Content", 0, 0, width, 40);
            var item = Image(content, "Item", 0, 0, width, 40); item.raycastTarget = true;
            var check = Image(item.transform, "Checkmark", 8, 8, 20, 24); check.color = new Color(.4f, .87f, .71f);
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = item; toggle.graphic = check;
            var itemLabel = Text(item.transform, "Item Label", "", 36, 0, width - 40, 40);
            foreach (var rect in new[] { viewport, content, item.rectTransform }) { rect.pivot = new Vector2(0, 1); rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.anchoredPosition = Vector2.zero; }
            var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var dropdown = root.gameObject.AddComponent<Dropdown>(); dropdown.targetGraphic = root; dropdown.captionText = caption; dropdown.itemText = itemLabel; dropdown.template = template.rectTransform;
            template.gameObject.SetActive(false); return dropdown;
        }
    }

    [CustomEditor(typeof(AnimationLabAssetPreview))]
    public sealed class AnimationLabAssetPreviewInspector : UnityEditor.Editor
    {
        private GameObject prefab;
        private RuntimeAnimatorController controller;
        private AnimationClip clip;
        private string label;
        private bool showWiring;
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("UI 캐릭터/환경 애니메이션 작업용입니다. 공유 클립·프리팹은 본게임에서도 사용합니다. 대상별 scale/offset/flip은 이 씬의 기본 배치입니다.", MessageType.Info);
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("subjects"), new GUIContent("프리뷰 대상 / 기본 배치"), true);
            showWiring = EditorGUILayout.Foldout(showWiring, "씬 UI 연결", true);
            if (showWiring)
                foreach (string property in new[] { "stage", "controls", "combatControls", "subjectDropdown", "clipDropdown", "scaleSlider", "xSlider", "ySlider", "flipToggle", "resetButton", "adjustmentLabel" })
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(property));
            bool modified = serializedObject.ApplyModifiedProperties();
            if (modified && !EditorApplication.isPlaying)
            {
                var preview = (AnimationLabAssetPreview)target;
                Undo.RecordObjects(preview.Subjects.Where(s => s.placement != null).Select(s => (UnityEngine.Object)s.placement).ToArray(), "Adjust Preview Layout");
                preview.ApplyAuthoredLayoutEditor(); EditorSceneManager.MarkSceneDirty(preview.gameObject.scene);
            }
            EditorGUILayout.Space(); EditorGUILayout.LabelField("새 프리뷰 등록", EditorStyles.boldLabel);
            label = EditorGUILayout.TextField("표시명 (선택)", label);
            prefab = (GameObject)EditorGUILayout.ObjectField("UI 프리팹", prefab, typeof(GameObject), false);
            controller = (RuntimeAnimatorController)EditorGUILayout.ObjectField("Controller", controller, typeof(RuntimeAnimatorController), false);
            clip = (AnimationClip)EditorGUILayout.ObjectField("Clip (추가 가능)", clip, typeof(AnimationClip), false);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Controller에서 새 클립 추가"))
                {
                    try { AnimationLabWorkbenchAuthoring.AddControllerClips((AnimationLabAssetPreview)target); }
                    catch (Exception e) { Debug.LogError("[Animation Lab] " + e.Message); }
                }
                if (GUILayout.Button("프리팹 등록 / Controller·Clip 등록"))
                {
                    try
                    {
                        var preview = (AnimationLabAssetPreview)target;
                        if (prefab != null) AnimationLabWorkbenchAuthoring.RegisterPrefab(preview, prefab, controller, clip, label);
                        else AnimationLabWorkbenchAuthoring.RegisterClip(preview, controller, clip, label);
                    }
                    catch (Exception e) { Debug.LogError("[Animation Lab] " + e.Message); }
                }
            }
        }
    }
}
