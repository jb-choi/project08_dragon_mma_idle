using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonMMA
{
    /// <summary>Authoring data only. Every session takes a deep, validated runtime snapshot.</summary>
    [CreateAssetMenu(fileName = "DefaultGameConfig", menuName = "Dragon MMA/Game Balance")]
    public sealed class DragonGameConfig : ScriptableObject
    {
        [Header("Dragons · species order is fixed by Kind")]
        [SerializeField] private DragonSpec[] dragons = DragonRuntimeConfig.DefaultDragonSpecs();

        [Header("Hunting / seconds")]
        [SerializeField, Min(0.1f)] private float walkSeconds = 10f;
        [SerializeField, Min(0.1f)] private float battleSeconds = 20f;
        [SerializeField, Min(0.1f)] private float winResultSeconds = 2f;
        [SerializeField, Min(0.1f)] private float recoverySeconds = 10f;

        [Header("Win probability / percentage points")]
        [SerializeField, Range(0f, 100f)] private float baseChance = 50f;
        [SerializeField, Min(0f)] private float powerChanceStep = 5f;
        [SerializeField, Range(0f, 100f)] private float minChance = 15f;
        [SerializeField, Range(0f, 100f)] private float maxChance = 85f;
        [SerializeField, Min(0f)] private float cheerPerInput = 0.2f;
        [SerializeField, Range(0f, 100f)] private float maxCheerBonus = 10f;

        [Header("Economy")]
        [SerializeField, Min(1)] private int cartCapacity = 3;
        [SerializeField, Min(0)] private int trainingUnlockCost = 80;
        [Tooltip("Applies only to a new save. Existing player progress is preserved.")]
        [SerializeField, Min(0)] private int startingCoins;
        [Tooltip("Applies only to a new save. Existing player progress is preserved.")]
        [SerializeField, Min(1)] private int startingPower = 10;

        [Header("First appearance guarantee · Baby, Headbutt, Logtail, Stonehorn, Giant")]
        [Tooltip("Counted from previous species' first capture. Baby's index 0 is unused.")]
        [SerializeField] private int[] pityEncounterLimits = { 0, 2, 10, 20, 60 };
        [SerializeField] private float[] pitySecondLimits = { 0f, 60f, 300f, 600f, 1800f };

        public DragonRuntimeConfig CreateRuntimeCopy() => new DragonRuntimeConfig(dragons,
            walkSeconds, battleSeconds, winResultSeconds, recoverySeconds,
            baseChance, powerChanceStep, minChance, maxChance, cheerPerInput, maxCheerBonus,
            cartCapacity, trainingUnlockCost, startingCoins, startingPower, pityEncounterLimits, pitySecondLimits);

        public DragonSpec GetSpec(DragonKind kind) => CreateRuntimeCopy().GetSpec(kind);
        public GameSaveData CreateNewSave() => CreateRuntimeCopy().CreateNewSave();

        private void OnValidate()
        {
            // Normalize only during authoring, never from runtime code. Duplicate/missing kinds
            // recover the five stable save IDs instead of permitting an unusable encounter table.
            DragonRuntimeConfig validated = CreateRuntimeCopy();
            dragons = validated.CopyDragonSpecs();
            walkSeconds = validated.WalkSeconds; battleSeconds = validated.BattleSeconds;
            winResultSeconds = validated.WinResultSeconds; recoverySeconds = validated.RecoverySeconds;
            baseChance = validated.BaseChance; powerChanceStep = validated.PowerChanceStep;
            minChance = validated.MinChance; maxChance = validated.MaxChance;
            cheerPerInput = validated.CheerPerInput; maxCheerBonus = validated.MaxCheerBonus;
            cartCapacity = validated.CartCapacity; trainingUnlockCost = validated.TrainingUnlockCost;
            startingCoins = validated.StartingCoins; startingPower = validated.StartingPower;
            pityEncounterLimits = validated.CopyPityEncounterLimits(); pitySecondLimits = validated.CopyPitySecondLimits();
        }
    }

    /// <summary>Session-owned data, without Unity assets or allocations during the game loop.</summary>
    public sealed class DragonRuntimeConfig
    {
        private readonly DragonSpec[] dragons;
        private readonly int[] pityEncounterLimits;
        private readonly float[] pitySecondLimits;
        public IReadOnlyList<DragonSpec> Dragons => dragons;
        public int DragonCount => dragons.Length;
        public float WalkSeconds { get; }
        public float BattleSeconds { get; }
        public float WinResultSeconds { get; }
        public float RecoverySeconds { get; }
        public float BaseChance { get; }
        public float PowerChanceStep { get; }
        public float MinChance { get; }
        public float MaxChance { get; }
        public float CheerPerInput { get; }
        public float MaxCheerBonus { get; }
        public float FinalChanceCap => Mathf.Min(100f, MaxChance + MaxCheerBonus);
        public int CartCapacity { get; }
        public int TrainingUnlockCost { get; }
        public int StartingCoins { get; }
        public int StartingPower { get; }

        internal DragonRuntimeConfig(DragonSpec[] source, float walkSeconds, float battleSeconds,
            float winResultSeconds, float recoverySeconds, float baseChance, float powerChanceStep,
            float minChance, float maxChance, float cheerPerInput, float maxCheerBonus,
            int cartCapacity, int trainingUnlockCost, int startingCoins, int startingPower,
            int[] encounterLimits, float[] secondLimits)
        {
            dragons = DefaultDragonSpecs();
            var accepted = new bool[dragons.Length];
            if (source != null)
                foreach (DragonSpec spec in source)
                {
                    if (spec == null || (int)spec.kind < 0 || (int)spec.kind >= dragons.Length || accepted[(int)spec.kind]) continue;
                    int index = (int)spec.kind; accepted[index] = true;
                    DragonSpec fallback = dragons[index];
                    dragons[index] = new DragonSpec(spec.kind,
                        string.IsNullOrWhiteSpace(spec.displayName) ? fallback.displayName : spec.displayName,
                        Mathf.Clamp(spec.weight, 1, 1000000), Mathf.Clamp(spec.power, 1, 1000000),
                        Mathf.Clamp(spec.salePrice, 0, 1000000), Finite(spec.saleSeconds, fallback.saleSeconds, 1f, 31536000f),
                        Mathf.Clamp(spec.coreReward, 0, 1000000), Finite(spec.trainingSeconds, fallback.trainingSeconds, 1f, 31536000f),
                        string.IsNullOrWhiteSpace(spec.skillName) ? fallback.skillName : spec.skillName,
                        Mathf.Clamp(spec.powerReward, 0, 1000000));
                }
            WalkSeconds = Finite(walkSeconds, 10f, 0.1f, 86400f);
            BattleSeconds = Finite(battleSeconds, 20f, 0.1f, 86400f);
            WinResultSeconds = Finite(winResultSeconds, 2f, 0.1f, 86400f);
            RecoverySeconds = Finite(recoverySeconds, 10f, 0.1f, 86400f);
            BaseChance = Finite(baseChance, 50f, 0f, 100f);
            PowerChanceStep = Finite(powerChanceStep, 5f, 0f, 100f);
            MinChance = Finite(minChance, 15f, 0f, 100f);
            MaxChance = Finite(maxChance, 85f, MinChance, 100f);
            CheerPerInput = Finite(cheerPerInput, 0.2f, 0f, 100f);
            MaxCheerBonus = Finite(maxCheerBonus, 10f, 0f, 100f);
            CartCapacity = Mathf.Clamp(cartCapacity, 1, 100);
            TrainingUnlockCost = Mathf.Clamp(trainingUnlockCost, 0, 1000000);
            StartingCoins = Mathf.Clamp(startingCoins, 0, 1000000);
            StartingPower = Mathf.Clamp(startingPower, 1, 1000000);
            pityEncounterLimits = new[] { 0, 2, 10, 20, 60 };
            pitySecondLimits = new[] { 0f, 60f, 300f, 600f, 1800f };
            for (int i = 1; i < dragons.Length; i++)
            {
                if (encounterLimits != null && i < encounterLimits.Length)
                    pityEncounterLimits[i] = Mathf.Clamp(encounterLimits[i], 1, 1000000);
                if (secondLimits != null && i < secondLimits.Length)
                    pitySecondLimits[i] = Finite(secondLimits[i], pitySecondLimits[i], 0.1f, 31536000f);
            }
        }

        public static DragonRuntimeConfig CreateDefault() => new DragonRuntimeConfig(null,
            10f, 20f, 2f, 10f, 50f, 5f, 15f, 85f, 0.2f, 10f, 3, 80, 0, 10, null, null);

        public DragonSpec GetSpec(DragonKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= dragons.Length) throw new ArgumentOutOfRangeException(nameof(kind));
            return dragons[index];
        }
        public int GetPityEncounterLimit(DragonKind kind) => pityEncounterLimits[(int)kind];
        public float GetPitySecondLimit(DragonKind kind) => pitySecondLimits[(int)kind];
        public float CalculateBaseWinChance(int fighterPower, int dragonPower) =>
            Mathf.Clamp(BaseChance + ((float)fighterPower - dragonPower) * PowerChanceStep, MinChance, MaxChance);
        public float AddCheer(float bonus, int count) =>
            Mathf.Clamp(bonus + CheerPerInput * Mathf.Max(0, count), 0f, MaxCheerBonus);
        public int ExtortionReward(DragonKind kind) =>
            (int)Math.Round(GetSpec(kind).salePrice * 0.25, MidpointRounding.AwayFromZero);
        public GameSaveData CreateNewSave()
        {
            var data = new GameSaveData { coins = StartingCoins, fighterPower = StartingPower, phaseRemaining = WalkSeconds };
            data.EnsureDefaults(this); return data;
        }

        internal static DragonSpec[] DefaultDragonSpecs()
        {
            var result = new DragonSpec[DragonCatalog.Count];
            for (int i = 0; i < result.Length; i++) result[i] = Copy(DragonCatalog.Get((DragonKind)i));
            return result;
        }
        internal DragonSpec[] CopyDragonSpecs()
        {
            var result = new DragonSpec[dragons.Length];
            for (int i = 0; i < result.Length; i++) result[i] = Copy(dragons[i]);
            return result;
        }
        internal int[] CopyPityEncounterLimits() => (int[])pityEncounterLimits.Clone();
        internal float[] CopyPitySecondLimits() => (float[])pitySecondLimits.Clone();
        private static DragonSpec Copy(DragonSpec spec) => new DragonSpec(spec.kind, spec.displayName,
            spec.weight, spec.power, spec.salePrice, spec.saleSeconds, spec.coreReward, spec.trainingSeconds, spec.skillName, spec.powerReward);
        private static float Finite(float value, float fallback, float minimum, float maximum) =>
            Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, minimum, maximum);
    }
}
