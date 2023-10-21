using Core.Inventory;
using Game.PlayerInventory.Interface;
using Server.Protocol.PacketResponse.Util.InventoryService;

namespace Server.Protocol.PacketResponse.Util.RecipePlace
{
    public static class MovingItemToMainInventory
    {

        ///     
        ///     <see cref="SetRecipeCraftingInventoryProtocol" /> 

        /// <param name="main"></param>
        /// <param name="craft"></param>
        /// <param name="grab"></param>
        public static void Move(IOpenableInventory main, IOpenableInventory craft, IOpenableInventory grab)
        {
            
            for (var i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                var itemCount = craft.GetItem(i).Count;
                InventoryItemInsertService.Insert(craft, i, main, itemCount);
            }

            var grabItemCount = grab.GetItem(0).Count;
            InventoryItemInsertService.Insert(grab, 0, main, grabItemCount);
        }
    }
}