using System;
using System.Collections.Generic;
using Core.Item;

namespace Core.Inventory
{
    /// <summary>
    ///     
    /// </summary>
    internal static class InventoryInsertItem
    {

        ///     

        /// <param name="insertItemStack"></param>
        /// <param name="inventoryItems"></param>
        /// <param name="itemStackFactory">ItemStackFactory</param>
        /// <param name="onSlotUpdate"></param>
        /// <returns></returns>
        internal static List<IItemStack> InsertItem(List<IItemStack> insertItemStack, List<IItemStack> inventoryItems, ItemStackFactory itemStackFactory, Action<int> onSlotUpdate = null)
        {
            var reminderItemStacks = new List<IItemStack>();

            foreach (var item in insertItemStack)
            {
                var remindItemStack = InsertItem(item, inventoryItems, itemStackFactory, onSlotUpdate);
                if (remindItemStack.Equals(itemStackFactory.CreatEmpty())) continue;

                reminderItemStacks.Add(remindItemStack);
            }

            return reminderItemStacks;
        }



        ///     

        /// <param name="insertItemStack"></param>
        /// <param name="inventoryItems"></param>
        /// <param name="itemStackFactory">ItemStackFactory</param>
        /// <param name="onSlotUpdate"></param>
        /// <returns></returns>
        internal static IItemStack InsertItem(IItemStack insertItemStack, List<IItemStack> inventoryItems, ItemStackFactory itemStackFactory, Action<int> onSlotUpdate = null)
        {
            for (var i = 0; i < inventoryItems.Count; i++)
            {
                
                if (!inventoryItems[i].IsAllowedToAddWithRemain(insertItemStack)) continue;

                
                var remain = InsertionItemBySlot(i, insertItemStack, inventoryItems, itemStackFactory, onSlotUpdate);

                
                if (remain.Equals(itemStackFactory.CreatEmpty())) return remain;
                
                insertItemStack = remain;
            }

            return insertItemStack;
        }


        ///     
        ///     

        public static IItemStack InsertItemWithPrioritySlot(IItemStack itemStack, List<IItemStack> inventory, ItemStackFactory itemStackFactory, int[] prioritySlots, Action<int> invokeEvent)
        {
            
            var remainItem = itemStack;
            foreach (var prioritySlot in prioritySlots) remainItem = InsertionItemBySlot(prioritySlot, remainItem, inventory, itemStackFactory, invokeEvent);

            
            return InsertItem(remainItem, inventory, itemStackFactory, invokeEvent);
        }


        ///     

        /// <returns> </returns>
        private static IItemStack InsertionItemBySlot(int slot, IItemStack itemStack, List<IItemStack> inventoryItems, ItemStackFactory itemStackFactory, Action<int> onSlotUpdate = null)
        {
            if (itemStack.Equals(itemStackFactory.CreatEmpty())) return itemStack;
            if (!inventoryItems[slot].IsAllowedToAddWithRemain(itemStack)) return itemStack;

            var result = inventoryItems[slot].AddItem(itemStack);

            
            if (!inventoryItems[slot].Equals(result.ProcessResultItemStack))
            {
                inventoryItems[slot] = result.ProcessResultItemStack;
                onSlotUpdate?.Invoke(slot);
            }

            return result.RemainderItemStack;
        }
    }
}