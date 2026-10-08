using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DragonMMA.Tests
{
    public sealed class BabyFirstBattleTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void CompleteFight_HasSevenJabsAndNoFakeThreatContact(int fps)
        {
            var gate = new FighterContactGate(); int contacts = 0;
            for (int n = 0; n <= 20 * fps; n++)
            {
                float t = n / (float)fps; var f = BabyFirstBattle.Evaluate(t);
                Assert.That(f.Powered, Is.False);
                Assert.That(new[] { "fight_idle", "jab", "first_guard" }, Does.Contain(f.FighterPose));
                Assert.That(new[] { "fight_idle", "light_hit", "threat" }, Does.Contain(f.DragonPose));
                Assert.That(f.FighterOffset, Is.EqualTo(Vector2.zero));
                Assert.That(f.DragonOffset, Is.EqualTo(Vector2.zero));
                if (f.Defending) { Assert.That(f.ContactKey, Is.EqualTo(-1)); Assert.That(f.Hit, Is.False); }
                if (f.ContactKey >= 0 && gate.TryContact(t, f.ContactKey, f.ContactAt)) contacts++;
            }
            Assert.That(contacts, Is.EqualTo(7));
        }
        [TestCase(5.8f)] [TestCase(12f)]
        public void ReadableThreat_StopsPunchingAndKeepsGuard(float t)
        {
            var f = BabyFirstBattle.Evaluate(t);
            Assert.That(f.DragonPose, Is.EqualTo("threat"));
            Assert.That(f.FighterPose, Is.EqualTo("first_guard"));
            Assert.That(f.FighterState, Is.EqualTo(CombatActorState.Guard));
            Assert.That(f.DragonSeconds, Is.InRange(0f, .5f));
        }
        [TestCase(1.7f)] [TestCase(3.8f)] [TestCase(7.8f)] [TestCase(9.2f)]
        [TestCase(13.7f)] [TestCase(14.5f)] [TestCase(18f)]
        public void AuthoredContacts_PreserveApprovedJabAndImmediateLightHit(float t)
        {
            Assert.That(BabyFirstBattle.Evaluate(t - .001f).DragonPose, Is.EqualTo("fight_idle"));
            var f = BabyFirstBattle.Evaluate(t + .00001f);
            Assert.That(f.FighterPose, Is.EqualTo("jab"));
            Assert.That(f.DragonPose, Is.EqualTo("light_hit"));
            Assert.That(f.FighterSeconds, Is.EqualTo(.2f).Within(.0001f));
            Assert.That(f.DragonSeconds, Is.EqualTo(0).Within(.0001f));
        }
        [Test]
        public void Ending_DoesNotPretendTheProbabilisticFightWasWon()
        {
            foreach (float t in new[] { 19f, 20f, 25f })
            {
                var f = BabyFirstBattle.Evaluate(t);
                Assert.That(f.FighterPose, Is.EqualTo("fight_idle"));
                Assert.That(f.DragonPose, Is.EqualTo("fight_idle"));
                Assert.That(f.ContactKey, Is.EqualTo(-1));
            }
        }
        [TestCase(10f)] [TestCase(30f)]
        public void ConfiguredBattleDuration_MapsContactAndEnding(float duration)
        {
            float t = duration * (1.7f / 20f);
            var f = BabyFirstBattle.Evaluate(t + .00001f, duration);
            Assert.That(f.ContactAt, Is.EqualTo(t).Within(.0001f));
            Assert.That(f.DragonPose, Is.EqualTo("light_hit"));
            Assert.That(BabyFirstBattle.Evaluate(duration, duration).ContactKey, Is.EqualTo(-1));
        }
        [Test]
        public void ContactJump_MovesForwardAndStopsAtEnd()
        {
            Assert.That(BabyFirstBattle.NextContact(0), Is.EqualTo(1.7f).Within(.0001f));
            Assert.That(BabyFirstBattle.NextContact(1.7f), Is.EqualTo(3.8f).Within(.0001f));
            Assert.That(BabyFirstBattle.NextContact(18f), Is.EqualTo(20f));
        }
        [TestCase("fighter_first_guard", .45f, 96f)] [TestCase("dragon_0_yield", .9f, 52f)]
        public void ContinuityClips_AreIndependentRegisteredPersistentAssets(string name,float duration,float pivotX)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/DragonMMA/Animations/"+name+".anim");
            Assert.That(clip,Is.Not.Null);
            var keys=AnimationUtility.GetObjectReferenceCurve(clip,AnimationUtility.GetObjectReferenceCurveBindings(clip).Single());
            Assert.That(keys.Length,Is.EqualTo(8));Assert.That(keys.Last().time,Is.EqualTo(duration).Within(.001f));
            foreach(var key in keys)
            {
                var sprite=(Sprite)key.value;Assert.That(sprite,Is.Not.Null);
                Assert.That(sprite.name,Does.StartWith(name+"_"));
                Assert.That(sprite.pivot,Is.EqualTo(new Vector2(pivotX,8)));
                Assert.That(AssetDatabase.Contains(sprite),Is.True);
            }
        }
    }
}
