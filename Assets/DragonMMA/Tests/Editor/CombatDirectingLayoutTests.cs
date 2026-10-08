using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DragonMMA.Tests
{
    public sealed class CombatDirectingLayoutTests
    {
        [TestCase("OverlayRoot", 980f, 1.86f, 1.86f)]
        [TestCase("WebOverlayRoot", 1100f, 1.68f, 1.86f)]
        public void BattleStage_UsesExpandedInputAreaAndLargerActors(string prefab, float expectedWidth, float fighterScale, float dragonScale)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DragonMMA/Prefabs/UI/" + prefab + ".prefab");
            var bindings = root.GetComponent<DragonUiBindings>();
            var data = new SerializedObject(root.GetComponent<DragonMmaView>());
            var fighter = (RectTransform)((DragonActorView)data.FindProperty("hunter").objectReferenceValue).transform;
            var dragon = (RectTransform)((DragonActorView)data.FindProperty("dragon").objectReferenceValue).transform;

            Assert.That(bindings.Get<RectTransform>("Hunt/Battle input patch").rect.width, Is.EqualTo(expectedWidth).Within(.01f));
            Assert.That(fighter.localScale.x, Is.EqualTo(fighterScale).Within(.001f));
            Assert.That(dragon.localScale.x, Is.EqualTo(dragonScale).Within(.001f));
            Assert.That(fighter.anchoredPosition.x, Is.LessThan(dragon.anchoredPosition.x));
        }

        [TestCase("OverlayRoot")]
        [TestCase("WebOverlayRoot")]
        public void BattleStage_KeepsToolbarNameplatesActorsAndHudInSeparateVerticalBands(string prefab)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DragonMMA/Prefabs/UI/" + prefab + ".prefab");
            var bindings = root.GetComponent<DragonUiBindings>();
            var data = new SerializedObject(root.GetComponent<DragonMmaView>());
            var fighter = (DragonActorView)data.FindProperty("hunter").objectReferenceValue;
            var dragon = (DragonActorView)data.FindProperty("dragon").objectReferenceValue;
            var toolbar = bindings.Get<RectTransform>("Hunt/Bottom toolbar");
            var fighterPlate = bindings.Get<RectTransform>("Hunt/Hunter name plate");
            var dragonPlate = bindings.Get<RectTransform>("Hunt/Dragon name plate");
            var hud = bindings.Get<RectTransform>("Hunt/Battle status");
            float toolbarTop = toolbar.anchoredPosition.y + toolbar.rect.height;
            float plateTop = Mathf.Max(fighterPlate.anchoredPosition.y + fighterPlate.rect.height, dragonPlate.anchoredPosition.y + dragonPlate.rect.height);

            Assert.That(fighterPlate.anchoredPosition.y, Is.GreaterThanOrEqualTo(toolbarTop));
            Assert.That(dragonPlate.anchoredPosition.y, Is.GreaterThanOrEqualTo(toolbarTop));
            Assert.That(plateTop, Is.LessThan(((RectTransform)fighter.transform).anchoredPosition.y));
            Assert.That(plateTop, Is.LessThan(((RectTransform)dragon.transform).anchoredPosition.y));

            float fighterHeight = fighter.Image.rectTransform.rect.height * fighter.Image.rectTransform.localScale.y * fighter.transform.localScale.y;
            var scales = new SerializedObject(dragon).FindProperty("dragonPixelScales");
            float maxDragonHeight = 0;
            for (int kind = 0; kind < scales.arraySize; kind++)
            {
                float maxSpriteHeight = AssetDatabase.LoadAllAssetsAtPath("Assets/DragonMMA/Resources/DragonMMA/DragonSheets/dragon_" + kind + ".png")
                    .OfType<Sprite>().Where(sprite => !sprite.name.EndsWith("_portrait")).Max(sprite => sprite.rect.height);
                float authoredHeight = maxSpriteHeight * scales.GetArrayElementAtIndex(kind).floatValue;
                maxDragonHeight = Mathf.Max(maxDragonHeight, authoredHeight);
            }

            Assert.That(((RectTransform)fighter.transform).anchoredPosition.y + fighterHeight, Is.LessThan(hud.anchoredPosition.y));
            Assert.That(((RectTransform)dragon.transform).anchoredPosition.y + maxDragonHeight * dragon.transform.localScale.y, Is.LessThan(hud.anchoredPosition.y));
        }
    }
}
