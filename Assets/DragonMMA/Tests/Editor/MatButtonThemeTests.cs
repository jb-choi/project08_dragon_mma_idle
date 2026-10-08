using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DragonMMA.Tests
{
    public sealed class MatButtonThemeTests
    {
        [TestCase(EditorTools.MatButtonThemeAuthoring.DesktopPrefab)]
        [TestCase(EditorTools.MatButtonThemeAuthoring.WebPrefab)]
        public void Prefab_OrdinaryButtonsUseSlicedMatButCardsKeepSelectionStyle(string path)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var buttons = root.GetComponentsInChildren<Button>(true);
            Assert.That(buttons.Count(b => b.GetComponent<MatButtonPressFeedback>() != null), Is.EqualTo(29));
            foreach (var button in buttons)
            {
                if (button.name.StartsWith("StorageCard"))
                {
                    Assert.That(button.GetComponent<MatButtonPressFeedback>(), Is.Null);
                    continue;
                }
                var feedback = button.GetComponent<MatButtonPressFeedback>();
                Assert.That(feedback, Is.Not.Null, button.name);
                Assert.That(feedback.Label.transform.parent, Is.EqualTo(button.transform));
                Assert.That(feedback.Label.name, Is.EqualTo("Label"));
                Assert.That(feedback.Label.text, Does.Not.Contain("BUTTON"));
                var skin = feedback.Plate.GetComponent<Image>();
                Assert.That(skin.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(skin.sprite, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Sprite>(EditorTools.MatButtonThemeAuthoring.SpritePath)));
                Assert.That(skin.raycastTarget, Is.False);
                Assert.That(button.targetGraphic, Is.EqualTo(skin));
                Assert.That(button.GetComponent<Image>().raycastTarget, Is.True);
                Assert.That(button.GetComponent<Image>().alphaHitTestMinimumThreshold, Is.Zero);
            }
        }

        [Test] public void DesktopToolbar_ButtonsAndStatusLabelsDoNotOverlap()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(EditorTools.MatButtonThemeAuthoring.DesktopPrefab);
            var bar = root.transform.Find("Hunt/Bottom toolbar");
            var names = new[] { "Camp", "Training", "Market", "Skills", "Input hint", "Capacity", "Return", "Settings fixed tab", "Exit" };
            float right = 0;
            foreach (string name in names)
            {
                var rect = (RectTransform)bar.Find(name);
                Assert.That(rect.anchoredPosition.x, Is.GreaterThanOrEqualTo(right), name);
                right = rect.anchoredPosition.x + rect.sizeDelta.x;
                Assert.That(right, Is.LessThanOrEqualTo(1920), name);
            }
        }

        [Test] public void MatSprite_TransparentExteriorAndNoOpaqueBlackColorKeyHoles()
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(EditorTools.MatButtonThemeAuthoring.SpritePath));
                var pixels = texture.GetPixels32();
                Assert.That(pixels.Count(p => p.a == 0), Is.GreaterThan(pixels.Length / 10));
                Assert.That(pixels.Count(p => p.a == 255 && p.r == 0 && p.g == 0 && p.b == 0), Is.Zero);
                var importer = (TextureImporter)AssetImporter.GetAtPath(EditorTools.MatButtonThemeAuthoring.SpritePath);
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.isReadable, Is.False);
                Assert.That(importer.spriteBorder.x, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test] public void Easing_PressSettlesAndReleaseHasSmallRebound()
        {
            Assert.That(MatButtonPressFeedback.PressEase(0), Is.Zero);
            Assert.That(MatButtonPressFeedback.PressEase(1), Is.EqualTo(1));
            Assert.That(MatButtonPressFeedback.ReleaseEase(0), Is.EqualTo(0).Within(.0001f));
            Assert.That(MatButtonPressFeedback.ReleaseEase(1), Is.EqualTo(1));
            Assert.That(MatButtonPressFeedback.ReleaseEase(.8f), Is.InRange(1.001f, 1.08f));
        }

        private GameObject root;
        private Button button;
        private MatButtonPressFeedback feedback;
        private PointerEventData pointer;
        private EventSystem eventSystem;

        private void MakeButton()
        {
            root = new GameObject("Test mat button", typeof(RectTransform), typeof(Image), typeof(Button));
            button = root.GetComponent<Button>();
            var plate = new GameObject("Mat visual", typeof(RectTransform), typeof(Image));
            plate.transform.SetParent(root.transform, false);
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(root.transform, false);
            feedback = root.AddComponent<MatButtonPressFeedback>();
            feedback.ConfigureEditor((RectTransform)plate.transform, label.GetComponent<Text>(), 3);
            var events = new GameObject("Test event system", typeof(EventSystem));
            eventSystem = events.GetComponent<EventSystem>();
            pointer = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
        }

        [TearDown] public void Cleanup()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (eventSystem != null) Object.DestroyImmediate(eventSystem.gameObject);
        }

        [Test] public void Press_DeformsVisualsWithoutMovingHitboxOrInvokingClick()
        {
            MakeButton();
            var rect = (RectTransform)root.transform;
            var position = rect.anchoredPosition;
            var size = rect.sizeDelta;
            int clicks = 0; button.onClick.AddListener(() => clicks++);
            feedback.OnPointerDown(pointer);
            feedback.AdvanceFeedback(.075f);
            Assert.That(feedback.Plate.localScale.y, Is.EqualTo(.9f).Within(.0001));
            Assert.That(feedback.Plate.anchoredPosition.y, Is.EqualTo(-3).Within(.0001));
            Assert.That(rect.anchoredPosition, Is.EqualTo(position));
            Assert.That(rect.sizeDelta, Is.EqualTo(size));
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
            Assert.That(clicks, Is.Zero);
            feedback.OnPointerUp(pointer); feedback.AdvanceFeedback(.16f);
            Assert.That(feedback.Plate.localScale, Is.EqualTo(Vector3.one));
            Assert.That(feedback.Plate.anchoredPosition, Is.EqualTo(Vector2.zero));
        }

        [Test] public void DragOutAndBackIn_ReleasesThenPressesAgain()
        {
            MakeButton();
            feedback.OnPointerDown(pointer); feedback.AdvanceFeedback(.075f);
            feedback.OnPointerExit(pointer); feedback.AdvanceFeedback(.16f);
            Assert.That(feedback.Plate.localScale, Is.EqualTo(Vector3.one));
            feedback.OnPointerEnter(pointer); feedback.AdvanceFeedback(.075f);
            Assert.That(feedback.Plate.localScale.y, Is.LessThan(1));
            feedback.OnPointerUp(pointer); feedback.AdvanceFeedback(.16f);
            feedback.OnPointerEnter(pointer); feedback.AdvanceFeedback(.075f);
            Assert.That(feedback.Plate.localScale, Is.EqualTo(Vector3.one));
        }

        [Test] public void DisabledButtonAndRightClick_DoNotPress()
        {
            MakeButton();
            button.interactable = false;
            feedback.OnPointerDown(pointer); feedback.AdvanceFeedback(.1f);
            Assert.That(feedback.Plate.localScale, Is.EqualTo(Vector3.one));
            button.interactable = true;
            pointer.button = PointerEventData.InputButton.Right;
            feedback.OnPointerDown(pointer); feedback.AdvanceFeedback(.1f);
            Assert.That(feedback.Plate.localScale, Is.EqualTo(Vector3.one));
        }

        [Test] public void DisablingPressedButton_ReleasesAndDimsLabel()
        {
            MakeButton();
            feedback.OnPointerDown(pointer); feedback.AdvanceFeedback(.075f);
            button.interactable = false;
            feedback.AdvanceFeedback(.16f);
            Assert.That(feedback.Plate.localScale, Is.EqualTo(Vector3.one));
            Assert.That(feedback.Plate.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(feedback.Label.color.a, Is.EqualTo(.48f).Within(.0001));
        }

        [Test] public void Submit_AnimatesOnUnscaledClockWithoutDuplicatingClick()
        {
            MakeButton();
            int clicks = 0; button.onClick.AddListener(() => clicks++);
            feedback.OnSubmit(new BaseEventData(eventSystem));
            feedback.AdvanceFeedback(.075f);
            Assert.That(feedback.Plate.localScale.y, Is.LessThan(1));
            feedback.AdvanceFeedback(.2f);
            Assert.That(feedback.Plate.localScale, Is.EqualTo(Vector3.one));
            Assert.That(clicks, Is.Zero);
        }
    }
}
