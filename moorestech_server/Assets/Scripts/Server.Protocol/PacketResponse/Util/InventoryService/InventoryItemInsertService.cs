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
            
            var insertResult = toInventory.InsertItem(insertItemId, insertItemCount);
            
            //挿入した結果手元に何個アイテムが残るかを計算
            var returnItemCount = fromInventory.GetItem(fromSlot).Count - insertItemCount + insertResult.Count;
            
            fromInventory.SetItem(fromSlot, insertItemId, returnItemCount);
        }
    }
}