using System;
using System.Collections.Generic;
using Core.Item.Interface;

namespace Core.Inventory
{
    /// <summary>
    ///     インベントリの指定スロットを置き換える処理だけを行います
    /// </summary>
    public static class InventoryReplaceItem
    {
        /// <summary>
        ///     指定されたスロットのアイテムを置き換える
        ///     同一IDならスタックして余りを返し、別IDなら入れ替えて元のアイテムを返す
        /// </summary>
        /// <returns>スタック時は入りきらなかった余り、入れ替え時は元々スロットにあったアイテム</returns>
        internal static IItemStack ReplaceItem(int slot, IItemStack itemStack, List<IItemStack> inventoryItems, OpenableInventoryItemDataStoreServiceOption option, Action<int> onSlotUpdate)
        {
            // 空アイテムでの置き換えは取り出し操作なので受入制限は適用しない
            // Replacing with an empty item is a take-out operation, so acceptance is not applied
            if (itemStack.Count == 0) return ReplaceWithoutRestriction();

            // 受け入れられないアイテムは書き込まず、入力をそのまま返す
            // Unacceptable items are not written and the input is returned as is
            if (!option.ItemAcceptance.CanAccept(itemStack.Id)) return itemStack;

            var currentItem = inventoryItems[slot];
            var maxCountPerSlot = option.ItemAcceptance.GetMaxCountPerSlot(itemStack.Id);

            // 同一IDは1スロット上限までスタックし、入らなかった分を余りとして返す
            // Same ids stack up to the per-slot cap and the rest is returned as a remainder
            if (currentItem.Id == itemStack.Id) return StackToSameItemSlot();

            // 空スロットへは上限までを置き、超過分を余りとして返す
            // Empty slots receive up to the cap and the excess is returned as a remainder
            if (currentItem.Count == 0) return PlaceToEmptySlot();

            // 別アイテムとの入れ替えは元のアイテムを返すため余りを返せず、上限を超えるなら実行しない
            // A swap must return the replaced item, so it is skipped when the count exceeds the cap
            if (maxCountPerSlot < itemStack.Count) return itemStack;

            return ReplaceWithoutRestriction();

            #region Internal

            IItemStack ReplaceWithoutRestriction()
            {
                //アイテムIDが同じの時はスタックして余ったものを返す
                var replacedItem = inventoryItems[slot];
                if (replacedItem.Id == itemStack.Id)
                {
                    var result = replacedItem.AddItem(itemStack);
                    inventoryItems[slot] = result.ProcessResultItemStack;
                    onSlotUpdate(slot);
                    return result.RemainderItemStack;
                }

                //違う場合はそのまま入れ替える
                inventoryItems[slot] = itemStack;
                onSlotUpdate(slot);
                return replacedItem;
            }

            IItemStack StackToSameItemSlot()
            {
                var addableCount = Math.Min(maxCountPerSlot - currentItem.Count, itemStack.Count);
                if (addableCount <= 0) return itemStack;
                if (addableCount == itemStack.Count) return ReplaceWithoutRestriction();

                // 上限までを切り出して加算し、実際に入った分を差し引いた残りを返す
                // Cut out the amount up to the cap, add it, and return what was not consumed
                var addingItem = itemStack.SubItem(itemStack.Count - addableCount);
                var result = currentItem.AddItem(addingItem);
                inventoryItems[slot] = result.ProcessResultItemStack;
                onSlotUpdate(slot);

                return itemStack.SubItem(addingItem.Count - result.RemainderItemStack.Count);
            }

            IItemStack PlaceToEmptySlot()
            {
                var placeableCount = Math.Min(maxCountPerSlot, itemStack.Count);
                if (placeableCount <= 0) return itemStack;
                if (placeableCount == itemStack.Count) return ReplaceWithoutRestriction();

                // 上限までを空スロットに置き、置けなかった分を返す
                // Put the amount up to the cap into the empty slot and return the rest
                inventoryItems[slot] = itemStack.SubItem(itemStack.Count - placeableCount);
                onSlotUpdate(slot);

                return itemStack.SubItem(placeableCount);
            }

            #endregion
        }
    }
}
