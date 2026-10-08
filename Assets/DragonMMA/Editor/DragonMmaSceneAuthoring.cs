using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DragonMMA.EditorTools
{
    /// <summary>One-time migration. After conversion the scene/prefabs are the source of truth.</summary>
    public static class DragonMmaSceneAuthoring
    {
        public const string PrefabRoot = "Assets/DragonMMA/Prefabs";
        public const string ConfigPath = "Assets/DragonMMA/Configuration/DefaultGameConfig.asset";
        [MenuItem("Dragon MMA/Authoring/Convert Current Scene (once)")]
        public static void ConvertCurrentScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before conversion.");
            if (Object.FindFirstObjectByType<DragonMmaView>(FindObjectsInactive.Include) != null)
                throw new InvalidOperationException("Scene is already authored. Edit its existing prefabs; conversion will not overwrite your edits.");
            var game = Object.FindFirstObjectByType<DragonMmaGame>();
            if (game == null) throw new InvalidOperationException("Open DesktopOverlayScene first.");
            string scenePath = game.gameObject.scene.path;
            if (scenePath != "Assets/DragonMMA/Scenes/DesktopOverlayScene.unity") throw new InvalidOperationException("Unexpected scene: " + scenePath);
            foreach (var folder in new[] { PrefabRoot + "/UI", PrefabRoot + "/Actors", PrefabRoot + "/Facilities", "Assets/DragonMMA/Configuration" }) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            DragonAnimationAuthoring.CreateAssets();
            var config = AssetDatabase.LoadAssetAtPath<DragonGameConfig>(ConfigPath);
            if (config == null) { config = ScriptableObject.CreateInstance<DragonGameConfig>(); AssetDatabase.CreateAsset(config, ConfigPath); }
            Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/DragonMMA/Fonts/NanumGothic-Regular.ttf");
            if (font == null) throw new InvalidOperationException("Import the bundled Nanum Gothic font before conversion.");
            var data = config.CreateNewSave();
            for (int i = 0; i < 13; i++) data.storage.Add(new DragonInstance((DragonKind)(i % 5)));
            data.coins = 160; data.trainingUnlocked = true;
            data.training[0].dragon = new DragonInstance(DragonKind.Headbutt, true); data.training[0].completed = true; data.training[0].rewardApplied = true;
            for (int i = 0; i < 2; i++) { data.sales[i].dragon = new DragonInstance((DragonKind)i); data.sales[i].completed = true; }
            var session = new DragonGameSession(data, null, config);
            var seed = new DragonMmaLayoutSeed(game, session, game.GetComponent<DesktopOverlayWindow>());
            var canvas = seed.Canvas; canvas.name = "OverlayRoot";
            Undo.RegisterCreatedObjectUndo(canvas.gameObject, "Create editable Dragon MMA UI");
            RectTransform baseRect = (RectTransform)canvas.transform.Find("BaseUI");
            RectTransform huntRect = (RectTransform)canvas.transform.Find("Hunt");
            Center(huntRect, 0); Center(baseRect, 362);
            RenameRepeated(canvas.transform.Find("Hunt"), "Forest tile", 7);
            RenameRepeated(canvas.transform.Find("Hunt"), "Understory", 10);
            RenameRepeated(canvas.transform.Find("Hunt/PrisonCart"), "Passenger", 3);
            RenameRepeated(canvas.transform.Find("Hunt/Departing cart"), "Departing passenger", 3);
            RenameRepeated(canvas.transform.Find("Hunt/Battle status"), "Progress track", 2);

            var pages = new List<DragonUiBindings>();
            var storage = CopyPage(seed, "storage", "StoragePage", font);
            int row = 0;
            foreach (Transform child in storage.transform)
                if (child.name.StartsWith("Dragon ")) child.name = "StorageCard" + row++;
            RenameRepeated(storage.transform, "Recent roaming dragon", 10);
            for (int i = 0; i < 10; i++) storage.transform.Find("Recent roaming dragon" + i).name = "Roamer" + i;
            var empty = new GameObject("Empty state", typeof(RectTransform), typeof(Text));
            empty.transform.SetParent(storage.transform, false);
            var er = empty.GetComponent<RectTransform>(); er.anchorMin = er.anchorMax = er.pivot = Vector2.zero;
            er.anchoredPosition = new Vector2(24, 80); er.sizeDelta = new Vector2(960, 100);
            var et = empty.GetComponent<Text>(); et.font = font; et.fontSize = 22; et.color = new Color(.7f,.78f,.7f); et.text = "아직 조용한 수용소입니다.\n용을 포획한 뒤 ‘수레 귀환’을 눌러주세요."; et.raycastTarget = false;
            empty.SetActive(false);
            MakeStorageCards(storage.transform);
            pages.Add(storage);
            data.trainingUnlocked = false; pages.Add(CopyPage(seed, "training", "TrainingLockedPage", font));
            data.trainingUnlocked = true;
            data.training[0].Clear(); pages.Add(CopyPage(seed, "training", "TrainingEmptyPage", font));
            data.training[0].dragon = new DragonInstance(DragonKind.Headbutt, true); data.training[0].completed = true; data.training[0].rewardApplied = true;
            var activeTraining = CopyPage(seed, "training", "TrainingActivePage", font); pages.Add(activeTraining);
            var market = CopyPage(seed, "market", "MarketPage", font);
            data.sales[0].Clear(); data.sales[1].Clear();
            var emptyMarket = CopyPage(seed, "market", "TemporaryEmptyMarket", font);
            for (int i = 0; i < 2; i++)
            {
                Transform target = market.transform.Find("Market slot " + i), from = emptyMarket.transform.Find("Market slot " + i);
                Object.Instantiate(from.Find("Empty listing").gameObject, target, false).name = "Empty listing";
                Object.Instantiate(from.Find("Choose seller").gameObject, target, false).name = "Choose seller";
            }
            Object.DestroyImmediate(emptyMarket.gameObject);
            pages.Add(market);
            pages.Add(CopyPage(seed, "skills", "SkillsPage", font));
            pages.Add(CopyPage(seed, "settings", "SettingsPage", font));
            pages.Add(CopyPage(seed, "exit", "QuitPage", font));
            Object.DestroyImmediate(canvas.transform.Find("BaseUI/Facility content").gameObject);

            var portraits = new Sprite[5]; var controllers = new RuntimeAnimatorController[5];
            for (int i = 0; i < 5; i++)
            {
                portraits[i] = DragonArtCatalog.Load("dragon_" + i);
                controllers[i] = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DragonAnimationAuthoring.DragonControllerPath(i));
            }
            var hunterController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DragonAnimationAuthoring.HunterControllerPath);
            DragonActorView hunter = MakeActor(canvas.transform.Find("Hunt/Hunter").gameObject, false, hunterController, controllers, portraits);
            DragonActorView dragon = MakeActor(canvas.transform.Find("Hunt/Dragon").gameObject, true, hunterController, controllers, portraits);
            DragonActorView helper = MakeActor(canvas.transform.Find("Hunt/Departing cart/Cart helper").gameObject, false, hunterController, controllers, portraits);
            DragonActorView trainee = MakeActor(activeTraining.transform.Find("Dojo scene/Trainee").gameObject, true, hunterController, controllers, portraits);
            ((RectTransform)trainee.transform).anchoredPosition = new Vector2(370, 64);
            trainee.transform.localScale = new Vector3(-1, 1, 1);
            PrefabUtility.SaveAsPrefabAssetAndConnect(hunter.gameObject, PrefabRoot + "/Actors/Hunter.prefab", InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAssetAndConnect(dragon.gameObject, PrefabRoot + "/Actors/Dragon.prefab", InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAssetAndConnect(helper.gameObject, PrefabRoot + "/Actors/CartHelper.prefab", InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAssetAndConnect(trainee.gameObject, PrefabRoot + "/Actors/TrainingDragon.prefab", InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAssetAndConnect(canvas.transform.Find("Hunt/PrisonCart").gameObject, PrefabRoot + "/Facilities/PrisonCart.prefab", InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAssetAndConnect(canvas.transform.Find("Hunt/Forest outpost").gameObject, PrefabRoot + "/Facilities/ForestOutpost.prefab", InteractionMode.AutomatedAction);
            foreach (var text in canvas.GetComponentsInChildren<Text>(true)) text.font = font;
            foreach (var p in pages)
            {
                p.CaptureEditorBindings();
                PrefabUtility.SaveAsPrefabAssetAndConnect(p.gameObject, PrefabRoot + "/UI/" + p.name + ".prefab", InteractionMode.AutomatedAction);
            }
            var refs = canvas.gameObject.AddComponent<DragonUiBindings>(); refs.CaptureEditorBindings();
            var view = canvas.gameObject.AddComponent<DragonMmaView>();
            view.ConfigureEditor(refs, pages.ToArray(), new[] { hunter, dragon, helper, trainee }, portraits);
            view.PreviewEditorPage("storage");
            PrefabUtility.SaveAsPrefabAssetAndConnect(canvas.gameObject, PrefabRoot + "/UI/OverlayRoot.prefab", InteractionMode.AutomatedAction);
            var serialized = new SerializedObject(game);
            serialized.FindProperty("view").objectReferenceValue = view;
            serialized.FindProperty("configuration").objectReferenceValue = config;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = canvas.gameObject;
            if (SceneView.lastActiveSceneView != null) { SceneView.lastActiveSceneView.in2DMode = true; SceneView.lastActiveSceneView.FrameSelected(); }
            Debug.Log("[Dragon MMA Authoring] Converted scene to serialized Canvas, 8 pages, nested actor/facility prefabs, Animator clips and Game Config. No runtime UI creation.");
        }
        private static void Center(RectTransform rect, float y)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.pivot = new Vector2(.5f, 0); rect.anchoredPosition = new Vector2(0, y);
        }
        private static DragonUiBindings CopyPage(DragonMmaLayoutSeed seed, string tab, string name, Font font)
        {
            seed.OpenPanel(tab);
            Transform content = seed.Canvas.transform.Find("BaseUI/Facility content");
            var copy = Object.Instantiate(content.gameObject, content.parent, false); copy.name = name;
            foreach (Text text in copy.GetComponentsInChildren<Text>(true)) text.font = font;
            return copy.AddComponent<DragonUiBindings>();
        }
        private static void RenameRepeated(Transform parent, string prefix, int count)
        {
            int index = 0;
            foreach (Transform child in parent) if (child.name == prefix) child.name = prefix + index++;
            if (index != count) throw new InvalidOperationException(parent.name + ": expected " + count + " " + prefix + ", got " + index);
        }
        private static DragonActorView MakeActor(GameObject root, bool isDragon, RuntimeAnimatorController hunter, RuntimeAnimatorController[] dragons, Sprite[] previews)
        {
            Image old = root.GetComponent<Image>();
            var child = new GameObject("Visual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Animator)); child.transform.SetParent(root.transform, false);
            var rect = child.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = child.GetComponent<Image>(); EditorUtility.CopySerialized(old, image); Object.DestroyImmediate(old);
            var actor = root.AddComponent<DragonActorView>(); var animator = child.GetComponent<Animator>();
            animator.runtimeAnimatorController = isDragon ? dragons[0] : hunter;
            actor.ConfigureEditor(image, animator, isDragon ? DragonActorView.ActorRole.Dragon : DragonActorView.ActorRole.Hunter, hunter, dragons, previews);
            return actor;
        }
        private static void MakeStorageCards(Transform storage)
        {
            const string path = PrefabRoot + "/UI/StorageCard.prefab";
            var sample = Object.Instantiate(storage.Find("StorageCard1").gameObject);
            sample.name = "StorageCard"; ((RectTransform)sample.transform).anchoredPosition = Vector2.zero;
            var prefab = PrefabUtility.SaveAsPrefabAsset(sample, path); Object.DestroyImmediate(sample);
            for (int i = 0; i < 10; i++)
            {
                var old = storage.Find("StorageCard" + i); Vector2 position = ((RectTransform)old).anchoredPosition;
                Sprite portrait = old.Find("Portrait").GetComponent<Image>().sprite;
                string title = old.Find("Name").GetComponent<Text>().text, state = old.Find("Individual state").GetComponent<Text>().text;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, storage);
                instance.name = "StorageCard" + i; ((RectTransform)instance.transform).anchoredPosition = position;
                instance.transform.Find("Portrait").GetComponent<Image>().sprite = portrait;
                instance.transform.Find("Name").GetComponent<Text>().text = title;
                instance.transform.Find("Individual state").GetComponent<Text>().text = state;
                Object.DestroyImmediate(old.gameObject);
            }
        }
    }

    [CustomEditor(typeof(DragonMmaView))]
    public sealed class DragonMmaViewInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("이 화면은 씬/프리팹에 저장됩니다. 아래 미리보기로 페이지를 고른 뒤 Hierarchy에서 RectTransform · Image · Text를 수정하세요. Play 중 변경은 Unity 기본 규칙대로 임시입니다.", MessageType.Info);
            var view = (DragonMmaView)target;
            EditorGUILayout.BeginHorizontal();
            string[] pages = { "", "storage", "training", "market", "training-locked", "training-empty", "skills", "settings", "exit" };
            string[] labels = { "사냥", "수용소", "수련방", "판매소", "수련 잠김", "수련 빈 방", "기술", "설정", "종료" };
            for (int i = 0; i < 4; i++) if (GUILayout.Button(labels[i])) Preview(view, pages[i]);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            for (int i = 4; i < pages.Length; i++) if (GUILayout.Button(labels[i])) Preview(view, pages[i]);
            EditorGUILayout.EndHorizontal();
            DrawDefaultInspector();
        }
        private static void Preview(DragonMmaView view, string page)
        {
            Undo.RegisterFullObjectHierarchyUndo(view.gameObject, "Preview authored page");
            view.PreviewEditorPage(page); EditorUtility.SetDirty(view);
            if (!EditorApplication.isPlaying) EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        }
    }
}

