using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DragonMMA
{
    public enum DragonKind { Baby, Headbutt, Logtail, Stonehorn, Giant }
    public enum HuntPhase { Walking, Fighting, Result, Recovery }

    [Serializable]
    public sealed class DragonSpec
    {
        public DragonKind kind;
        public string displayName;
        public int weight, power, salePrice, coreReward, powerReward;
        public float saleSeconds, trainingSeconds;
        public string skillName;
        public DragonSpec(DragonKind kind, string name, int weight, int power, int salePrice,
            float saleSeconds, int coreReward, float trainingSeconds, string skillName, int powerReward)
        {
            this.kind = kind; displayName = name; this.weight = weight; this.power = power;
            this.salePrice = salePrice; this.saleSeconds = saleSeconds; this.coreReward = coreReward;
            this.trainingSeconds = trainingSeconds; this.skillName = skillName; this.powerReward = powerReward;
        }
    }

    public static class DragonCatalog
    {
        private static readonly DragonSpec[] Specs =
        {
            new DragonSpec(DragonKind.Baby, "숲 새끼용", 45, 7, 40, 30f, 5, 30f, "기초 체력", 1),
            new DragonSpec(DragonKind.Headbutt, "박치기용", 40, 10, 70, 60f, 10, 120f, "Power Punch", 2),
            new DragonSpec(DragonKind.Logtail, "통나무꼬리용", 25, 13, 120, 180f, 20, 300f, "Power Punch II", 3),
            new DragonSpec(DragonKind.Stonehorn, "돌뿔용", 15, 17, 220, 480f, 40, 600f, "Dragon Clinch", 5),
            new DragonSpec(DragonKind.Giant, "숲 거구룡", 5, 22, 500, 900f, 100, 1200f, "Giant Takedown", 8)
        };
        public static int Count => Specs.Length;
        public static IReadOnlyList<DragonSpec> All => Specs;
        public static DragonSpec Get(DragonKind kind) => Specs[(int)kind];
    }

    public static class DragonMmaRules
    {
        public const float CheerPerInput = 0.2f;
        public const float MaxCheerBonus = 10f;
        public const int CartCapacity = 3;
        public const int TrainingUnlockCost = 80;
        public const float WalkSeconds = 10f;
        public const float BattleSeconds = 20f;
        public const float WinResultSeconds = 2f;
        public const float RecoverySeconds = 10f;
        public static readonly int[] PityEncounterLimits = { 0, 2, 10, 20, 60 };
        public static readonly float[] PitySecondLimits = { 0f, 60f, 300f, 600f, 1800f };

        public static float BaseWinChance(int fighterPower, int dragonPower) =>
            Mathf.Clamp(50f + (fighterPower - dragonPower) * 5f, 15f, 85f);
        public static float AddCheer(float currentBonus, int inputCount = 1) =>
            Mathf.Clamp(currentBonus + CheerPerInput * Mathf.Max(0, inputCount), 0f, MaxCheerBonus);
        public static float CurrentWinChance(int fighterPower, int dragonPower, float cheerBonus) =>
            Mathf.Min(95f, BaseWinChance(fighterPower, dragonPower) + Mathf.Clamp(cheerBonus, 0f, MaxCheerBonus));
        public static bool IsUnlocked(GameSaveData data, DragonKind kind) =>
            kind == DragonKind.Baby || data.captured[(int)kind - 1];

        // Counters begin at the previous species' first capture, never at global game start.
        public static DragonKind? GetPityDragon(GameSaveData data)
        {
            for (int i = 1; i < DragonCatalog.Count; i++)
                if (IsUnlocked(data, (DragonKind)i) && !data.discovered[i] &&
                    (data.pityEncounters[i] >= PityEncounterLimits[i] || data.pityElapsed[i] >= PitySecondLimits[i]))
                    return (DragonKind)i;
            return null;
        }
        public static DragonKind ChooseEncounter(System.Random random, GameSaveData data)
        {
            if (!data.discovered[0]) return DragonKind.Baby;
            DragonKind? pity = GetPityDragon(data);
            if (pity.HasValue) return pity.Value;
            int total = 0;
            for (int i = 0; i < DragonCatalog.Count; i++)
                if (IsUnlocked(data, (DragonKind)i)) total += DragonCatalog.Get((DragonKind)i).weight;
            int roll = random.Next(total);
            for (int i = 0; i < DragonCatalog.Count; i++)
            {
                if (!IsUnlocked(data, (DragonKind)i)) continue;
                roll -= DragonCatalog.Get((DragonKind)i).weight;
                if (roll < 0) return (DragonKind)i;
            }
            return DragonKind.Baby;
        }
        public static int ExtortionReward(DragonKind kind) =>
            (int)Math.Round(DragonCatalog.Get(kind).salePrice * 0.25, MidpointRounding.AwayFromZero);
        public static bool IsTimerComplete(long nowUtc, long endUtc) => endUtc > 0 && nowUtc >= endUtc;
    }

    [Serializable]
    public sealed class DragonInstance
    {
        public string id;
        public int kind;
        public bool coreRewardClaimed;
        public DragonKind Kind => (DragonKind)kind;
        public DragonInstance() { }
        public DragonInstance(DragonKind kind, bool coreRewardClaimed = false)
        { id = Guid.NewGuid().ToString("N"); this.kind = (int)kind; this.coreRewardClaimed = coreRewardClaimed; }
    }

    [Serializable]
    public sealed class TimedDragonSlot
    {
        public DragonInstance dragon;
        public long startUtc, endUtc;
        public bool completed, rewardApplied;
        // Migration only: old saved slots used a species index instead of an individual.
        public int dragonKind = -1;
        public bool IsEmpty => dragon == null;
        public DragonKind Kind => dragon != null ? dragon.Kind : (DragonKind)dragonKind;
        public void Clear()
        { dragon = null; dragonKind = -1; startUtc = 0; endUtc = 0; completed = false; rewardApplied = false; }
    }

    [Serializable]
    public sealed class GameSaveData
    {
        public int version = 2;
        public int coins, cores;
        public int fighterPower = 10;
        public int encounterCount;
        public float activeHuntSeconds;
        public bool[] discovered = new bool[5];
        public bool[] captured = new bool[5];
        public bool[] trainingRewardsClaimed = new bool[5];
        public int[] pityEncounters = new int[5];
        public float[] pityElapsed = new float[5];
        public List<DragonInstance> storage = new List<DragonInstance>();
        public List<DragonInstance> cart = new List<DragonInstance>();
        public TimedDragonSlot[] sales = { new TimedDragonSlot(), new TimedDragonSlot() };
        public TimedDragonSlot[] training = { new TimedDragonSlot() };
        public bool trainingUnlocked;
        public int currentEnemy = -1;
        public HuntPhase phase = HuntPhase.Walking;
        public float phaseRemaining = DragonMmaRules.WalkSeconds;
        public float cheerBonus, battleBaseChance, battleRoll;
        public bool lastBattleWon, lastWasExtortion;
        public bool overlayAlwaysOnTop = true;
        public int selectedMonitor;
        public long lastSavedUtc;

        // Retained only for one-time migration, and cleared after conversion.
        public int[] ownedCounts = new int[5];
        public List<int> cartKinds = new List<int>();
        public List<int> trainedDragons = new List<int>();
        public bool secondTrainingUnlocked;
        public List<TimedDragonSlot> pendingLegacyTraining = new List<TimedDragonSlot>();

        public void EnsureDefaults(DragonRuntimeConfig config = null)
        {
            discovered = Resize(discovered, 5); captured = Resize(captured, 5);
            trainingRewardsClaimed = Resize(trainingRewardsClaimed, 5);
            pityEncounters = Resize(pityEncounters, 5); pityElapsed = Resize(pityElapsed, 5);
            ownedCounts = Resize(ownedCounts, 5);
            if (storage == null) storage = new List<DragonInstance>();
            if (cart == null) cart = new List<DragonInstance>();
            if (cartKinds == null) cartKinds = new List<int>();
            if (trainedDragons == null) trainedDragons = new List<int>();
            if (pendingLegacyTraining == null) pendingLegacyTraining = new List<TimedDragonSlot>();
            sales = ResizeSlots(sales, 2);
            if (version < 2) MigrateV1();
            training = ResizeSlots(training, 1); version = 2;
            fighterPower = Math.Max(1, fighterPower); coins = Math.Max(0, coins); cores = Math.Max(0, cores);
            if (float.IsNaN(phaseRemaining) || float.IsInfinity(phaseRemaining)) phaseRemaining = 10f;
            if (!Enum.IsDefined(typeof(HuntPhase), phase)) phase = HuntPhase.Walking;
            if (currentEnemy < -1 || currentEnemy >= DragonCatalog.Count ||
                (phase == HuntPhase.Fighting && currentEnemy < 0))
            { currentEnemy = -1; phase = HuntPhase.Walking; phaseRemaining = 10f; }
            cheerBonus = Mathf.Clamp(cheerBonus, 0f, config != null ? config.MaxCheerBonus : DragonMmaRules.MaxCheerBonus);
            var identities = new HashSet<string>();
            RepairDragons(storage, identities); RepairDragons(cart, identities);
            RepairSlots(sales, identities); RepairSlots(training, identities);
            pendingLegacyTraining.RemoveAll(slot => slot == null);
            RepairSlots(pendingLegacyTraining.ToArray(), identities);
            int capacity = config != null ? config.CartCapacity : DragonMmaRules.CartCapacity;
            while (cart.Count > capacity)
            { storage.Add(cart[capacity]); cart.RemoveAt(capacity); }
        }

        private void MigrateV1()
        {
            for (int i = 0; i < 5; i++)
                for (int n = 0; n < Math.Max(0, ownedCounts[i]); n++) storage.Add(new DragonInstance((DragonKind)i));
            foreach (int kind in cartKinds) if (ValidKind(kind)) cart.Add(new DragonInstance((DragonKind)kind));
            foreach (int kind in trainedDragons) if (ValidKind(kind)) storage.Add(new DragonInstance((DragonKind)kind, true));
            if (training == null) training = new TimedDragonSlot[0];
            foreach (TimedDragonSlot slot in sales) MigrateSlot(slot, false);
            foreach (TimedDragonSlot slot in training) MigrateSlot(slot, true);
            trainingUnlocked = secondTrainingUnlocked || trainedDragons.Count > 0 || Array.Exists(trainingRewardsClaimed, value => value);
            for (int i = 0; i < training.Length; i++)
            {
                if (training[i] == null || training[i].IsEmpty) continue;
                trainingUnlocked = true;
                if (i > 0) pendingLegacyTraining.Add(training[i]);
            }
            foreach (DragonInstance dragon in storage) captured[dragon.kind] = true;
            foreach (DragonInstance dragon in cart) captured[dragon.kind] = true;
            foreach (TimedDragonSlot slot in sales) if (!slot.IsEmpty) captured[(int)slot.Kind] = true;
            foreach (TimedDragonSlot slot in training) if (slot != null && !slot.IsEmpty) captured[(int)slot.Kind] = true;
            for (int i = 0; i < 5; i++)
            { captured[i] |= trainingRewardsClaimed[i]; discovered[i] |= captured[i]; }
            if (phase == HuntPhase.Fighting && ValidKind(currentEnemy))
            {
                battleBaseChance = DragonMmaRules.BaseWinChance(fighterPower, DragonCatalog.Get((DragonKind)currentEnemy).power);
                battleRoll = (float)(new System.Random().NextDouble() * 100d);
            }
            ownedCounts = new int[5]; cartKinds.Clear(); trainedDragons.Clear(); secondTrainingUnlocked = false;
        }
        private static void MigrateSlot(TimedDragonSlot slot, bool isTraining)
        {
            if (slot == null) return;
            // JsonUtility may materialize missing nested reference fields as default objects.
            // In v1 the explicit legacy index, not the new reference, is authoritative.
            if (!ValidKind(slot.dragonKind)) { slot.dragon = null; return; }
            slot.dragon = new DragonInstance((DragonKind)slot.dragonKind);
            float duration = isTraining ? DragonCatalog.Get(slot.Kind).trainingSeconds : DragonCatalog.Get(slot.Kind).saleSeconds;
            slot.startUtc = slot.endUtc - (long)duration; slot.dragonKind = -1;
        }
        private static bool ValidKind(int kind) => kind >= 0 && kind < DragonCatalog.Count;
        private static void RepairDragons(List<DragonInstance> dragons, HashSet<string> identities)
        {
            dragons.RemoveAll(dragon => dragon == null || !ValidKind(dragon.kind));
            foreach (DragonInstance dragon in dragons) RepairIdentity(dragon, identities);
        }
        private static void RepairSlots(TimedDragonSlot[] slots, HashSet<string> identities)
        {
            foreach (TimedDragonSlot slot in slots)
                if (slot.dragon != null)
                {
                    // Empty serialized nested DTOs are not real captured individuals.
                    if (!ValidKind(slot.dragon.kind) || string.IsNullOrEmpty(slot.dragon.id)) slot.Clear();
                    else RepairIdentity(slot.dragon, identities);
                }
        }
        private static void RepairIdentity(DragonInstance dragon, HashSet<string> identities)
        {
            if (string.IsNullOrEmpty(dragon.id) || !identities.Add(dragon.id))
            { dragon.id = Guid.NewGuid().ToString("N"); identities.Add(dragon.id); }
        }
        private static T[] Resize<T>(T[] source, int size)
        {
            if (source != null && source.Length == size) return source;
            var result = new T[size];
            if (source != null) Array.Copy(source, result, Math.Min(source.Length, size));
            return result;
        }
        private static TimedDragonSlot[] ResizeSlots(TimedDragonSlot[] source, int size)
        {
            var result = new TimedDragonSlot[size];
            for (int i = 0; i < size; i++)
                result[i] = source != null && i < source.Length && source[i] != null ? source[i] : new TimedDragonSlot();
            return result;
        }
    }

    public static class DragonSaveSystem
    {
        public static string OverridePath { get; set; }
        public static string SavePath => string.IsNullOrEmpty(OverridePath)
            ? Path.Combine(Application.persistentDataPath, "dragon_mma_idle_save.json") : OverridePath;
        public static string LastLoadMessage { get; private set; }
        public static GameSaveData Load(DragonGameConfig config = null)
        {
            LastLoadMessage = null;
            GameSaveData data;
            if (TryRead(SavePath, out data, config))
            { ApplyOfflineCompletion(data, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), config); return data; }
            if (File.Exists(SavePath))
            { PreserveCorruptFile(SavePath); LastLoadMessage = "저장 파일을 별도 보관했습니다."; }
            if (TryRead(SavePath + ".bak", out data, config))
            {
                LastLoadMessage = "백업 저장 파일에서 진행 상황을 복구했습니다.";
                Debug.LogWarning(LastLoadMessage);
                ApplyOfflineCompletion(data, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), config); return data;
            }
            if (LastLoadMessage != null) Debug.LogWarning(LastLoadMessage + " 안전한 새 게임을 시작합니다.");
            return config != null ? config.CreateNewSave() : DragonRuntimeConfig.CreateDefault().CreateNewSave();
        }
        private static bool TryRead(string path, out GameSaveData data, DragonGameConfig config)
        {
            data = null; if (!File.Exists(path)) return false;
            try
            {
                string json = File.ReadAllText(path);
                if (json.IndexOf("\"version\"", StringComparison.Ordinal) < 0) return false;
                data = JsonUtility.FromJson<GameSaveData>(json);
                if (data == null || data.version < 1 || data.version > 2) return false;
                data.EnsureDefaults(config != null ? config.CreateRuntimeCopy() : null); return true;
            }
            catch (Exception exception)
            { Debug.LogWarning("저장 파일을 읽지 못했습니다: " + exception.Message); return false; }
        }
        private static void PreserveCorruptFile(string path)
        {
            try { File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"), false); }
            catch (IOException exception) { Debug.LogWarning("손상 저장 보관 실패: " + exception.Message); }
        }
        public static void Save(GameSaveData data, DragonRuntimeConfig config = null)
        {
            if (data == null) return;
            data.EnsureDefaults(config);
            data.lastSavedUtc = Math.Max(data.lastSavedUtc, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            string path = SavePath, directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(data, true));
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebAssembly's virtual filesystem has no Windows ReplaceFile API.
            // The template enables IDBFS auto-persistence after these writes.
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.Copy(temporary, path, true);
            File.Delete(temporary);
#else
            // Atomic replacement on Windows retains the previous revision if interrupted.
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak", true);
            else File.Move(temporary, path);
#endif
        }
        public static void ApplyOfflineCompletion(GameSaveData data, long nowUtc, DragonGameConfig config = null)
        {
            if (data == null) return;
            new DragonGameSession(data, null, config).ProcessTimers(nowUtc);
        }
    }
}
