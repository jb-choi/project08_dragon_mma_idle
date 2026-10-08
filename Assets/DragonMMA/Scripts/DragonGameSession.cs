using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonMMA
{
    /// <summary>Pure state machine; Unity UI, input, animation and persistence are adapters.</summary>
    public sealed class DragonGameSession
    {
        public GameSaveData Data { get; }
        public DragonRuntimeConfig Config { get; }
        public DragonSpec GetSpec(DragonKind kind) => Config.GetSpec(kind);
        public Action<string> Notify;
        public Action Changed;
        private readonly System.Random random;

        public DragonGameSession(GameSaveData data, System.Random random = null, DragonGameConfig config = null)
        {
            Config = config != null ? config.CreateRuntimeCopy() : DragonRuntimeConfig.CreateDefault();
            Data = data ?? Config.CreateNewSave(); Data.EnsureDefaults(Config);
            this.random = random ?? new System.Random();
        }
        public float CurrentWinChance => Data.currentEnemy < 0 ? 0f :
            Mathf.Clamp(Data.battleBaseChance + Data.cheerBonus, 0f, Config.FinalChanceCap);
        public string ActiveSkill
        {
            get
            {
                for (int i = DragonCatalog.Count - 1; i >= 1; i--)
                    if (Data.trainingRewardsClaimed[i]) return GetSpec((DragonKind)i).skillName;
                return "기본 펀치 · 킥";
            }
        }

        public void Tick(float delta, long nowUtc)
        {
            ProcessTimers(nowUtc);
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta <= 0f) return;
            // Only live-frame time advances hunting; loading never supplies wall-clock elapsed time.
            float remaining = delta;
            int transitions = 0;
            while (remaining > 0f && transitions < 256)
            {
                float step = Mathf.Min(remaining, Mathf.Max(0f, Data.phaseRemaining));
                AdvanceProgression(step);
                Data.phaseRemaining -= step; remaining -= step;
                if (Data.phaseRemaining > 0.0001f) break;
                transitions++;
                switch (Data.phase)
                {
                    case HuntPhase.Walking: StartEncounter(); break;
                    case HuntPhase.Fighting: ResolveBattle(); break;
                    case HuntPhase.Result:
                    case HuntPhase.Recovery:
                        Data.phase = HuntPhase.Walking; Data.phaseRemaining = Config.WalkSeconds;
                        Data.currentEnemy = -1; Changed?.Invoke(); break;
                }
            }
        }
        private void AdvanceProgression(float seconds)
        {
            Data.activeHuntSeconds += seconds;
            for (int i = 1; i < DragonCatalog.Count; i++)
                if (DragonMmaRules.IsUnlocked(Data, (DragonKind)i) && !Data.discovered[i])
                    Data.pityElapsed[i] += seconds;
        }
        public void Cheer(int count = 1)
        {
            if (Data.phase != HuntPhase.Fighting || count <= 0) return;
            Data.cheerBonus = Config.AddCheer(Data.cheerBonus, count);
        }
        private void StartEncounter()
        {
            for (int i = 1; i < DragonCatalog.Count; i++)
                if (DragonMmaRules.IsUnlocked(Data, (DragonKind)i) && !Data.discovered[i]) Data.pityEncounters[i]++;
            BeginBattle(ChooseEncounter(), (float)(random.NextDouble() * 100d));
        }
        private DragonKind ChooseEncounter()
        {
            if (!Data.discovered[0]) return DragonKind.Baby;
            int total = 0;
            for (int i = 0; i < DragonCatalog.Count; i++)
            {
                if (!DragonMmaRules.IsUnlocked(Data, (DragonKind)i)) continue;
                if (i > 0 && !Data.discovered[i] &&
                    (Data.pityEncounters[i] >= Config.GetPityEncounterLimit((DragonKind)i) ||
                     Data.pityElapsed[i] >= Config.GetPitySecondLimit((DragonKind)i))) return (DragonKind)i;
                total += GetSpec((DragonKind)i).weight;
            }
            int roll = random.Next(total);
            for (int i = 0; i < DragonCatalog.Count; i++)
                if (DragonMmaRules.IsUnlocked(Data, (DragonKind)i))
                { roll -= GetSpec((DragonKind)i).weight; if (roll < 0) return (DragonKind)i; }
            return DragonKind.Baby;
        }
        private void BeginBattle(DragonKind kind, float roll)
        {
            Data.encounterCount++; Data.currentEnemy = (int)kind;
            Data.discovered[(int)kind] = true;
            Data.phase = HuntPhase.Fighting; Data.phaseRemaining = Config.BattleSeconds;
            Data.cheerBonus = 0f;
            Data.battleBaseChance = Config.CalculateBaseWinChance(Data.fighterPower, GetSpec(kind).power);
            Data.battleRoll = Mathf.Clamp(roll, 0f, 100f);
            Data.lastBattleWon = false; Data.lastWasExtortion = false;
            Changed?.Invoke();
        }
        public void DebugForceEncounter(DragonKind kind, float roll = 0f)
        {
            if ((int)kind < 0 || (int)kind >= DragonCatalog.Count) return;
            BeginBattle(kind, roll);
        }
        private void ResolveBattle()
        {
            DragonKind kind = (DragonKind)Data.currentEnemy;
            DragonSpec spec = GetSpec(kind);
            Data.lastBattleWon = Data.battleRoll < CurrentWinChance;
            if (Data.lastBattleWon)
            {
                Data.phase = HuntPhase.Result; Data.phaseRemaining = Config.WinResultSeconds;
                Data.lastWasExtortion = Data.cart.Count >= Config.CartCapacity;
                if (Data.lastWasExtortion)
                {
                    int reward = Config.ExtortionReward(kind); Data.coins += reward;
                    Notify?.Invoke("수레가 가득 찼습니다 · 삥뜯기 +" + reward + " G");
                }
                else
                {
                    Data.cart.Add(new DragonInstance(kind));
                    if (!Data.captured[(int)kind])
                    {
                        Data.captured[(int)kind] = true;
                        int next = (int)kind + 1;
                        if (next < DragonCatalog.Count)
                        { Data.pityEncounters[next] = 0; Data.pityElapsed[next] = 0f; }
                    }
                    Notify?.Invoke(spec.displayName + " 포획!  수레 " + Data.cart.Count + "/" + Config.CartCapacity);
                }
            }
            else
            {
                Data.phase = HuntPhase.Recovery; Data.phaseRemaining = Config.RecoverySeconds;
                Notify?.Invoke($"파이터가 잠시 쉬어갑니다 · {Config.RecoverySeconds:0.#}초 후 다시 출발");
            }
            Changed?.Invoke();
        }

        public List<DragonInstance> ReturnCart()
        {
            // Copy before clearing: captures during the exit animation belong to the new cart.
            var departing = new List<DragonInstance>(Data.cart);
            if (departing.Count == 0) return departing;
            Data.storage.AddRange(departing); Data.cart.Clear();
            Notify?.Invoke(departing.Count + "마리를 수용소로 보냈습니다"); Changed?.Invoke();
            return departing;
        }
        public bool UnlockTraining()
        {
            if (Data.trainingUnlocked || Data.coins < Config.TrainingUnlockCost) return false;
            Data.coins -= Config.TrainingUnlockCost; Data.trainingUnlocked = true;
            Notify?.Invoke("숲 수련방 개방 · 용 한 마리를 배치해보세요"); Changed?.Invoke(); return true;
        }
        public bool AssignTraining(string id, long now)
        {
            if (!Data.trainingUnlocked) return false;
            DragonInstance dragon = FindStored(id); if (dragon == null) return false;
            ProcessTimers(now);
            TimedDragonSlot slot = Data.training[0];
            if (!slot.IsEmpty) Data.storage.Add(slot.dragon);
            Data.storage.Remove(dragon); slot.Clear(); slot.dragon = dragon;
            slot.startUtc = Math.Max(now, Data.lastSavedUtc);
            slot.endUtc = slot.startUtc + (long)GetSpec(dragon.Kind).trainingSeconds;
            // Completed individuals can continue decorative exercise, never produce repeated cores.
            slot.completed = dragon.coreRewardClaimed; slot.rewardApplied = dragon.coreRewardClaimed;
            Notify?.Invoke(GetSpec(dragon.Kind).displayName + (slot.completed ? " · 자유 수련 중" : " · 수련 시작"));
            Changed?.Invoke(); return true;
        }
        public bool RecallTraining()
        {
            TimedDragonSlot slot = Data.training[0];
            if (slot.IsEmpty) return false;
            Data.storage.Add(slot.dragon); slot.Clear();
            Notify?.Invoke("수련 용을 수용소로 회수했습니다"); Changed?.Invoke(); return true;
        }
        public bool RegisterSale(string id, int slotIndex, long now)
        {
            if (slotIndex < 0 || slotIndex >= Data.sales.Length || !Data.sales[slotIndex].IsEmpty) return false;
            DragonInstance dragon = FindStored(id); if (dragon == null) return false;
            Data.storage.Remove(dragon);
            TimedDragonSlot slot = Data.sales[slotIndex]; slot.Clear(); slot.dragon = dragon;
            slot.startUtc = Math.Max(now, Data.lastSavedUtc);
            slot.endUtc = slot.startUtc + (long)GetSpec(dragon.Kind).saleSeconds;
            Notify?.Invoke(GetSpec(dragon.Kind).displayName + " · 판매소 등록"); Changed?.Invoke(); return true;
        }
        public bool ClaimSale(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Data.sales.Length) return false;
            TimedDragonSlot slot = Data.sales[slotIndex];
            if (slot.IsEmpty || !slot.completed) return false;
            int reward = GetSpec(slot.Kind).salePrice; Data.coins += reward; slot.Clear();
            Notify?.Invoke("판매 보상 +" + reward + " G"); Changed?.Invoke(); return true;
        }
        public void ProcessTimers(long nowUtc)
        {
            if (nowUtc < Data.lastSavedUtc) return;
            bool changed = false;
            foreach (TimedDragonSlot slot in Data.sales)
                if (!slot.IsEmpty && !slot.completed && DragonMmaRules.IsTimerComplete(nowUtc, slot.endUtc))
                { slot.completed = true; changed = true; }
            foreach (TimedDragonSlot slot in Data.training) changed |= CompleteTraining(slot, nowUtc);
            // Honor an old second slot's pending timer without offering a second new training slot.
            for (int i = Data.pendingLegacyTraining.Count - 1; i >= 0; i--)
            {
                TimedDragonSlot slot = Data.pendingLegacyTraining[i];
                changed |= CompleteTraining(slot, nowUtc);
                if (slot.IsEmpty || slot.rewardApplied)
                {
                    if (!slot.IsEmpty) Data.storage.Add(slot.dragon);
                    Data.pendingLegacyTraining.RemoveAt(i); changed = true;
                }
            }
            if (changed) Changed?.Invoke();
        }
        private bool CompleteTraining(TimedDragonSlot slot, long nowUtc)
        {
            if (slot.IsEmpty || slot.rewardApplied) return false;
            if (!slot.completed && !DragonMmaRules.IsTimerComplete(nowUtc, slot.endUtc)) return false;
            slot.completed = true; slot.rewardApplied = true;
            DragonSpec spec = GetSpec(slot.Kind);
            int gained = 0;
            if (!slot.dragon.coreRewardClaimed)
            { gained = spec.coreReward; Data.cores += gained; slot.dragon.coreRewardClaimed = true; }
            bool first = !Data.trainingRewardsClaimed[(int)slot.Kind];
            if (first)
            { Data.trainingRewardsClaimed[(int)slot.Kind] = true; Data.fighterPower += spec.powerReward; }
            Notify?.Invoke(spec.displayName + " 수련 완료 · 용핵 +" + gained +
                (first ? " / " + spec.skillName + " · 전투력 +" + spec.powerReward : " · 계속 자유 수련합니다"));
            return true;
        }
        private DragonInstance FindStored(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Data.storage.Find(dragon => dragon.id == id);
        }
    }
}
