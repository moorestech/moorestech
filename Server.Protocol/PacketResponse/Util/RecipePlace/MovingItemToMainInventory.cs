using Core.Inventory;
using Game.PlayerInventory.Interface;
using Server.Protocol.PacketResponse.Util.InventoryService;

namespace Server.Protocol.PacketResponse.Util.RecipePlace
{
    public static class MovingItemToMainInventory
    {
        
        /// <summary>
        /// クラフトインベントリ、グラブインベントリにあるアイテムをすべてメインインベントリに移動します
        /// <see cref="SetRecipeCraftingInventoryProtocol"/> で使用します
        /// </summary>
        /// <param name="main">移動先のメインインベントリ</param>
        /// <param name="craft">移動元のクラフトインベントリ</param>
        /// <param name="grab">移動元のグラブインベントリ</param>
        public static void Move(IOpenableInventory main, IOpenableInventory craft, IOpenableInventory grab)
        {
            //クラフトインベントリ、グラブインベントリのアイテムを全てメインインベントリに移動
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                var itemCount = craft.GetItem(i).Count;
                InventoryItemInsertService.Insert(craft,i,main,itemCount);
            }
            var grabItemCount = grab.GetItem(0).Count;
            InventoryItemInsertService.Insert(grab,0,main,grabItemCount);
        }
    }
}