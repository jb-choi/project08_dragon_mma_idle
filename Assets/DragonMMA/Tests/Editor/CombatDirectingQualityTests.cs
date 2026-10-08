using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DragonMMA.Tests
{
    // Perceptual proxies measured at authored 1:1 canvas scale. These gates are
    // intentionally about visible occupation and layered direction, not merely
    // whether references and animation frames exist.
    public sealed class CombatDirectingQualityTests
    {
        [TestCase("OverlayRoot", 500f, 300f, 180f, 115f)]
        [TestCase("WebOverlayRoot", 650f, 380f, 165f, 105f)]
        public void CombatStage_OccupiesEnoughOfTheActualViewport(string prefabName, float minHeight,
            float minBattleHeight, float minFighterHeight, float minBabyHeight)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DragonMMA/Prefabs/UI/" + prefabName + ".prefab");
            var view = new SerializedObject(root.GetComponent<DragonMmaView>());
            var bindings = root.GetComponent<DragonUiBindings>();
            var fighter = (DragonActorView)view.FindProperty("hunter").objectReferenceValue;
            var dragon = (DragonActorView)view.FindProperty("dragon").objectReferenceValue;

            Assert.That(view.FindProperty("huntReferenceHeight").floatValue, Is.GreaterThanOrEqualTo(minHeight), "The collapsed viewport remains a shallow bottom strip.");
            Assert.That(bindings.Get<RectTransform>("Hunt/Battle input patch").rect.height, Is.GreaterThanOrEqualTo(minBattleHeight));
            Assert.That(fighter.Image.rectTransform.rect.height * fighter.transform.localScale.y, Is.GreaterThanOrEqualTo(minFighterHeight), "Fighter is too small at 100% scale.");
            Assert.That(dragon.Image.rectTransform.rect.height * dragon.transform.localScale.y, Is.GreaterThanOrEqualTo(minBabyHeight), "Selected Baby dragon is too small at 100% scale.");
        }

        [TestCase("OverlayRoot")] [TestCase("WebOverlayRoot")]
        public void CombatStage_HasLayeredFocusAndContactEffects(string prefabName)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DragonMMA/Prefabs/UI/" + prefabName + ".prefab");
            Transform hunt = root.transform.Find("Hunt");
            Assert.That(hunt.Find("Combat focus backdrop"), Is.Not.Null);
            Assert.That(hunt.Find("Combat frame top"), Is.Not.Null);
            Assert.That(hunt.Find("Combat beat label"), Is.Not.Null);
            Assert.That(hunt.Find("Impact ring"), Is.Not.Null);
            Assert.That(hunt.Find("Impact echo"), Is.Not.Null);
            Assert.That(hunt.Find("Impact dust"), Is.Not.Null);
            for (int i = 0; i < 5; i++) Assert.That(hunt.Find("Speed line " + i), Is.Not.Null);
        }

        [TestCase("OverlayRoot")] [TestCase("WebOverlayRoot")]
        public void RuntimeView_HasDistinctAuthoredVfxBindings(string prefabName)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DragonMMA/Prefabs/UI/" + prefabName + ".prefab");
            var view = new SerializedObject(root.GetComponent<DragonMmaView>());
            Assert.That(view.FindProperty("combatBeatLabel").objectReferenceValue, Is.Not.Null);
            Assert.That(view.FindProperty("combatFocusObjects").arraySize, Is.GreaterThanOrEqualTo(6));
            var ring = (UnityEngine.UI.Image)view.FindProperty("impactRing").objectReferenceValue;
            var echo = (UnityEngine.UI.Image)view.FindProperty("impactEcho").objectReferenceValue;
            var dust = (UnityEngine.UI.Image)view.FindProperty("impactDust").objectReferenceValue;
            Assert.That(ring, Is.Not.Null); Assert.That(echo, Is.Not.Null); Assert.That(dust, Is.Not.Null);
            Assert.That(ring.sprite, Is.Not.Null); Assert.That(dust.sprite, Is.Not.Null);
            Assert.That(dust.sprite, Is.Not.SameAs(ring.sprite));
            string[] impactFields = { "weakImpactSprite", "heavyImpactSprite", "guardImpactSprite", "knockdownImpactSprite" };
            foreach (string field in impactFields)
                Assert.That(view.FindProperty(field).objectReferenceValue, Is.Not.Null, field + " is not authored");
            Assert.That(impactFields.Select(field => view.FindProperty(field).objectReferenceValue).Distinct().Count(), Is.EqualTo(4));
            Assert.That(view.FindProperty("speedLines").arraySize, Is.EqualTo(5));
            for (int i = 0; i < 5; i++)
            {
                var line = (UnityEngine.UI.Image)view.FindProperty("speedLines").GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(line.sprite, Is.Not.Null);
                Assert.That(line.preserveAspect, Is.False, "Directional streaks must fill their authored wide rectangles.");
            }
        }

        [Test]
        public void CombatMotion_UsesAnticipationAndContactShapeChanges()
        {
            var rewards = new bool[5];
            float windupAt = .16f + (DragonCombatChoreography.EnemyStart(DragonKind.Baby, 0) - .16f) * .75f;
            var anticipation = DragonCombatChoreography.Evaluate(DragonKind.Baby, windupAt, rewards);
            Assert.That(anticipation.DragonScale.x, Is.GreaterThan(1.02f));
            Assert.That(anticipation.DragonScale.y, Is.LessThan(.96f));

            float powerStart = DragonCombatChoreography.CycleSeconds(DragonKind.Baby, 0) +
                DragonCombatChoreography.CycleSeconds(DragonKind.Baby, 1);
            float contactAt = powerStart + DragonCombatChoreography.CounterStart(DragonKind.Baby, 2) +
                FighterCombatTiming.Contact(DragonCombatChoreography.Counter(DragonKind.Baby, 2, rewards)) + .01f;
            var contact = DragonCombatChoreography.Evaluate(DragonKind.Baby, contactAt, rewards);
            Assert.That(contact.FighterScale.x, Is.GreaterThan(1.05f));
            Assert.That(contact.DragonScale.y, Is.LessThan(.92f));
        }

        [Test]
        public void QualityQa_UsesAnIdenticalScenarioBaseline()
        {
            const string before = "Artifacts/CombatDirectingRedo-20261007/before-heavy.png";
            Assert.That(System.IO.File.Exists(before), Is.True);
            var bytes = System.IO.File.ReadAllBytes(before);
            var image = new Texture2D(2, 2);
            try
            {
                Assert.That(image.LoadImage(bytes), Is.True);
                Assert.That(image.width, Is.EqualTo(1920));
                Assert.That(image.height, Is.EqualTo(720));
            }
            finally { Object.DestroyImmediate(image); }
        }
    }
}
