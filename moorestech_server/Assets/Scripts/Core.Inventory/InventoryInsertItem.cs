using System;
using System.Collections.Generic;
using Core.Item.Interface;

namespace Core.Inventory
{
    /// <summary>
    ///     インベントリに挿入するアイテムの処理だけを行います
    /// </summary>
    public static class InventoryInsertItem
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
                if (remindItemStack.Count == 0) continue;

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
                    if (slot.Count == 0) continue;
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
                    if (inventory[i].Count == 0) continue;
                    // 同じアイテムIDのスロットにのみ挿入を試みる
                    if (inventory[i].Id != currentItemStack.Id) continue;
                    if (!inventory[i].IsAllowedToAddWithRemain(currentItemStack)) continue;

                    var remain = InsertionItemBySlot(i, currentItemStack, inventory, factory, slotUpdate);

                    if (remain.Count == 0) return remain;
                    currentItemStack = remain;
                }

                return currentItemStack;
            }

            IItemStack InsertWithDefaultBehavior(IItemStack itemStack, List<IItemStack> inventory, IItemStackFactory factory, Action<int> slotUpdate)
            {
                // 既存スタックを起点に挿入順序を決定する
                // Determine insertion order by existing stacks first
                var currentItemStack = itemStack;
                var sameItemSlots = CollectSameItemSlots(inventory, currentItemStack);

                // 優先スタックに対して挿入処理を行う
                // Insert into the prioritized stacks
                if (sameItemSlots.Count > 0)
                {
                    currentItemStack = InsertPrioritizedStacks(currentItemStack, inventory, factory, slotUpdate, sameItemSlots);
                    if (currentItemStack.Count == 0) return currentItemStack;
                }

                // 既存スタックで処理しきれなかった分を通常順序で処理する
                // Process the remaining items using the standard order
                return InsertSequentially(currentItemStack, inventory, factory, slotUpdate);
            }

            List<int> CollectSameItemSlots(List<IItemStack> inventory, IItemStack targetItemStack)
            {
                // 同じアイテムIDを持つスロットを収集する
                // Collect slots that have the same item ID
                var slots = new List<int>();
                for (var i = 0; i < inventory.Count; i++)
                {
                    if (inventory[i].Count == 0) continue;
                    if (inventory[i].Id != targetItemStack.Id) continue;
                    slots.Add(i);
                }

                return slots;
            }

            IItemStack InsertPrioritizedStacks(IItemStack targetItemStack, List<IItemStack> inventory, IItemStackFactory factory, Action<int> slotUpdate, List<int> sameItemSlots)
            {
                var currentItemStack = targetItemStack;
                
                // 既存スタックに順番に挿入する
                // Insert items sequentially into existing stacks
                foreach (var slot in sameItemSlots)
                {
                    if (currentItemStack.Count == 0) return currentItemStack;
                    
                    // スロットにアイテムを挿入し、入りきらなかった余りを取得
                    // Insert items into the slot and get the remainder that didn't fit
                    var remain = InsertionItemBySlot(slot, currentItemStack, inventory, factory, slotUpdate);
                    
                    // 余りがない（すべて挿入できた）場合は処理を終了
                    // If there's no remainder (all items were inserted), end the process
                    if (remain.Count == 0) return remain;
                    
                    currentItemStack = remain;
                }
                
                
                // このスロットで入りきらなかった分を、このスロットの近傍スロットに展開して挿入を試みる
                // Try to insert the remainder into nearby slots around this slot
                var originSlot = sameItemSlots[^1];
                foreach (var slot in EnumerateProximity(originSlot, inventory.Count))
                {
                    if (!inventory[slot].IsAllowedToAddWithRemain(currentItemStack)) continue;
                    
                    var remain = InsertionItemBySlot(slot, currentItemStack, inventory, factory, slotUpdate);
                    if (remain.Count == 0) return remain;
                    
                    currentItemStack = remain;
                }
                
                return currentItemStack;
            }
            
            IEnumerable<int> EnumerateProximity(int originSlot, int inventorySize)
            {
                // 起点から距離順にスロットを列挙する
                // Enumerate slots by distance from the origin slot
                for (var offset = 1; offset < inventorySize; offset++)
                {
                    var left = originSlot - offset;
                    if (left >= 0) yield return left;

                    var right = originSlot + offset;
                    if (right < inventorySize) yield return right;
                }
            }

            IItemStack InsertSequentially(IItemStack targetItemStack, List<IItemStack> inventory, IItemStackFactory factory, Action<int> slotUpdate)
            {
                // 通常の探索順序で残りを処理する
                // Handle remaining items with the default sequential order
                var currentItemStack = targetItemStack;
                for (var i = 0; i < inventory.Count; i++)
                {
                    // 挿入できるスロットを探索
                    // Find slots that can accept the item
                    if (!inventory[i].IsAllowedToAddWithRemain(currentItemStack)) continue;
                    
                    // 挿入実行
                    // Execute insertion
                    var remain = InsertionItemBySlot(i, currentItemStack, inventory, factory, slotUpdate);

                    // 挿入結果が空のアイテムならそのまま処理を終了
                    // If the insertion result is an empty item, end the process
                    if (remain.Count == 0) return remain;
                    
                    // そうでないならあまりのアイテムを入れるまで探索
                    // Otherwise, continue searching with the remaining item
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
            // 優先スロット内で同種スタックを優先する
            // Prioritize same item stacks within the priority slots
            var currentItemStack = itemStack;
            var prioritizedSameSlots = CollectSameItemPrioritySlots(inventory, currentItemStack, itemStackFactory, prioritySlots);

            foreach (var slot in prioritizedSameSlots)
            {
                currentItemStack = ProcessPrioritySlot(slot, currentItemStack, inventory, itemStackFactory, invokeEvent);
                if (currentItemStack.Count == 0) return currentItemStack;
            }

            // 残った優先スロットで挿入を試す
            // Use the remaining priority slots for insertion
            foreach (var slot in prioritySlots)
            {
                if (prioritizedSameSlots.Contains(slot)) continue;
                currentItemStack = ProcessPrioritySlot(slot, currentItemStack, inventory, itemStackFactory, invokeEvent);
                if (currentItemStack.Count == 0) return currentItemStack;
            }

            // 優先枠で余った分は通常処理に回す
            // Delegate the rest to the default insertion logic
            return InsertItem(currentItemStack, inventory, itemStackFactory, option, invokeEvent);

            #region Internal

            List<int> CollectSameItemPrioritySlots(List<IItemStack> allSlots, IItemStack targetItemStack, IItemStackFactory factory, int[] prioritySlotIndices)
            {
                // 優先スロット内に存在する同種スタックを列挙する
                // Enumerate priority slots containing the same item stack
                var slots = new List<int>();
                foreach (var slot in prioritySlotIndices)
                {
                    if (slot < 0 || slot >= allSlots.Count) continue;
                    if (allSlots[slot].Count == 0) continue;
                    if (allSlots[slot].Id != targetItemStack.Id) continue;
                    slots.Add(slot);
                }

                return slots;
            }

            IItemStack ProcessPrioritySlot(int slotIndex, IItemStack targetItemStack, List<IItemStack> allSlots, IItemStackFactory factory, Action<int> slotUpdate)
            {
                // 指定スロットで挿入し、溢れた場合は近傍に展開する
                // Insert into the specified slot and spread overflow nearby
                var currentItemStack = targetItemStack;
                if (slotIndex < 0 || slotIndex >= allSlots.Count) return currentItemStack;

                if (allSlots[slotIndex].IsAllowedToAddWithRemain(currentItemStack))
                {
                    var remain = InsertionItemBySlot(slotIndex, currentItemStack, allSlots, factory, slotUpdate);
                    if (remain.Count == 0) return remain;
                    currentItemStack = remain;
                }

                return InsertByProximity(slotIndex, currentItemStack, allSlots, factory, slotUpdate);
            }

            IItemStack InsertByProximity(int originSlot, IItemStack targetItemStack, List<IItemStack> allSlots, IItemStackFactory factory, Action<int> slotUpdate)
            {
                // 溢れたスタックを近い順に処理する
                // Handle overflow stacks based on proximity order
                var currentItemStack = targetItemStack;
                if (currentItemStack.Count == 0) return currentItemStack;
                foreach (var slot in EnumerateProximity(originSlot, allSlots.Count))
                {
                    if (!allSlots[slot].IsAllowedToAddWithRemain(currentItemStack)) continue;
                    var remain = InsertionItemBySlot(slot, currentItemStack, allSlots, factory, slotUpdate);
                    if (remain.Count == 0) return remain;
                    currentItemStack = remain;
                }

                return currentItemStack;
            }

            IEnumerable<int> EnumerateProximity(int originSlot, int inventorySize)
            {
                // 優先スロットからの距離順でスロットを返す
                // Return slots ordered by distance from the priority slot
                for (var offset = 1; offset < inventorySize; offset++)
                {
                    var left = originSlot - offset;
                    if (left >= 0) yield return left;

                    var right = originSlot + offset;
                    if (right < inventorySize) yield return right;
                }
            }

            #endregion
        }
        
        /// <summary>
        ///     指定されたスロットにアイテムを挿入する
        /// </summary>
        /// <returns>余ったアイテム 余ったアイテムがなければ空のアイテムを返す</returns>
        private static IItemStack InsertionItemBySlot(int slot, IItemStack itemStack, List<IItemStack> inventoryItems, IItemStackFactory itemStackFactory, Action<int> onSlotUpdate = null)
        {
            if (itemStack.Count == 0) return itemStack;
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
