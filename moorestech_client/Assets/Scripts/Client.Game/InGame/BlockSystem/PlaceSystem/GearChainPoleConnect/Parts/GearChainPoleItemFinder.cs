using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts
{
    /// <summary>
    /// 所持アイテムから延長で消費するチェーンアイテムを自動選択する
    /// Auto-selects the chain item consumed by extension from owned items
    /// </summary>
    public static class GearChainPoleItemFinder
    {
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
