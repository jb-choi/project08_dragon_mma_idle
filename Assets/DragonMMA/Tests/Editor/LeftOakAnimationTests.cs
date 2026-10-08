using System.Linq;
using DragonMMA.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DragonMMA.Tests
{
    public sealed class LeftOakAnimationTests
    {
        [Test]
        public void AuthoredAnimation_UsesFiveRegisteredFramesAndPingPongLoop()
        {
            Assert.DoesNotThrow(LeftOakAnimationAuthoring.ValidateOrThrow);
            var sprites = AssetDatabase.LoadAllAssetsAtPath(LeftOakAnimationAuthoring.SheetPath)
                .OfType<Sprite>()
                .OrderBy(sprite => sprite.name)
                .ToArray();
            Assert.That(sprites.Select(sprite => sprite.name), Is.EqualTo(LeftOakAnimationAuthoring.FrameNames));
        }

        [TestCase(LeftOakAnimationAuthoring.DesktopPrefabPath)]
        [TestCase(LeftOakAnimationAuthoring.WebPrefabPath)]
        public void OverlayPrefab_LeftOakHasAnimator_RightOakRemainsStatic(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);
            var transforms = prefab.GetComponentsInChildren<Transform>(true);
            var leftOak = transforms.Single(item => item.name == "Left oak");
            var rightOak = transforms.Single(item => item.name == "Right oak");
            Assert.That(leftOak.GetComponent<Image>().sprite.name, Is.EqualTo("tree_sway_0"));
            Assert.That(leftOak.GetComponent<Animator>().runtimeAnimatorController,
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(LeftOakAnimationAuthoring.ControllerPath)));
            Assert.That(rightOak.GetComponent<Animator>(), Is.Null);
        }
    }
}
