using NUnit.Framework;
using UnityEngine;

namespace DragonMMA.Tests
{
    public sealed class HuntPresentationTests
    {
        private static HuntPresentation.Frame Evaluate(GameSaveData data, DragonRuntimeConfig rules, bool profile = true) =>
            HuntPresentation.Evaluate(data, rules, profile, 100f, 68f, 88f, 102f);

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void Projection_DoesNotMutateSessionOrRewards(int kind)
        {
            var rules = DragonRuntimeConfig.CreateDefault();
            var data = rules.CreateNewSave();
            data.currentEnemy = kind;
            data.cart.Add(new DragonInstance((DragonKind)kind));
            foreach (HuntPhase phase in new[] { HuntPhase.Walking, HuntPhase.Fighting, HuntPhase.Result, HuntPhase.Recovery })
                for (int n = 0; n < 20; n++)
                {
                    data.phase = phase; data.phaseRemaining = n / 10f;
                    string before = JsonUtility.ToJson(data);
                    Evaluate(data, rules);
                    Assert.That(JsonUtility.ToJson(data), Is.EqualTo(before));
                }
        }

        [TestCase(1.7f)] [TestCase(3.8f)] [TestCase(7.8f)] [TestCase(9.2f)]
        [TestCase(13.7f)] [TestCase(14.5f)] [TestCase(18f)]
        public void BeginnerProjection_PreservesApprovedContacts(float time)
        {
            var rules = DragonRuntimeConfig.CreateDefault();
            var data = rules.CreateNewSave();
            data.phase = HuntPhase.Fighting; data.currentEnemy = 0;
            data.phaseRemaining = rules.BattleSeconds - time - .00001f;
            var frame = Evaluate(data, rules);
            Assert.That(frame.BasicJab && frame.UsesContactProfile, Is.True);
            Assert.That(frame.Actors.FighterPose, Is.EqualTo("jab"));
            Assert.That(frame.Actors.DragonPose, Is.EqualTo("light_hit"));
            Assert.That(frame.Actors.ContactAt, Is.EqualTo(time).Within(.0001f));
            Assert.That(frame.FighterMotion, Is.EqualTo(Vector2.zero));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void OtherSpecies_KeepOriginalChoreographyAndSkillGating(int kind)
        {
            var rules = DragonRuntimeConfig.CreateDefault();
            var data = rules.CreateNewSave(); data.currentEnemy = kind; data.phase = HuntPhase.Fighting;
            foreach (bool unlocked in new[] { false, true })
                foreach (float t in new[] { .1f, .3f, .6f, 1.7f, 3f, 9f, 19.99f })
                {
                    for (int i = 0; i < 5; i++) data.trainingRewardsClaimed[i] = unlocked;
                    data.phaseRemaining = rules.BattleSeconds - t;
                    var frame = Evaluate(data, rules);
                    var expected = DragonCombatChoreography.Evaluate((DragonKind)kind, frame.BattleElapsed, data.trainingRewardsClaimed);
                    Assert.That(frame.Actors, Is.EqualTo(expected));
                    Assert.That(frame.UsesContactProfile, Is.False);
                }
        }

        [TestCase(true)] [TestCase(false)]
        public void BabyResult_UsesContinuityOnlyWhenProfileIsAvailable(bool profile)
        {
            var rules = DragonRuntimeConfig.CreateDefault();
            var data = rules.CreateNewSave(); data.currentEnemy = 0; data.phase = HuntPhase.Result;
            data.phaseRemaining = rules.WinResultSeconds;
            var frame = Evaluate(data, rules, profile);
            Assert.That(frame.BabyResolution, Is.EqualTo(profile));
            Assert.That(frame.Actors.FighterPose, Is.EqualTo(profile ? "fight_idle" : "victory"));
            Assert.That(frame.Actors.DragonPose, Is.EqualTo(profile ? "yield" : "defeat"));
            Assert.That(frame.Actors.FighterState, Is.EqualTo(CombatActorState.Victory));
        }

        [TestCase(.54f, false)] [TestCase(.8f, false)] [TestCase(.939f, false)] [TestCase(.941f, true)]
        public void Capture_HandoffRemainsAtArrival(float progress, bool transferred)
        {
            var rules = DragonRuntimeConfig.CreateDefault();
            var data = rules.CreateNewSave(); data.currentEnemy = 0; data.phase = HuntPhase.Result;
            data.phaseRemaining = rules.WinResultSeconds * (1 - progress);
            var frame = Evaluate(data, rules);
            Assert.That(frame.CaptureTransferred, Is.EqualTo(transferred));
            Assert.That(frame.TransportScale, Is.InRange(.35f, 1f));
            if (progress < .55f) Assert.That(frame.CarryProgress, Is.Zero);
        }

        [TestCase(0)] [TestCase(1)]
        public void FullCart_UsesFleeInsteadOfAnotherCapture(int kind)
        {
            var rules = DragonRuntimeConfig.CreateDefault();
            var data = rules.CreateNewSave(); data.currentEnemy = kind; data.phase = HuntPhase.Result;
            data.lastWasExtortion = true; data.phaseRemaining = rules.WinResultSeconds * .2f;
            var frame = Evaluate(data, rules);
            Assert.That(frame.RunAway, Is.True);
            Assert.That(frame.CarryProgress, Is.Zero);
            Assert.That(frame.CaptureTransferred, Is.False);
            Assert.That(frame.Actors.DragonPose, Is.EqualTo(kind == 0 ? "fight_idle" : "walk"));
            Assert.That(frame.DragonMotion.x, Is.GreaterThan(0));
        }

        [TestCase(.5f, 1f)] [TestCase(1.1f, 1f)] [TestCase(1.3f, .5f)] [TestCase(1.5f, 0f)] [TestCase(2f, 0f)]
        public void BabyRecovery_FadeContractIsUnchanged(float elapsed, float opacity)
        {
            Assert.That(HuntPresentation.BabyRecoveryOpacity(elapsed), Is.EqualTo(opacity).Within(.0001f));
        }

        [Test]
        public void Walking_KeepsIdleOpponentAndNoContact()
        {
            var rules = DragonRuntimeConfig.CreateDefault();
            var frame = Evaluate(rules.CreateNewSave(), rules);
            Assert.That(frame.Walking, Is.True);
            Assert.That(frame.Actors.FighterPose, Is.EqualTo("walk"));
            Assert.That(frame.Actors.ContactKey, Is.EqualTo(-1));
            Assert.That(frame.UsesContactProfile, Is.False);
        }

        [Test]
        public void MissingBabyProfile_KeepsLegacyFallback()
        {
            var rules = DragonRuntimeConfig.CreateDefault();
            var data = rules.CreateNewSave(); data.phase = HuntPhase.Fighting; data.currentEnemy = 0;
            data.phaseRemaining = 18;
            var frame = Evaluate(data, rules, false);
            Assert.That(frame.BasicJab, Is.False);
            Assert.That(frame.Actors, Is.EqualTo(DragonCombatChoreography.Evaluate(DragonKind.Baby, 2, data.trainingRewardsClaimed)));
        }
    }
}
