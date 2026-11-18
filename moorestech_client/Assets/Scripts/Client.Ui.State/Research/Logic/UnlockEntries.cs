using System.Collections.Generic;

namespace Client.Ui.State.Research.Logic
{
    /// <summary>
    /// 解放物と報酬を表示用の 1 本の並びへ集める（Web unlockEntries.ts の写し）。並びは Web と同じ種類順
    /// Gathers unlocks and rewards into one display list (port of Web unlockEntries.ts) in the same kind order as the Web
    /// </summary>
    public static class UnlockEntries
    {
        public static List<UnlockEntry> ToUnlockEntries(ResearchUnlockSet unlocks)
        {
            // ブロック → 機械レシピ → レシピ閲覧 → 報酬 → 接続ツール → 車両 → モジュール上限 → レベル継承上限
            // Blocks → machine recipes → recipe views → rewards → connect tools → cars → module limits → inheritance caps
            var entries = new List<UnlockEntry>();
            foreach (var block in unlocks.Blocks) entries.Add(new BlockUnlockEntry(entries.Count, block));
            foreach (var recipe in unlocks.MachineRecipes) entries.Add(new MachineRecipeOutputUnlockEntry(entries.Count, recipe));
            foreach (var itemId in unlocks.ItemRecipeViewItemIds) entries.Add(new ItemRecipeViewUnlockEntry(entries.Count, itemId));
            foreach (var reward in unlocks.RewardItems) entries.Add(new RewardItemUnlockEntry(entries.Count, reward));
            foreach (var guid in unlocks.ConnectToolGuids) entries.Add(new ConnectToolUnlockEntry(entries.Count, guid));
            foreach (var guid in unlocks.TrainCarGuids) entries.Add(new TrainCarUnlockEntry(entries.Count, guid));
            foreach (var limit in unlocks.ModuleSlotStackLimits) entries.Add(new ModuleSlotStackLimitUnlockEntry(entries.Count, limit));
            foreach (var cap in unlocks.LevelInheritanceCraftCaps) entries.Add(new LevelInheritanceCraftCapUnlockEntry(entries.Count, cap));
            return entries;
        }
    }
}
