using System;
using Core.Inventory;
using Core.Master;
using Game.Context;

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

            // 受入制限を宣言するインベントリは通常のInsertItemがスロット上限を知らないため、スロット単位で自前に詰める
            // InsertItem cannot know the per-slot cap, so restricted inventories are filled slot by slot instead
            var remainCount = toInventory is IItemAcceptanceInventory acceptanceInventory
                ? InsertWithAcceptanceLimit(acceptanceInventory, insertItemId, insertItemCount)
                : toInventory.InsertItem(insertItemId, insertItemCount).Count;

            //挿入した結果手元に何個アイテムが残るかを計算
            var returnItemCount = fromInventory.GetItem(fromSlot).Count - insertItemCount + remainCount;

            fromInventory.SetItem(fromSlot, insertItemId, returnItemCount);

            #region Internal

            int InsertWithAcceptanceLimit(IItemAcceptanceInventory acceptance, ItemId itemId, int itemCount)
            {
                // 受け入れられないアイテムは1個も入らない
                // Nothing fits when the item is not accepted at all
                if (!acceptance.CanAccept(itemId)) return itemCount;

                var maxCountPerSlot = acceptance.GetMaxCountPerSlot(itemId);
                var remain = itemCount;
                for (var slot = 0; slot < toInventory.GetSlotSize() && remain > 0; slot++)
                {
                    // 空きスロットか同一アイテムのスロットにだけ、上限までの余裕分を入れる
                    // Only empty slots or slots holding the same item receive items up to the cap
                    var currentItem = toInventory.GetItem(slot);
                    if (currentItem.Count != 0 && currentItem.Id != itemId) continue;

                    var insertableCount = Math.Min(remain, maxCountPerSlot - currentItem.Count);
                    if (insertableCount <= 0) continue;

                    // ReplaceItemはアイテムのスタック上限も見て余りを返すため、その分を残数へ戻す
                    // ReplaceItem also honors the item stack limit and returns the leftover to keep
                    var insertItem = ServerContext.ItemStackFactory.Create(itemId, insertableCount);
                    var leftoverItem = toInventory.ReplaceItem(slot, insertItem);
                    remain -= insertableCount - leftoverItem.Count;
                }

                return remain;
            }

            #endregion
        }
    }
}
