using System;
using Core.Master;

namespace Game.PlayerInventory.Interface
{
    /// <summary>
    ///     items.jsonのplayerInventorySlotLevelsからレベル→スロット数を解決する
    ///     Resolves level → slot count from playerInventorySlotLevels in items.json
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
            return levels[index].SlotCount;
        }

        public static int GetMaxLevel()
        {
            var levels = MasterHolder.ItemMaster.Items.PlayerInventorySlotLevels;
            if (levels == null || levels.Length == 0) return 0;
            return levels.Length - 1;
        }
    }
}
