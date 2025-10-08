using System;
using System.Collections.Generic;
using Core.Item.Interface;

namespace Core.Inventory
{
    /// <summary>
    ///     インベントリに挿入するアイテムの処理だけを行います
    /// </summary>
    internal static class InventoryInsertItem
    {
        /// <summary>
        ///     引数のインベントリ用アイテム配列に対して複数のアイテム挿入処理を行う
        /// </summary>
        /// <param name="insertItemStack">挿入したいアイテムリスト</param>
        /// <param name="inventoryItems">挿入するインベントリのアイテムリスト</param>
        /// <param name="itemStackFactory">アイテム作成用のItemStackFactory</param>
        /// <param name="option">挿入時のオプション</param>
        /// <param name="onSlotUpdate">挿入後発火したいイベント</param>
        /// <returns>余ったアイテム</returns>
        internal static List<IItemStack> InsertItem(List<IItemStack> insertItemStack, List<IItemStack> inventoryItems, IItemStackFactory itemStackFactory, OpenableInventoryItemDataStoreServiceOption option, Action<int> onSlotUpdate = null)
        {
            var reminderItemStacks = new List<IItemStack>();

            foreach (var item in insertItemStack)
            {
                var remindItemStack = InsertItem(item, inventoryItems, itemStackFactory, option, onSlotUpdate);
                if (remindItemStack.Equals(itemStackFactory.CreatEmpty())) continue;

                reminderItemStacks.Add(remindItemStack);
            }

            return reminderItemStacks;
        }
        
        
        /// <summary>
        ///     引数のインベントリ用アイテム配列に対して挿入処理を行う
        /// </summary>
        /// <param name="insertItemStack">挿入したいアイテム</param>
        /// <param name="inventoryItems">挿入するインベントリのアイテムリスト</param>
        /// <param name="itemStackFactory">アイテム作成用のItemStackFactory</param>
        /// <param name="option">挿入時のオプション</param>
        /// <param name="onSlotUpdate">挿入後発火したいイベント</param>
        /// <returns>余ったアイテム</returns>
        internal static IItemStack InsertItem(IItemStack insertItemStack, List<IItemStack> inventoryItems, IItemStackFactory itemStackFactory, OpenableInventoryItemDataStoreServiceOption option, Action<int> onSlotUpdate = null)
        {
            // AllowMultipleStacksPerItemOnInsert = trueの場合は通常通り挿入
            if (option.AllowMultipleStacksPerItemOnInsert)
            {
                return InsertWithDefaultBehavior(insertItemStack, inventoryItems, itemStackFactory, onSlotUpdate);
            }

            // AllowMultipleStacksPerItemOnInsert = falseの場合
            var hasSameItem = HasSameItemInInventory(insertItemStack, inventoryItems, itemStackFactory);
            if (hasSameItem)
            {
                return InsertToExistingStacksOnly(insertItemStack, inventoryItems, itemStackFactory, onSlotUpdate);
            }

            // 同じアイテムが存在しない場合は通常通り挿入（最初のスタック作成OK）
            return InsertWithDefaultBehavior(insertItemStack, inventoryItems, itemStackFactory, onSlotUpdate);

            #region Internal

            bool HasSameItemInInventory(IItemStack itemStack, List<IItemStack> inventory, IItemStackFactory factory)
            {
                foreach (var slot in inventory)
                {
                    if (slot.Equals(factory.CreatEmpty())) continue;
                    if (slot.Id == itemStack.Id) return true;
                }
                return false;
            }

            IItemStack InsertToExistingStacksOnly(IItemStack itemStack, List<IItemStack> inventory, IItemStackFactory factory, Action<int> slotUpdate)
            {
                var currentItemStack = itemStack;
                for (var i = 0; i < inventory.Count; i++)
                {
                    // 空スロットはスキップ（新しいスタックの作成を禁止）
                    if (inventory[i].Equals(factory.CreatEmpty())) continue;
                    // 同じアイテムIDのスロットにのみ挿入を試みる
                    if (inventory[i].Id != currentItemStack.Id) continue;
                    if (!inventory[i].IsAllowedToAddWithRemain(currentItemStack)) continue;

                    var remain = InsertionItemBySlot(i, currentItemStack, inventory, factory, slotUpdate);

                    if (remain.Equals(factory.CreatEmpty())) return remain;
                    currentItemStack = remain;
                }

                return currentItemStack;
            }

            IItemStack InsertWithDefaultBehavior(IItemStack itemStack, List<IItemStack> inventory, IItemStackFactory factory, Action<int> slotUpdate)
            {
                var currentItemStack = itemStack;
                for (var i = 0; i < inventory.Count; i++)
                {
                    //挿入できるスロットを探索
                    if (!inventory[i].IsAllowedToAddWithRemain(currentItemStack)) continue;

                    //挿入実行
                    var remain = InsertionItemBySlot(i, currentItemStack, inventory, factory, slotUpdate);

                    //挿入結果が空のアイテムならそのまま処理を終了
                    if (remain.Equals(factory.CreatEmpty())) return remain;
                    //そうでないならあまりのアイテムを入れるまで探索
                    currentItemStack = remain;
                }

                return currentItemStack;
            }

            #endregion
        }
        
        /// <summary>
        ///     特定のスロットを優先してアイテムを挿入します
        ///     優先すべきスロットに入らない場合は、通常通り挿入処理を行います
        /// </summary>
        public static IItemStack InsertItemWithPrioritySlot(IItemStack itemStack, List<IItemStack> inventory, IItemStackFactory itemStackFactory, OpenableInventoryItemDataStoreServiceOption option, int[] prioritySlots, Action<int> invokeEvent)
        {
            //優先スロットに挿入を試みる
            var remainItem = itemStack;
            foreach (var prioritySlot in prioritySlots)
                remainItem = InsertionItemBySlot(prioritySlot, remainItem, inventory, itemStackFactory, invokeEvent);

            //優先スロットに入り切らなかったアイテムは通常のインサート処理を行う
            return InsertItem(remainItem, inventory, itemStackFactory, option, invokeEvent);
        }
        
        /// <summary>
        ///     指定されたスロットにアイテムを挿入する
        /// </summary>
        /// <returns>余ったアイテム 余ったアイテムがなければ空のアイテムを返す</returns>
        private static IItemStack InsertionItemBySlot(int slot, IItemStack itemStack, List<IItemStack> inventoryItems, IItemStackFactory itemStackFactory, Action<int> onSlotUpdate = null)
        {
            if (itemStack.Equals(itemStackFactory.CreatEmpty())) return itemStack;
            if (!inventoryItems[slot].IsAllowedToAddWithRemain(itemStack)) return itemStack;
            
            var result = inventoryItems[slot].AddItem(itemStack);
            
            //挿入を試した結果が今までと違う場合は入れ替えをしてイベントを発火
            if (!inventoryItems[slot].Equals(result.ProcessResultItemStack))
            {
                inventoryItems[slot] = result.ProcessResultItemStack;
                onSlotUpdate?.Invoke(slot);
            }
            
            return result.RemainderItemStack;
        }
    }
}