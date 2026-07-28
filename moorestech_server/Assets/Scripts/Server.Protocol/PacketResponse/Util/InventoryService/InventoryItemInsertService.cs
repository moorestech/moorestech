using System;
using Core.Inventory;

namespace Server.Protocol.PacketResponse.Util.InventoryService
{
    public static class InventoryItemInsertService
    {
        public static void Insert(IOpenableInventory fromInventory, int fromSlot, IOpenableInventory toInventory,
            int count)
        {
            var insertItemId = fromInventory.GetItem(fromSlot).Id;
            //持っているアイテム以上のアイテムをinsertしないようにする
            var insertItemCount = Math.Min(fromInventory.GetItem(fromSlot).Count, count);

            //受入制限はインベントリ自身がInsertItem内で守るため、ここでは余りを受け取るだけでよい
            //The inventory itself enforces acceptance inside InsertItem, so only the remainder matters here
            var insertResult = toInventory.InsertItem(insertItemId, insertItemCount);

            //挿入した結果手元に何個アイテムが残るかを計算
            var returnItemCount = fromInventory.GetItem(fromSlot).Count - insertItemCount + insertResult.Count;

            fromInventory.SetItem(fromSlot, insertItemId, returnItemCount);
        }
    }
}
