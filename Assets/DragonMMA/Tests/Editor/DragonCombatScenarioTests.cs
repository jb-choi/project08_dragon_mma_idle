using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using DragonMMA.EditorTools;

namespace DragonMMA.Tests
{
    public sealed class DragonCombatScenarioTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void SpeciesProfiles_HaveThreeDesignedRhythms(int kind)
        {
            var species = (DragonKind)kind;
            var lengths = Enumerable.Range(0, 3).Select(i => DragonCombatChoreography.CycleSeconds(species, i)).ToArray();
            Assert.That(lengths.Distinct().Count(), Is.EqualTo(3));
            Assert.That(lengths.Sum(), Is.EqualTo(DragonCombatChoreography.SequenceSeconds(species)).Within(.001));
            Assert.That(DragonCombatChoreography.Pattern(0), Is.EqualTo(DragonCombatChoreography.ExchangePattern.Probe));
            Assert.That(DragonCombatChoreography.Pattern(1), Is.EqualTo(DragonCombatChoreography.ExchangePattern.Rush));
            Assert.That(DragonCombatChoreography.Pattern(2), Is.EqualTo(DragonCombatChoreography.ExchangePattern.Power));
            Assert.That(new[] { DragonCombatChoreography.EnemyImpact(0), DragonCombatChoreography.EnemyImpact(1), DragonCombatChoreography.EnemyImpact(2) }.Distinct().Count(), Is.EqualTo(3));
        }
        [Test]
        public void CounterSelection_RespectsEveryUnlockCombinationAndSpecies()
        {
            for (int mask = 0; mask < 32; mask++)
            {
                var rewards = Enumerable.Range(0, 5).Select(i => (mask & (1 << i)) != 0).ToArray();
                for (int kind = 0; kind < 5; kind++) for (int cycle = 0; cycle < 9; cycle++)
                {
                    string pose = DragonCombatChoreography.Counter((DragonKind)kind, cycle, rewards);
                    if (pose == "cross") Assert.That(rewards[1], Is.True);
                    if (pose == "clinch") { Assert.That(rewards[3], Is.True); Assert.That(kind, Is.GreaterThanOrEqualTo(3)); }
                    if (pose == "takedown") { Assert.That(rewards[4], Is.True); Assert.That(kind, Is.EqualTo(4)); }
                    if (kind == 0) Assert.That(new[] { "punch", "kick" }, Does.Contain(pose));
                }
            }
            var all = new[] { true, true, true, true, true };
            Assert.That(DragonCombatChoreography.Counter(DragonKind.Stonehorn, 2, all), Is.EqualTo("clinch"));
            Assert.That(DragonCombatChoreography.Counter(DragonKind.Giant, 2, all), Is.EqualTo("takedown"));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void SharedClock_SequencesReadWindupAttackCounterRest(int index)
        {
            var kind = (DragonKind)index; var rewards = new bool[5];
            Assert.That(DragonCombatChoreography.Evaluate(kind, .1f, rewards).DragonPose, Is.EqualTo("idle"));
            Assert.That(DragonCombatChoreography.Evaluate(kind, .3f, rewards).DragonPose, Is.EqualTo("windup"));
            Assert.That(DragonCombatChoreography.Evaluate(kind, DragonCombatChoreography.EnemyStart(kind) + .01f, rewards).DragonPose, Is.EqualTo("attack"));
            var counter = DragonCombatChoreography.Evaluate(kind, DragonCombatChoreography.CounterStart(kind) + .01f, rewards);
            Assert.That(counter.FighterPose, Is.EqualTo(index == 2 ? "kick" : "punch"));
            Assert.That(counter.Defending, Is.False);
            Assert.That(DragonCombatChoreography.Evaluate(kind, DragonCombatChoreography.CycleSeconds(kind) - .1f, rewards).FighterPose, Is.EqualTo("idle"));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void BothActors_AndRootTravelHoldForImpactSpecificDuration(int index)
        {
            var kind = (DragonKind)index; var rewards = new bool[5];
            float enemyContact = DragonCombatChoreography.EnemyStart(kind) + DragonAnimationTiming.Contact(kind);
            AssertHeld(kind, enemyContact, rewards);
            string pose = DragonCombatChoreography.Counter(kind, 0, rewards);
            float counterContact = DragonCombatChoreography.CounterStart(kind) + FighterCombatTiming.Contact(pose);
            AssertHeld(kind, counterContact, rewards);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void ContactTelegraph_IsVisibleOnlyDuringFinalAnticipationWindow(int index)
        {
            var kind = (DragonKind)index; var rewards = new bool[5];
            float enemyContact = DragonCombatChoreography.EnemyStart(kind) + DragonAnimationTiming.Contact(kind);
            Assert.That(DragonCombatChoreography.Evaluate(kind, enemyContact - CombatFeedbackTiming.TelegraphSeconds - .001f, rewards).Telegraph, Is.Zero);
            Assert.That(DragonCombatChoreography.Evaluate(kind, enemyContact - .06f, rewards).Telegraph, Is.GreaterThan(0));
            Assert.That(DragonCombatChoreography.Evaluate(kind, enemyContact - .001f, rewards).Telegraph, Is.GreaterThan(.9f));
            Assert.That(DragonCombatChoreography.Evaluate(kind, enemyContact, rewards).Telegraph, Is.Zero);

            string pose = DragonCombatChoreography.Counter(kind, 0, rewards);
            float counterContact = DragonCombatChoreography.CounterStart(kind) + FighterCombatTiming.Contact(pose);
            Assert.That(DragonCombatChoreography.Evaluate(kind, counterContact - .06f, rewards).Telegraph, Is.GreaterThan(0));
            Assert.That(DragonCombatChoreography.Evaluate(kind, counterContact, rewards).Telegraph, Is.Zero);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void TrainingRhythm_UsesThreeDistinctStartAttackRecoveryRounds(int index)
        {
            var kind = (DragonKind)index;
            Assert.That(new[] { DragonTrainingChoreography.RoundSeconds(kind, 0), DragonTrainingChoreography.RoundSeconds(kind, 1), DragonTrainingChoreography.RoundSeconds(kind, 2) }.Distinct().Count(), Is.EqualTo(3));
            for (int round = 0; round < 3; round++)
            {
                float contact = DragonTrainingChoreography.ContactTime(kind, round);
                var strike = DragonTrainingChoreography.Evaluate(kind, contact);
                Assert.That(strike.Round, Is.EqualTo(round));
                Assert.That(strike.Pose, Is.EqualTo("attack"));
                Assert.That(strike.PoseSeconds, Is.EqualTo(DragonAnimationTiming.Contact(kind)).Within(.001f));
            }
            float cycle = DragonTrainingChoreography.SuperCycleSeconds(kind);
            Assert.That(DragonTrainingChoreography.Evaluate(kind, 0).Pose, Is.EqualTo("idle"));
            Assert.That(DragonTrainingChoreography.Evaluate(kind, cycle).Pose, Is.EqualTo("idle"));
            Assert.That(DragonTrainingChoreography.Evaluate(kind, cycle).Round, Is.Zero);
        }
        [Test]
        public void TrainingIdleVariants_UseDifferentStablePlaybackRates()
        {
            float a = DragonTrainingChoreography.IdleSampleSeconds(1, 0);
            float b = DragonTrainingChoreography.IdleSampleSeconds(1, 1);
            float c = DragonTrainingChoreography.IdleSampleSeconds(1, 2);
            Assert.That(new[] { a, b, c }.Distinct().Count(), Is.EqualTo(3));
            for (int round = 0; round < 3; round++)
                Assert.That(DragonTrainingChoreography.IdleSampleSeconds(1.01f, round), Is.GreaterThan(DragonTrainingChoreography.IdleSampleSeconds(1, round)));
        }
        private static void AssertHeld(DragonKind kind, float at, bool[] rewards)
        {
            var contact = DragonCombatChoreography.Evaluate(kind, at + .001f, rewards);
            Assert.That(contact.HitStopSeconds, Is.GreaterThan(0));
            var a = DragonCombatChoreography.Evaluate(kind, at + contact.HitStopSeconds * .25f, rewards);
            var b = DragonCombatChoreography.Evaluate(kind, at + contact.HitStopSeconds * .75f, rewards);
            Assert.That(a.Held && b.Held, Is.True);
            Assert.That(b.FighterSeconds, Is.EqualTo(a.FighterSeconds).Within(.0001));
            Assert.That(b.DragonSeconds, Is.EqualTo(a.DragonSeconds).Within(.0001));
            Assert.That(b.FighterTravel, Is.EqualTo(a.FighterTravel).Within(.0001));
            Assert.That(b.FighterOffset, Is.EqualTo(a.FighterOffset));
            Assert.That(b.DragonOffset, Is.EqualTo(a.DragonOffset));
        }
        [TestCase(0, 30)] [TestCase(1, 30)] [TestCase(2, 30)] [TestCase(3, 30)] [TestCase(4, 30)]
        [TestCase(0, 60)] [TestCase(1, 60)] [TestCase(2, 60)] [TestCase(3, 60)] [TestCase(4, 60)]
        public void ContactAudioGate_FiresExactlyTwicePerExchange(int index, int fps)
        {
            var kind = (DragonKind)index; var gate = new FighterContactGate(); var rewards = new bool[5]; int contacts = 0;
            float duration = DragonCombatChoreography.SequenceSeconds(kind) * 4;
            for (int frame = 0; frame / (float)fps < duration; frame++)
            {
                float elapsed = frame / (float)fps;
                var f = DragonCombatChoreography.Evaluate(kind, elapsed, rewards);
                if (f.ContactKey >= 0 && gate.TryContact(elapsed, f.ContactKey, f.ContactAt)) contacts++;
            }
            Assert.That(contacts, Is.EqualTo(24));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void AnimationKeys_UsePersistentFramesAndActualAttackContact(int index)
        {
            var kind = (DragonKind)index;
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(DragonAnimationAuthoring.DragonControllerPath(index));
            Assert.That(controller.animationClips.Length, Is.EqualTo(index == 0 ? 11 : 6));
            foreach (string pose in DragonAnimationTiming.Poses)
            {
                var clip = controller.animationClips.Single(c => c.name == "dragon_" + index + "_" + pose);
                var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                for (int frame = 0; frame < keys.Length; frame++)
                {
                    Assert.That(keys[frame].time, Is.EqualTo(DragonAnimationTiming.FrameTime(kind, pose, frame)).Within(.001));
                    Assert.That(((Sprite)keys[frame].value).name, Is.EqualTo("dragon_" + index + "_" + pose + "_" + DragonAnimationTiming.SpriteFrame(pose, frame)));
                    Assert.That(AssetDatabase.GetAssetPath(keys[frame].value), Is.EqualTo(ForestDragonReferenceUpgrade.SheetRoot + "/dragon_" + index + ".png"));
                }
                if (pose == "attack") Assert.That(keys[DragonAnimationTiming.ContactFrame(kind)].time, Is.EqualTo(DragonAnimationTiming.Contact(kind)).Within(.001));
            }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void Sheets_HaveThirtySixFramesAndOnePortraitWithSafeImportSettings(int index)
        {
            string path = ForestDragonReferenceUpgrade.SheetRoot + "/dragon_" + index + ".png";
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            Assert.That(sprites.Length, Is.EqualTo(37));
            Assert.That(sprites.Select(s => s.name).Distinct().Count(), Is.EqualTo(37));
            Assert.That(sprites.All(s => s.texture.width == 1254 && s.texture.height == 1254), Is.True);
            Assert.That(sprites.All(s => s.vertices.Length >= 4 && s.triangles.Length >= 6), Is.True);
            Assert.That(sprites.Where(s => !s.name.EndsWith("_portrait")).All(s => s.pivot.y == 0), Is.True);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled || importer.isReadable, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(System.IO.File.ReadAllBytes(path), Is.EqualTo(System.IO.File.ReadAllBytes("Assets/DragonMMA/ArtSource/ForestDragons/dragon_" + index + "_sheet.png")));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void PreparationAndAttack_EndFramesAreVisibleBeforePhaseSwitch(int index)
        {
            var kind = (DragonKind)index;
            foreach (string pose in new[] { "windup", "attack" })
            {
                Assert.That(DragonAnimationTiming.FrameTime(kind, pose, 5), Is.LessThan(DragonAnimationTiming.Duration(kind, pose) - .04f));
                Assert.That(DragonAnimationTiming.FrameTime(kind, pose, 6), Is.EqualTo(DragonAnimationTiming.Duration(kind, pose)));
                Assert.That(DragonAnimationTiming.SpriteFrame(pose, 6), Is.EqualTo(5));
                Assert.That(DragonAnimationTiming.Loops(pose), Is.False);
            }
        }
        [TestCase("OverlayRoot")] [TestCase("WebOverlayRoot")]
        public void UiAndActorPortraits_UseNewSheetsAndColorKeySafeMaterial(string name)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DragonMMA/Prefabs/UI/" + name + ".prefab");
            var view = new SerializedObject(root.GetComponent<DragonMmaView>());
            var portraits = view.FindProperty("dragonPortraits");
            Assert.That(portraits.arraySize, Is.EqualTo(5));
            for (int i = 0; i < 5; i++)
            {
                var portrait = (Sprite)portraits.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(portrait.name, Is.EqualTo(i == 0 ? "dragon_0_fight_idle_0" : "dragon_" + i + "_portrait"));
                Assert.That(AssetDatabase.GetAssetPath(portrait), Is.EqualTo(i == 0
                    ? "Assets/Resources/DragonMMA/DragonSheets/dragon_0_fight_idle.png"
                    : ForestDragonReferenceUpgrade.SheetRoot + "/dragon_" + i + ".png"));
            }
            foreach (var image in root.GetComponentsInChildren<Image>(true).Where(i => i.sprite != null && i.sprite.name.StartsWith("dragon_")))
            {
                Assert.That(AssetDatabase.GetAssetPath(image.sprite), Does.StartWith(ForestDragonReferenceUpgrade.SheetRoot));
                Assert.That(image.material.shader.name, Is.EqualTo("DragonMMA/Forest Dragon UI"));
                Assert.That(image.useSpriteMesh, Is.True);
                Assert.That(image.material.GetFloat("_RGBFloor"), Is.GreaterThan(0));
            }
        }
        [TestCase("OverlayRoot")] [TestCase("WebOverlayRoot")]
        public void Battle_AllDragonPosesFitBetweenGroundAndHud_NameplatesClearFeet(string prefab)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DragonMMA/Prefabs/UI/" + prefab + ".prefab");
            var so = new SerializedObject(root.GetComponent<DragonMmaView>());
            var actor = (DragonActorView)so.FindProperty("dragon").objectReferenceValue;
            var scales = new SerializedObject(actor).FindProperty("dragonPixelScales");
            var bindings = root.GetComponent<DragonUiBindings>();
            float floor = ((RectTransform)actor.transform).anchoredPosition.y;
            float hudBottom = bindings.Get<RectTransform>("Hunt/Battle status").anchoredPosition.y;
            for (int kind = 0; kind < 5; kind++)
            {
                float max = AssetDatabase.LoadAllAssetsAtPath(ForestDragonReferenceUpgrade.SheetRoot + "/dragon_" + kind + ".png")
                    .OfType<Sprite>().Max(s => s.rect.height);
                Assert.That(floor + max * scales.GetArrayElementAtIndex(kind).floatValue, Is.LessThan(hudBottom - 1), "species " + kind);
            }
            var plate = bindings.Get<RectTransform>("Hunt/Dragon name plate");
            Assert.That(plate.anchoredPosition.y + plate.rect.height, Is.LessThan(floor - 5));
            var fighter = (DragonActorView)so.FindProperty("hunter").objectReferenceValue;
            var visual = fighter.Image.rectTransform;
            float fighterTop = ((RectTransform)fighter.transform).anchoredPosition.y + visual.rect.height * (1 - visual.pivot.y);
            Assert.That(fighterTop, Is.LessThan(hudBottom - 1), "Victory hands must not be hidden under the HUD.");
        }
    }
}
