using System;

namespace DragonMMA
{
    // Compact, text-first status labels for the always-visible toolbar. States are
    // readable without relying on color and do not require new scene objects.
    public static class DragonUiStatusLabels
    {
        public static string Camp(GameSaveData data) => $"거점 {data?.storage?.Count ?? 0} ↑";

        public static string Training(GameSaveData data, long nowUtc)
        {
            if (data == null || !data.trainingUnlocked) return "수련 잠김";
            TimedDragonSlot slot = data.training != null && data.training.Length > 0 ? data.training[0] : null;
            if (slot == null || slot.IsEmpty) return "수련 배치";
            if (slot.completed || slot.endUtc <= nowUtc) return "수련 완료";
            return "수련 " + ShortDuration(slot.endUtc - nowUtc);
        }

        public static string Market(GameSaveData data, long nowUtc)
        {
            if (data?.sales == null || data.sales.Length == 0) return "판매소";
            int active = 0, ready = 0;
            foreach (TimedDragonSlot slot in data.sales)
            {
                if (slot == null || slot.IsEmpty) continue;
                active++;
                if (slot.completed || slot.endUtc <= nowUtc) ready++;
            }
            if (ready > 0) return "판매 수령 " + ready;
            return active > 0 ? $"판매 {active}/{data.sales.Length}" : "판매소";
        }

        public static string Skills(GameSaveData data)
        {
            int mastered = 0;
            if (data?.trainingRewardsClaimed != null)
                foreach (bool claimed in data.trainingRewardsClaimed) if (claimed) mastered++;
            return $"기술 {mastered}/{data?.trainingRewardsClaimed?.Length ?? 5}";
        }

        private static string ShortDuration(long seconds)
        {
            seconds = Math.Max(0, seconds);
            if (seconds < 60) return seconds + "초";
            if (seconds < 3600) return (long)Math.Ceiling(seconds / 60d) + "분";
            return (long)Math.Ceiling(seconds / 3600d) + "시간";
        }
    }
}
