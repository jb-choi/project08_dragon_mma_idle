using System;
using UnityEngine;
using UnityEngine.UI;

namespace DragonMMA
{
    public sealed partial class DragonMmaView
    {
        private DragonInstance Selected()
        {
            var selected = Data.storage.Find(d => d.id == selectedId);
            if (selected == null && Data.storage.Count > 0) { selected = Data.storage[Data.storage.Count - 1]; selectedId = selected.id; }
            return selected;
        }
        private void RefreshStorage()
        {
            var selected = Selected();
            int pages = Math.Max(1, (Data.storage.Count + 9) / 10); page = Mathf.Clamp(page, 0, pages - 1);
            Set(storage, "Storage heading", $"용 수용소   /   {Data.storage.Count}마리 보유");
            Set(storage, "Page", $"{page + 1} / {pages}");
            storage.Get<Button>("Previous page").interactable = page > 0;
            storage.Get<Button>("Next page").interactable = page + 1 < pages;
            storage.Show("Empty state", selected == null); storage.Show("Selected dragon", selected != null);
            for (int i = 0; i < 10; i++)
            {
                string key = "StorageCard" + i; int index = Data.storage.Count - 1 - page * 10 - i;
                storage.Show(key, index >= 0);
                if (index >= 0)
                {
                    var d = Data.storage[index]; var s = game.GetSpec(d.Kind);
                    storage.Get<Image>(key).color = d.id == selectedId ? selectedCardTint : cardColors[i];
                    storage.Get<Image>(key + "/Portrait").sprite = Portrait(d.kind);
                    Set(storage, key + "/Name", s.displayName);
                    Set(storage, key + "/Individual state", d.coreRewardClaimed ? "수련 완료 개체" : "미수련 · " + s.salePrice + "G");
                }
                int recent = Data.storage.Count - 1 - i;
                roamers[i].gameObject.SetActive(recent >= 0);
                if (recent >= 0) roamers[i].sprite = Portrait(Data.storage[recent].kind);
            }
            if (selected == null) return;
            var spec = game.GetSpec(selected.Kind);
            Set(storage, "Selected dragon/Detail name", spec.displayName);
            Set(storage, "Selected dragon/Detail stats", $"전투력 {spec.power}   ·   판매 {spec.salePrice}G / {Duration(spec.saleSeconds)}\n수련 {Duration(spec.trainingSeconds)}   ·   용핵 +{(selected.coreRewardClaimed ? 0 : spec.coreReward)}");
            storage.Get<Button>("Selected dragon/Assign training").interactable = Data.trainingUnlocked;
            Set(storage, "Selected dragon/Assign training/Label", Data.trainingUnlocked ? "수련방 배치 / 교체" : "수련방 잠김 · " + Rules.TrainingUnlockCost + "G");
        }
        private void RefreshTraining()
        {
            if (!Data.trainingUnlocked)
            {
                Set(trainingLocked, "Unlock training/Label", $"{Rules.TrainingUnlockCost}G로 개방  /  보유 {Data.coins}G");
                trainingLocked.Get<Button>("Unlock training").interactable = Data.coins >= Rules.TrainingUnlockCost;
                return;
            }
            var slot = Data.training[0]; if (slot.IsEmpty) return;
            var spec = game.GetSpec(slot.Kind);
            trainee.SetDragonKind(slot.Kind);
            Set(trainingActive, "Partner", spec.displayName + "  /  " + spec.skillName);
            Set(trainingActive, "Dojo scene/Practice caption", slot.completed ? "계속 수련 중 · 보상 수령 완료" : "힘을 모으는 중");
            float ratio = slot.completed ? 1 : Mathf.Clamp01(1 - (float)Math.Max(0, slot.endUtc - DragonMmaGame.Now) / Math.Max(1, slot.endUtc - slot.startUtc));
            Fill(trainingActive, "Progress track/Progress fill", ratio);
            Set(trainingActive, "Core charge", slot.completed ? "용핵 보상 확정 · 이 개체의 반복 보상은 없습니다." : $"용핵 충전 {ratio:P0}   ·   남은 시간 {Duration(Math.Max(0, slot.endUtc - DragonMmaGame.Now))}");
        }
        private void RefreshMarket()
        {
            for (int i = 0; i < 2; i++)
            {
                string key = "Market slot " + i; var slot = Data.sales[i];
                foreach (string child in new[] { "Selling dragon", "Listing name", "Sale clock", "Claim sale " + i }) market.Show(key + "/" + child, !slot.IsEmpty);
                market.Show(key + "/Empty listing", slot.IsEmpty); market.Show(key + "/Choose seller", slot.IsEmpty);
                if (slot.IsEmpty) continue;
                var spec = game.GetSpec(slot.Kind);
                market.Get<Image>(key + "/Selling dragon").sprite = Portrait((int)slot.Kind);
                Set(market, key + "/Listing name", spec.displayName + "   " + spec.salePrice + "G");
                Set(market, key + "/Sale clock", slot.completed ? "판매 완료 · 수령 대기" : "남은 시간  " + Duration(Math.Max(0, slot.endUtc - DragonMmaGame.Now)));
                Set(market, key + "/Claim sale " + i + "/Label", slot.completed ? "판매금 수령 +" + spec.salePrice + "G" : "구매자를 기다리는 중");
                market.Get<Button>(key + "/Claim sale " + i).interactable = slot.completed;
            }
        }
        private void RefreshSkills()
        {
            for (int i = 0; i < 5; i++)
            {
                string key = "Skill " + i; var spec = game.GetSpec((DragonKind)i);
                Set(skills, key + "/Species", Data.discovered[i] ? spec.displayName : "미발견");
                Set(skills, key + "/Power reward", "최초 수련  힘 +" + spec.powerReward);
                Set(skills, key + "/Technique name", spec.skillName);
                Set(skills, key + "/Unlock state", Data.trainingRewardsClaimed[i] ? "● 해금 완료 · 전투 연출 적용" : "○ 이 종을 포획하고 수련");
            }
        }
    }
}
