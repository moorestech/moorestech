using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Game.PlayerInventory.Interface;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect
{
    /// <summary>
    /// 敷設に使うレールアイテムをマスタ定義順で自動選択する（サーバーの自動接続と同一ルール）
    /// Auto-select the rail item in master definition order, matching the server auto-connect rule
    /// </summary>
    public static class TrainRailItemAutoSelector
    {
        public static ItemId FindOwnedRailItemId(ILocalPlayerInventory inventory)
        {
            // マスタ定義順に走査し、最初に所持しているレールアイテムを採用する
            // Scan in master definition order and adopt the first rail item owned
            foreach (var railItem in MasterHolder.TrainUnitMaster.GetRailItems())
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(railItem.ItemGuid);
                for (var i = 0; i < inventory.MainSlotCount; i++)
                {
                    if (inventory[i].Id == itemId && inventory[i].Count > 0) return itemId;
                }
            }

            return ItemMaster.EmptyItemId;
        }
    }
}
