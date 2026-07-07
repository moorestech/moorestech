using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts
{
    /// <summary>
    /// 手持ち・所持アイテムからポール／チェーンを判別する。モード分岐（上位）と延長時のチェーン自動選択（下位）の両方から使われる。
    /// Identifies pole/chain items from holding or owned items. Used both for mode branching (upper) and auto chain selection on extension (lower).
    /// </summary>
    public static class GearChainPoleItemFinder
    {
        /// <summary>
        /// アイテムが歯車チェーンポールのブロックアイテムならブロックマスタを返す
        /// Resolve the block master when the item is a gear chain pole block item
        /// </summary>
        public static bool TryGetPoleBlockMaster(ItemId itemId, out BlockMasterElement poleBlockMaster)
        {
            poleBlockMaster = null;
            if (itemId == ItemMaster.EmptyItemId || !MasterHolder.BlockMaster.IsBlock(itemId)) return false;

            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(MasterHolder.BlockMaster.GetBlockId(itemId));
            if (blockMaster.BlockType != BlockMasterElement.BlockTypeConst.GearChainPole) return false;

            poleBlockMaster = blockMaster;
            return true;
        }

        /// <summary>
        /// 延長接続で消費するチェーンアイテムをインベントリから自動選択する（未所持なら EmptyItemId）
        /// Auto-select the chain item consumed by extension from inventory (EmptyItemId when none is owned)
        /// </summary>
        public static ItemId FindOwnedChainItemId(ILocalPlayerInventory playerInventory)
        {
            // 所持スロット順で最初に見つかったチェーンアイテムを採用する
            // Adopt the first chain item found in inventory slot order
            for (var i = 0; i < playerInventory.Count; i++)
            {
                var stackId = playerInventory[i].Id;
                if (stackId == ItemMaster.EmptyItemId) continue;
                if (IsChainItem(stackId)) return stackId;
            }

            return ItemMaster.EmptyItemId;

            #region Internal

            bool IsChainItem(ItemId itemId)
            {
                foreach (var gearChainItem in MasterHolder.BlockMaster.Blocks.GearChainItems)
                {
                    if (MasterHolder.ItemMaster.GetItemId(gearChainItem.ItemGuid) == itemId) return true;
                }

                return false;
            }

            #endregion
        }
    }
}
