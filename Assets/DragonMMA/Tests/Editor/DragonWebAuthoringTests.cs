using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DragonMMA.Tests
{
    public sealed class DragonWebAuthoringTests
    {
        private GameObject Root => AssetDatabase.LoadAssetAtPath<GameObject>(EditorTools.DragonMmaWebGlBuild.WebPrefab);
        [Test] public void WebPrefab_BindingsAndViewReferencesRemainInternal()
        {
            Assert.That(Root, Is.Not.Null, "Author the dedicated WebGL scene first.");
            foreach (var bindings in Root.GetComponentsInChildren<DragonUiBindings>(true))
            {
                Assert.That(bindings.Elements.Select(e => e.key).Distinct().Count(), Is.EqualTo(bindings.Elements.Count));
                foreach (var entry in bindings.Elements)
                {
                    Assert.That(entry.target, Is.Not.Null, entry.key);
                    Assert.That(entry.target.transform.IsChildOf(Root.transform), Is.True, entry.key);
                }
            }
            var view = new SerializedObject(Root.GetComponent<DragonMmaView>());
            foreach (string name in new[] { "overlay", "storage", "trainingLocked", "trainingEmpty", "trainingActive", "market", "skills", "settings", "quit", "hunter", "dragon", "helper", "trainee" })
                Assert.That(((Component)view.FindProperty(name).objectReferenceValue).transform.IsChildOf(Root.transform), Is.True, name);
        }
        [Test] public void WebPrefab_MainTouchTargetsAreAtLeast44PixelsAt640x360()
        {
            Assert.That(Root, Is.Not.Null);
            float scale = Mathf.Min(640f / 1920, 360f / 1080);
            foreach (var button in Root.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeSelf || !button.interactable) continue;
                var rect = button.GetComponent<RectTransform>();
                Assert.That(rect.sizeDelta.x * scale, Is.GreaterThanOrEqualTo(43.99f), button.name);
                Assert.That(rect.sizeDelta.y * scale, Is.GreaterThanOrEqualTo(43.99f), button.name);
            }
        }
        [Test] public void WebPrefab_DoesNotChangeDesktopLayoutOrExposeNativeWindowSettings()
        {
            Assert.That(Root, Is.Not.Null);
            var desktop = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DragonMMA/Prefabs/UI/OverlayRoot.prefab");
            Assert.That(new SerializedObject(desktop.GetComponent<DragonMmaView>()).FindProperty("expandedReferenceHeight").floatValue, Is.EqualTo(720));
            Assert.That(new SerializedObject(Root.GetComponent<DragonMmaView>()).FindProperty("expandedReferenceHeight").floatValue, Is.EqualTo(1080));
            Assert.That(Root.transform.Find("BaseUI/SettingsPage/Toggle topmost").gameObject.activeSelf, Is.False);
            Assert.That(Root.transform.Find("Hunt/Bottom toolbar/Exit").gameObject.activeSelf, Is.False);
            Assert.That(Root.transform.Find("BaseUI/TrainingLockedPage/Dojo scene/Trainee").gameObject.activeSelf, Is.False);
            Assert.That(Root.transform.Find("BaseUI/TrainingLockedPage/Dojo scene/Practice caption").GetComponent<Text>().text, Does.Contain("잠겨"));
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorTools.DragonMmaWebGlBuild.WebScene), Is.Not.Null);
        }
        [Test] public void WebPrefab_ButtonLabelsResizeWithTouchTargets()
        {
            Assert.That(Root, Is.Not.Null);
            foreach (var button in Root.GetComponentsInChildren<Button>(true))
            {
                var label = button.transform.Find("Label"); if (label == null) continue;
                var rect = (RectTransform)label;
                Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero), button.name);
                Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one), button.name);
                Assert.That(label.GetComponent<Text>().verticalOverflow, Is.EqualTo(VerticalWrapMode.Truncate), button.name);
            }
        }
        [Test] public void WebPrefab_FighterAndDragonNamesDoNotOverlap()
        {
            Assert.That(Root, Is.Not.Null);
            var hunter = (RectTransform)Root.transform.Find("Hunt/Hunter name plate");
            var dragon = (RectTransform)Root.transform.Find("Hunt/Dragon name plate");
            Assert.That(hunter.anchoredPosition.x + hunter.sizeDelta.x, Is.LessThanOrEqualTo(dragon.anchoredPosition.x));
        }
    }
}
