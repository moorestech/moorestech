using System;
using Core.Master;

namespace Game.PlayerInventory.Interface
{
    /// <summary>
    ///     レベルからスロット数を解決する
    ///     Resolves slot count from level
    /// </summary>
    public static class PlayerInventorySlotLevelMasterUtil
    {
        // スロットレベル未定義時の従来スロット数
        // Fallback slot count when slot levels are undefined in master data
        private const int FallbackMainInventorySlotCount = 45;

        public static int GetSlotCount(int level)
        {
            var levels = MasterHolder.ItemMaster.Items.PlayerInventorySlotLevels;
            if (levels == null || levels.Length == 0) return FallbackMainInventorySlotCount;

            // 範囲外レベルは[0, 最大]へクランプする
            // Out-of-range levels are clamped into [0, max level]
            var index = Math.Clamp(level, 0, levels.Length - 1);
            var slotCount = levels[index].SlotCount;

            // ホットバー分未満・前レベル未満のマスタ値は設定ミスなので明示的に失敗させる
            // Values below the hotbar size or the previous level are configuration errors, so fail explicitly
            if (slotCount < PlayerInventoryConst.HotBarSlotCount) throw new Exception($"playerInventorySlotLevels[{index}].slotCount ({slotCount}) はホットバー数 {PlayerInventoryConst.HotBarSlotCount} 以上が必要です");
            if (0 < index && slotCount < levels[index - 1].SlotCount) throw new Exception($"playerInventorySlotLevels[{index}].slotCount ({slotCount}) は前レベルの {levels[index - 1].SlotCount} 以上が必要です");
            return slotCount;
        }

        public static int GetMaxLevel()
        {
            var levels = MasterHolder.ItemMaster.Items.PlayerInventorySlotLevels;
            if (levels == null || levels.Length == 0) return 0;
            return levels.Length - 1;
        }
    }
}
