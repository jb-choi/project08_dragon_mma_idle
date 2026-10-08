using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace DragonMMA.Tests
{
    public sealed class DragonMmaRulesTests
    {
        private const long Now = 10000;

        private static DragonInstance Store(GameSaveData data, DragonKind kind)
        {
            var dragon = new DragonInstance(kind); data.storage.Add(dragon); return dragon;
        }

        [TestCase(10, 7, 65f)]
        [TestCase(10, 10, 50f)]
        [TestCase(10, 22, 15f)]
        [TestCase(30, 7, 85f)]
        public void BaseWinChance_UsesPowerDifferenceAndClamp(int fighter, int dragon, float expected)
        { Assert.That(DragonMmaRules.BaseWinChance(fighter, dragon), Is.EqualTo(expected)); }

        [Test]
        public void FreshGame_HasLockedTrainingAndNoFreeCoins()
        {
            var session = new DragonGameSession(new GameSaveData());
            Assert.That(session.Data.coins, Is.Zero);
            Assert.That(session.Data.fighterPower, Is.EqualTo(10));
            Assert.That(session.Data.trainingUnlocked, Is.False);
            Assert.That(session.Data.training.Length, Is.EqualTo(1));
            Assert.That(session.Data.sales.Length, Is.EqualTo(2));
        }

        [Test]
        public void Cheer_FiftyInputsReachMaximumWithoutRateLimit()
        {
            Assert.That(DragonMmaRules.AddCheer(0f), Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(DragonMmaRules.AddCheer(0f, 50), Is.EqualTo(10f).Within(0.001f));
            Assert.That(DragonMmaRules.AddCheer(10f, 100), Is.EqualTo(10f).Within(0.001f));
            Assert.That(DragonMmaRules.AddCheer(3f, -3), Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void CurrentWinChance_NeverExceedsNinetyFive()
        {
            Assert.That(DragonMmaRules.CurrentWinChance(10, 10, 7.4f), Is.EqualTo(57.4f).Within(0.001f));
            Assert.That(DragonMmaRules.CurrentWinChance(30, 7, 100f), Is.EqualTo(95f).Within(0.001f));
        }

        [Test]
        public void FirstEncounter_IsBabyAfterTenSeconds()
        {
            var session = new DragonGameSession(new GameSaveData(), new System.Random(22));
            session.Tick(9f, Now);
            Assert.That(session.Data.phase, Is.EqualTo(HuntPhase.Walking));
            session.Tick(1f, Now);
            Assert.That(session.Data.currentEnemy, Is.EqualTo((int)DragonKind.Baby));
            Assert.That(session.Data.phase, Is.EqualTo(HuntPhase.Fighting));
            Assert.That(session.Data.phaseRemaining, Is.EqualTo(20f));
        }

        [Test]
        public void EncounterGate_DiscoveryAloneDoesNotUnlockNextSpecies()
        {
            var data = new GameSaveData(); data.discovered[0] = true;
            data.activeHuntSeconds = 100000f; data.encounterCount = 2000;
            var random = new System.Random(10);
            for (int i = 0; i < 1000; i++)
                Assert.That(DragonMmaRules.ChooseEncounter(random, data), Is.EqualTo(DragonKind.Baby));
            Assert.That(DragonMmaRules.GetPityDragon(data), Is.Null);
        }

        [Test]
        public void EncounterWeights_OnlyIncludeUnlockedSpecies()
        {
            var data = new GameSaveData(); data.discovered[0] = true; data.captured[0] = true;
            int baby = 0, headbutt = 0;
            var random = new System.Random(44);
            for (int i = 0; i < 10000; i++)
            {
                DragonKind kind = DragonMmaRules.ChooseEncounter(random, data);
                Assert.That((int)kind, Is.LessThanOrEqualTo(1));
                if (kind == DragonKind.Baby) baby++; else headbutt++;
            }
            Assert.That((float)baby / headbutt, Is.InRange(1.02f, 1.24f));
        }

        [TestCase(DragonKind.Headbutt, 2, 60f)]
        [TestCase(DragonKind.Logtail, 10, 300f)]
        [TestCase(DragonKind.Stonehorn, 20, 600f)]
        [TestCase(DragonKind.Giant, 60, 1800f)]
        public void Pity_AppliesOnlyAfterPreviousFirstCapture(DragonKind kind, int encounters, float seconds)
        {
            var data = new GameSaveData(); int index = (int)kind;
            data.pityEncounters[index] = encounters; data.pityElapsed[index] = seconds;
            Assert.That(DragonMmaRules.GetPityDragon(data), Is.Null);
            data.captured[index - 1] = true;
            Assert.That(DragonMmaRules.GetPityDragon(data), Is.EqualTo(kind));
            data.pityElapsed[index] = 0f;
            Assert.That(DragonMmaRules.GetPityDragon(data), Is.EqualTo(kind));
            data.pityEncounters[index] = 0; data.pityElapsed[index] = seconds;
            Assert.That(DragonMmaRules.GetPityDragon(data), Is.EqualTo(kind));
            data.discovered[index] = true;
            Assert.That(DragonMmaRules.GetPityDragon(data), Is.Null);
        }

        [Test]
        public void FirstCapture_ResetsNextSpeciesPityInsteadOfUsingGlobalTime()
        {
            var data = new GameSaveData { activeHuntSeconds = 9999f };
            data.pityEncounters[1] = 99; data.pityElapsed[1] = 999f;
            var session = new DragonGameSession(data);
            session.DebugForceEncounter(DragonKind.Baby, 0f); session.Tick(20f, Now);
            Assert.That(data.captured[0], Is.True);
            Assert.That(data.pityEncounters[1], Is.Zero);
            Assert.That(data.pityElapsed[1], Is.Zero);
            session.Tick(12f, Now);
            Assert.That(data.pityEncounters[1], Is.EqualTo(1));
        }

        [Test]
        public void BaseChanceAndRoll_AreStableDuringBattle()
        {
            var data = new GameSaveData(); var session = new DragonGameSession(data);
            session.DebugForceEncounter(DragonKind.Headbutt, 70f);
            data.fighterPower += 100;
            session.Tick(10f, Now);
            Assert.That(session.CurrentWinChance, Is.EqualTo(50f));
            Assert.That(data.battleRoll, Is.EqualTo(70f));
        }

        [Test]
        public void LastFrameCheer_ChangesActualBattleResult()
        {
            var data = new GameSaveData(); var session = new DragonGameSession(data);
            session.DebugForceEncounter(DragonKind.Headbutt, 59.9f);
            session.Tick(19.99f, Now); session.Cheer(50); session.Tick(0.02f, Now);
            Assert.That(data.lastBattleWon, Is.True);
            Assert.That(data.cart.Count, Is.EqualTo(1));
            Assert.That(data.cores, Is.Zero, "Capturing never grants cores.");
        }

        [Test]
        public void Cheer_IsIgnoredOutsideUnresolvedCombat()
        {
            var data = new GameSaveData(); var session = new DragonGameSession(data);
            session.Cheer(50); Assert.That(data.cheerBonus, Is.Zero);
            session.DebugForceEncounter(DragonKind.Baby, 0); session.Tick(20, Now);
            session.Cheer(50); Assert.That(data.cheerBonus, Is.Zero);
        }

        [Test]
        public void Loss_RecoversInTenSecondsIncludingDefeatThenWalksTen()
        {
            var data = new GameSaveData(); var session = new DragonGameSession(data);
            session.DebugForceEncounter(DragonKind.Baby, 99f); session.Tick(20f, Now);
            Assert.That(data.phase, Is.EqualTo(HuntPhase.Recovery));
            Assert.That(data.phaseRemaining, Is.EqualTo(10f));
            session.Tick(10f, Now);
            Assert.That(data.phase, Is.EqualTo(HuntPhase.Walking));
            Assert.That(data.phaseRemaining, Is.EqualTo(10f));
            Assert.That(data.cart, Is.Empty);
        }

        [Test]
        public void ReturnDuringBattle_MovesOnlyLockedCargoAndNewCaptureUsesEmptyCart()
        {
            var data = new GameSaveData(); var session = new DragonGameSession(data);
            for (int i = 0; i < 3; i++) data.cart.Add(new DragonInstance(DragonKind.Baby));
            session.DebugForceEncounter(DragonKind.Headbutt);
            var departing = session.ReturnCart();
            Assert.That(data.storage.Count, Is.EqualTo(3));
            Assert.That(data.cart, Is.Empty);
            Assert.That(data.phase, Is.EqualTo(HuntPhase.Fighting));
            session.Tick(20f, Now);
            Assert.That(data.cart.Count, Is.EqualTo(1));
            Assert.That(data.cart[0].Kind, Is.EqualTo(DragonKind.Headbutt));
            Assert.That(departing.Count, Is.EqualTo(3));
            Assert.That(data.lastWasExtortion, Is.False);
        }

        [Test]
        public void FullCartWin_GrantsCoinsNotCaptureOrSpeciesUnlock()
        {
            var data = new GameSaveData(); var session = new DragonGameSession(data);
            for (int i = 0; i < 3; i++) data.cart.Add(new DragonInstance(DragonKind.Baby));
            session.DebugForceEncounter(DragonKind.Headbutt); session.Tick(20f, Now);
            Assert.That(data.coins, Is.EqualTo(18));
            Assert.That(data.cart.Count, Is.EqualTo(3));
            Assert.That(data.captured[1], Is.False);
            Assert.That(data.lastWasExtortion, Is.True);
        }

        [TestCase(DragonKind.Baby, 10)]
        [TestCase(DragonKind.Headbutt, 18)]
        [TestCase(DragonKind.Logtail, 30)]
        [TestCase(DragonKind.Stonehorn, 55)]
        [TestCase(DragonKind.Giant, 125)]
        public void Extortion_RoundsNearestInteger(DragonKind kind, int expected)
        { Assert.That(DragonMmaRules.ExtortionReward(kind), Is.EqualTo(expected)); }

        [Test]
        public void TrainingUnlock_ChargesEightyOnceAndRejectsInsufficientMoney()
        {
            var data = new GameSaveData { coins = 79 }; var session = new DragonGameSession(data);
            var dragon = Store(data, DragonKind.Baby);
            Assert.That(session.UnlockTraining(), Is.False);
            Assert.That(session.AssignTraining(dragon.id, Now), Is.False);
            data.coins = 80;
            Assert.That(session.UnlockTraining(), Is.True);
            Assert.That(data.coins, Is.Zero);
            Assert.That(session.UnlockTraining(), Is.False);
        }

        [Test]
        public void CompletedTraining_RetainsDragonAndPaysOnlyOnce()
        {
            var data = new GameSaveData { trainingUnlocked = true }; var session = new DragonGameSession(data);
            var dragon = Store(data, DragonKind.Headbutt);
            Assert.That(session.AssignTraining(dragon.id, Now), Is.True);
            session.ProcessTimers(Now + 119); Assert.That(data.cores, Is.Zero);
            session.ProcessTimers(Now + 120); session.ProcessTimers(Now + 10000);
            Assert.That(data.training[0].dragon, Is.SameAs(dragon));
            Assert.That(data.training[0].completed, Is.True);
            Assert.That(data.storage, Is.Empty);
            Assert.That(data.cores, Is.EqualTo(10));
            Assert.That(data.fighterPower, Is.EqualTo(12));
            Assert.That(session.ActiveSkill, Is.EqualTo("Power Punch"));
        }

        [Test]
        public void ReassigningTrainedIndividual_CannotFarmCoreReward()
        {
            var data = new GameSaveData { trainingUnlocked = true }; var session = new DragonGameSession(data);
            var dragon = Store(data, DragonKind.Baby);
            session.AssignTraining(dragon.id, Now); session.ProcessTimers(Now + 30);
            session.RecallTraining(); session.AssignTraining(dragon.id, Now + 40);
            session.ProcessTimers(Now + 10000);
            Assert.That(data.cores, Is.EqualTo(5));
            Assert.That(data.fighterPower, Is.EqualTo(11));
            Assert.That(data.training[0].completed, Is.True);
        }

        [Test]
        public void SecondIndividualSameSpecies_GrantsCoreNotDuplicatePower()
        {
            var data = new GameSaveData { trainingUnlocked = true }; var session = new DragonGameSession(data);
            var first = Store(data, DragonKind.Baby); var second = Store(data, DragonKind.Baby);
            session.AssignTraining(first.id, Now); session.ProcessTimers(Now + 30);
            session.AssignTraining(second.id, Now + 40); session.ProcessTimers(Now + 70);
            Assert.That(data.storage.Contains(first), Is.True);
            Assert.That(data.training[0].dragon, Is.SameAs(second));
            Assert.That(data.cores, Is.EqualTo(10));
            Assert.That(data.fighterPower, Is.EqualTo(11));
        }

        [Test]
        public void TrainingReplacement_PreservesOldDragonAndCancelsIncompleteReward()
        {
            var data = new GameSaveData { trainingUnlocked = true }; var session = new DragonGameSession(data);
            var first = Store(data, DragonKind.Giant); var second = Store(data, DragonKind.Baby);
            session.AssignTraining(first.id, Now); session.AssignTraining(second.id, Now + 1);
            session.ProcessTimers(Now + 31);
            Assert.That(data.storage.Contains(first), Is.True);
            Assert.That(first.coreRewardClaimed, Is.False);
            Assert.That(data.cores, Is.EqualTo(5));
        }

        [Test]
        public void Market_IndependentTimersAndExplicitSingleClaim()
        {
            var data = new GameSaveData(); var session = new DragonGameSession(data);
            var baby = Store(data, DragonKind.Baby); var giant = Store(data, DragonKind.Giant);
            Assert.That(session.RegisterSale(baby.id, 0, Now), Is.True);
            Assert.That(session.RegisterSale(giant.id, 1, Now), Is.True);
            Assert.That(session.ClaimSale(0), Is.False);
            session.ProcessTimers(Now + 30);
            Assert.That(data.sales[0].completed, Is.True);
            Assert.That(data.sales[1].completed, Is.False);
            Assert.That(data.coins, Is.Zero);
            Assert.That(session.ClaimSale(0), Is.True);
            Assert.That(session.ClaimSale(0), Is.False);
            Assert.That(data.coins, Is.EqualTo(40));
        }

        [Test]
        public void FacilityActions_CannotPlaceSameIndividualTwice()
        {
            var data = new GameSaveData { trainingUnlocked = true }; var session = new DragonGameSession(data);
            var dragon = Store(data, DragonKind.Baby);
            session.RegisterSale(dragon.id, 0, Now);
            Assert.That(session.RegisterSale(dragon.id, 1, Now), Is.False);
            Assert.That(session.AssignTraining(dragon.id, Now), Is.False);
            Assert.That(session.RegisterSale("missing", 1, Now), Is.False);
            Assert.That(session.ClaimSale(-1), Is.False);
        }

        [Test]
        public void OfflineCompletion_AppliesTrainingButNeverHuntingOrAutomaticSaleMoney()
        {
            var data = new GameSaveData { trainingUnlocked = true, activeHuntSeconds = 42f };
            var session = new DragonGameSession(data);
            session.DebugForceEncounter(DragonKind.Baby, 22f); session.Tick(8f, Now);
            var baby = Store(data, DragonKind.Baby); var headbutt = Store(data, DragonKind.Headbutt);
            session.RegisterSale(baby.id, 0, Now); session.AssignTraining(headbutt.id, Now);
            float huntTime = data.activeHuntSeconds;
            DragonSaveSystem.ApplyOfflineCompletion(data, Now + 1000000);
            Assert.That(data.sales[0].completed, Is.True);
            Assert.That(data.training[0].completed, Is.True);
            Assert.That(data.cores, Is.EqualTo(10));
            Assert.That(data.coins, Is.Zero);
            Assert.That(data.cart, Is.Empty);
            Assert.That(data.activeHuntSeconds, Is.EqualTo(huntTime));
            Assert.That(data.phaseRemaining, Is.EqualTo(12f));
            Assert.That(data.battleRoll, Is.EqualTo(22f));
        }

        [Test]
        public void ClockMovedBackwards_DoesNotCompleteTimersOrGrantRewards()
        {
            var data = new GameSaveData { trainingUnlocked = true }; var session = new DragonGameSession(data);
            var dragon = Store(data, DragonKind.Baby);
            session.AssignTraining(dragon.id, Now);
            data.lastSavedUtc = Now + 100;
            DragonSaveSystem.ApplyOfflineCompletion(data, Now + 40);
            Assert.That(data.cores, Is.Zero);
            Assert.That(data.training[0].completed, Is.False);
            DragonSaveSystem.ApplyOfflineCompletion(data, Now + 100);
            Assert.That(data.cores, Is.EqualTo(5));
        }

        [Test]
        public void NewTimerDuringBackwardClock_UsesLastValidTimestamp()
        {
            var data = new GameSaveData { lastSavedUtc = Now + 100 };
            var session = new DragonGameSession(data); var dragon = Store(data, DragonKind.Baby);
            session.RegisterSale(dragon.id, 0, Now);
            Assert.That(data.sales[0].startUtc, Is.EqualTo(Now + 100));
            Assert.That(data.sales[0].endUtc, Is.EqualTo(Now + 130));
        }

        [Test]
        public void Storage_HasNoCapacityLimitAndIndividualsHaveUniqueIds()
        {
            var data = new GameSaveData(); var session = new DragonGameSession(data);
            for (int i = 0; i < 1001; i++) Store(data, DragonKind.Baby);
            data.cart.Add(new DragonInstance(DragonKind.Baby)); session.ReturnCart(); data.EnsureDefaults();
            Assert.That(data.storage.Count, Is.EqualTo(1002));
            Assert.That(data.storage[0].id, Is.Not.EqualTo(data.storage[1].id));
        }

        [Test]
        public void V1Migration_PreservesCoinsDragonsSkillsAndActiveTimers()
        {
            const string json = "{\"version\":1,\"coins\":157,\"cores\":22,\"fighterPower\":13," +
                "\"ownedCounts\":[2,1,0,0,0],\"cartKinds\":[1],\"trainedDragons\":[0]," +
                "\"trainingRewardsClaimed\":[true,false,false,false,false],\"secondTrainingUnlocked\":true," +
                "\"sales\":[{\"dragonKind\":2,\"endUtc\":10500}]," +
                "\"training\":[{\"dragonKind\":1,\"endUtc\":10200},{\"dragonKind\":3,\"endUtc\":10300}]}";
            var data = JsonUtility.FromJson<GameSaveData>(json); data.EnsureDefaults();
            Assert.That(data.version, Is.EqualTo(2));
            Assert.That(data.coins, Is.EqualTo(157));
            Assert.That(data.storage.Count, Is.EqualTo(4));
            Assert.That(data.storage[3].coreRewardClaimed, Is.True);
            Assert.That(data.cart.Count, Is.EqualTo(1));
            Assert.That(data.sales[0].Kind, Is.EqualTo(DragonKind.Logtail));
            Assert.That(data.training.Length, Is.EqualTo(1));
            Assert.That(data.training[0].Kind, Is.EqualTo(DragonKind.Headbutt));
            Assert.That(data.pendingLegacyTraining.Count, Is.EqualTo(1));
            Assert.That(data.trainingUnlocked, Is.True);
            data.EnsureDefaults();
            Assert.That(data.storage.Count, Is.EqualTo(4), "Migration must be idempotent.");
            DragonSaveSystem.ApplyOfflineCompletion(data, 10300);
            Assert.That(data.cores, Is.EqualTo(72));
            Assert.That(data.fighterPower, Is.EqualTo(20));
            Assert.That(data.storage.Count, Is.EqualTo(5));
            Assert.That(data.pendingLegacyTraining, Is.Empty);
        }

        [Test]
        public void SaveRepair_PreservesOverflowCargoAndRepairsMissingSlotArrays()
        {
            var data = new GameSaveData { sales = null, training = null, captured = null };
            for (int i = 0; i < 5; i++) data.cart.Add(new DragonInstance(DragonKind.Baby));
            data.EnsureDefaults();
            Assert.That(data.cart.Count, Is.EqualTo(3));
            Assert.That(data.storage.Count, Is.EqualTo(2));
            Assert.That(data.sales.Length, Is.EqualTo(2));
            Assert.That(data.training.Length, Is.EqualTo(1));
            Assert.That(data.captured.Length, Is.EqualTo(5));
        }
    }

    public sealed class DragonMmaSaveTests
    {
        private string directory;
        private string previousOverride;
        [SetUp]
        public void Setup()
        {
            previousOverride = DragonSaveSystem.OverridePath;
            directory = Path.Combine(Path.GetTempPath(), "DragonMmaTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            DragonSaveSystem.OverridePath = Path.Combine(directory, "save.json");
        }
        [TearDown]
        public void Teardown()
        {
            DragonSaveSystem.OverridePath = previousOverride;
            // This directory was uniquely created by this test and contains no player data.
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        [Test]
        public void AtomicSave_RoundTripsAndRetainsPreviousRevision()
        {
            var data = new GameSaveData { coins = 123 };
            var dragon = new DragonInstance(DragonKind.Giant, true); data.storage.Add(dragon);
            DragonSaveSystem.Save(data); data.coins = 456; DragonSaveSystem.Save(data);
            var loaded = DragonSaveSystem.Load();
            Assert.That(loaded.coins, Is.EqualTo(456));
            Assert.That(loaded.storage[0].id, Is.EqualTo(dragon.id));
            Assert.That(loaded.storage[0].coreRewardClaimed, Is.True);
            var backup = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(DragonSaveSystem.SavePath + ".bak"));
            Assert.That(backup.coins, Is.EqualTo(123));
            Assert.That(File.Exists(DragonSaveSystem.SavePath + ".tmp"), Is.False);
            Assert.That(loaded.sales[0].IsEmpty, Is.True);
            Assert.That(loaded.sales[1].IsEmpty, Is.True);
            Assert.That(loaded.training[0].IsEmpty, Is.True);
        }
        [Test]
        public void CorruptPrimary_RecoversBackupAndKeepsCorruptEvidence()
        {
            var data = new GameSaveData { coins = 99 };
            DragonSaveSystem.Save(data); data.coins = 120; DragonSaveSystem.Save(data);
            File.WriteAllText(DragonSaveSystem.SavePath, "{}");
            var loaded = DragonSaveSystem.Load();
            Assert.That(loaded.coins, Is.EqualTo(99));
            Assert.That(Directory.GetFiles(directory, "*.corrupt-*").Length, Is.EqualTo(1));
            Assert.That(DragonSaveSystem.LastLoadMessage, Does.Contain("복구"));
        }
    }
}
