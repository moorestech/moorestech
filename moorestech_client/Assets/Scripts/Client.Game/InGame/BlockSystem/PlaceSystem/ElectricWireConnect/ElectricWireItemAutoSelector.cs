using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Game.PlayerInventory.Interface;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    /// 敷設に使う電線アイテムをマスタ定義順で自動選択する（サーバーの自動接続と同一ルール）
    /// Auto-select the wire item in master definition order, matching the server auto-connect rule
    /// </summary>
    public static class ElectricWireItemAutoSelector
    {
        public static ItemId FindOwnedWireItemId(ILocalPlayerInventory inventory)
        {
            // マスタ定義順に走査し、最初に所持している電線アイテムを採用する
            // Scan in master definition order and adopt the first wire item owned
            foreach (var wireItem in MasterHolder.BlockMaster.Blocks.ElectricWireItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(wireItem.ItemGuid);
                for (var i = 0; i < inventory.MainSlotCount; i++)
                {
                    if (inventory[i].Id == itemId && inventory[i].Count > 0) return itemId;
                }
            }

            return ItemMaster.EmptyItemId;
        }
    }
}
