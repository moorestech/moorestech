using Core.Master;

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
    }
}
