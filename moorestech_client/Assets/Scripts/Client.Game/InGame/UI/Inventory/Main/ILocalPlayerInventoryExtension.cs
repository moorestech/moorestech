using Core.Master;
using Game.PlayerInventory.Interface;

namespace Client.Game.InGame.UI.Inventory.Main
{
    public static class ILocalPlayerInventoryExtension
    {
        public static int GetMainInventoryItemCount(this ILocalPlayerInventory localPlayerInventory, ItemId itemId)
        {
            var count = 0;
            for (var i = 0; i < localPlayerInventory.MainSlotCount; i++)
            {
                if (localPlayerInventory[i].Id == itemId)
                {
                    count += localPlayerInventory[i].Count;
                }
            }

            return count;
        }

        // ホットバーは常にメインインベントリの最後の9スロット
        // The hotbar is always the last nine slots of the main inventory
        public static int GetHotBarInventorySlot(this ILocalPlayerInventory localPlayerInventory, int hotBarSlot)
        {
            return PlayerInventoryConst.HotBarSlotToInventorySlot(hotBarSlot, localPlayerInventory.MainSlotCount);
        }

        public static bool IsHotBarSlot(this ILocalPlayerInventory localPlayerInventory, int slot)
        {
            return PlayerInventoryConst.IsHotBarSlot(slot, localPlayerInventory.MainSlotCount);
        }
    }
}