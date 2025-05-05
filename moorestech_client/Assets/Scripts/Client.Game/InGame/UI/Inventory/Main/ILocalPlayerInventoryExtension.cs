using Core.Master;
using Game.PlayerInventory.Interface;

namespace Client.Game.InGame.UI.Inventory.Main
{
    public static class ILocalPlayerInventoryExtension
    {
        public static int GetMainInventoryItemCount(this ILocalPlayerInventory localPlayerInventory, ItemId itemId)
        {
            var count = 0;
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
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