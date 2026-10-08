using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DragonMMA.Tests
{
    public sealed class CombatDirectingFeedbackTests
    {
        [Test]
        public void ImpactPresets_HaveClearWeakGuardHeavyKnockdownHierarchy()
        {
            var weak = CombatFeedbackTiming.Get(CombatImpactKind.Weak);
            var guard = CombatFeedbackTiming.Get(CombatImpactKind.Guard);
            var heavy = CombatFeedbackTiming.Get(CombatImpactKind.Heavy);
            var down = CombatFeedbackTiming.Get(CombatImpactKind.Knockdown);

            Assert.That(weak.HitStopSeconds, Is.LessThan(guard.HitStopSeconds));
            Assert.That(guard.HitStopSeconds, Is.LessThan(heavy.HitStopSeconds));
            Assert.That(heavy.HitStopSeconds, Is.LessThan(down.HitStopSeconds));
            Assert.That(weak.VfxScale, Is.LessThan(heavy.VfxScale));
            Assert.That(heavy.VfxScale, Is.LessThan(down.VfxScale));
            Assert.That(down.StageKickPixels, Is.GreaterThan(heavy.StageKickPixels));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void Choreography_ReportsActualActorStatesAtEachBeat(int index)
        {
            var kind = (DragonKind)index;
            var rewards = Enumerable.Repeat(true, 5).ToArray();
            float enemyStart = DragonCombatChoreography.EnemyStart(kind, 0);
            float enemyContact = enemyStart + DragonAnimationTiming.Contact(kind);
            string counterPose = DragonCombatChoreography.Counter(kind, 0, rewards);
            float counterContact = DragonCombatChoreography.CounterStart(kind, 0) + FighterCombatTiming.Contact(counterPose);

            Assert.That(DragonCombatChoreography.Evaluate(kind, enemyStart - .01f, rewards).DragonState, Is.EqualTo(CombatActorState.Windup));
            var defended = DragonCombatChoreography.Evaluate(kind, enemyContact + .001f, rewards);
            Assert.That(defended.DragonState, Is.EqualTo(CombatActorState.Attack));
            Assert.That(defended.FighterState, Is.EqualTo(CombatActorState.Guard));
            Assert.That(defended.Impact, Is.EqualTo(CombatImpactKind.Guard));

            var countered = DragonCombatChoreography.Evaluate(kind, counterContact + .001f, rewards);
            Assert.That(countered.FighterState, Is.EqualTo(CombatActorState.Attack));
            Assert.That(countered.DragonState, Is.EqualTo(CombatActorState.Hit));
            Assert.That(countered.Impact, Is.Not.EqualTo(CombatImpactKind.None));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void NextContact_AdvancesWithoutRepeatingOrSkippingBeats(int index)
        {
            var kind = (DragonKind)index;
            var rewards = new bool[5];
            float cursor = 0;
            for (int i = 0; i < 12; i++)
            {
                float next = DragonCombatChoreography.NextContact(kind, cursor, rewards);
                Assert.That(next, Is.GreaterThan(cursor), "contact " + i);
                var frame = DragonCombatChoreography.Evaluate(kind, next + .001f, rewards);
                Assert.That(frame.ContactAge, Is.GreaterThanOrEqualTo(0));
                cursor = next + .001f;
            }
        }

        [Test]
        public void StateLabels_AreCompleteAndHumanReadable()
        {
            foreach (CombatActorState state in System.Enum.GetValues(typeof(CombatActorState)))
            {
                Assert.That(CombatFeedbackTiming.Label(state), Is.Not.Empty);
                Assert.That(CombatFeedbackTiming.StateColor(state).a, Is.EqualTo(1));
            }
        }

        [TestCase("OverlayRoot")] [TestCase("WebOverlayRoot")]
        public void OverlayPrefabs_HaveDistinctFourWayCombatAudio(string prefabName)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DragonMMA/Prefabs/UI/" + prefabName + ".prefab");
            var data = new SerializedObject(root.GetComponent<DragonMmaView>());
            string[] fields = { "jabSound", "heavySound", "guardSound", "knockdownSound" };
            var clips = fields.Select(field => data.FindProperty(field).objectReferenceValue as AudioClip).ToArray();
            Assert.That(clips.All(clip => clip != null), Is.True);
            Assert.That(clips.Select(AssetDatabase.GetAssetPath).Distinct().Count(), Is.EqualTo(4));
            Assert.That(AssetDatabase.GetAssetPath(clips[3]), Is.EqualTo("Assets/DragonMMA/Audio/fighter_knockdown.wav"));
        }

        [Test]
        public void SelectedBabyDragon_UsesApprovedLeafCrestSheetInSourceAndRuntime()
        {
            const string source = "Assets/DragonMMA/ArtSource/ForestDragons/dragon_0_sheet.png";
            const string approved = "Assets/DragonMMA/ArtSource/ForestDragons/dragon_0_leafcrest_sheet.png";
            const string runtime = "Assets/DragonMMA/Resources/DragonMMA/DragonSheets/dragon_0.png";
            byte[] pixels = File.ReadAllBytes(approved);

            Assert.That(File.ReadAllBytes(source), Is.EqualTo(pixels));
            Assert.That(File.ReadAllBytes(runtime), Is.EqualTo(pixels));
            using (var sha = SHA256.Create())
                Assert.That(System.BitConverter.ToString(sha.ComputeHash(pixels)).Replace("-", ""),
                    Is.EqualTo("A04373A43894B90F0AA644B242A484EFC593E1BC7A12689628EBF4071D258A86"));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(runtime).OfType<Sprite>().Count(), Is.EqualTo(37));
        }
    }
}
