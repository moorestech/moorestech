using System;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Unity.Collections;

namespace Game.Train.Unit.Containers
{
    public class ItemTrainCarContainer : ITrainCarContainer
    {
        private readonly ItemTrainCarContainerSlot[] _inventoryItems;
        
        private ItemTrainCarContainer(params IItemStack[] inventoryItems)
        {
            _inventoryItems = inventoryItems.Select((stack, i) => new ItemTrainCarContainerSlot { Index = i, Stack = stack }).ToArray();
        }
        
        public int GetWeight()
        {
            return TrainMotionParameters.WEIGHT_PER_SLOT * _inventoryItems.Length;
        }

        public bool IsFull()
        {
            return _inventoryItems.All(slot => slot.Stack.Id != ItemMaster.EmptyItemId && slot.Stack.Count >= MasterHolder.ItemMaster.GetItemMaster(slot.Stack.Id).MaxStack);
        }
        
        public bool IsEmpty()
        {
            return _inventoryItems.All(stack => stack.Stack.Id == ItemMaster.EmptyItemId || stack.Stack.Count == 0);
        }
        
        public ItemTrainCarContainerSlot SetItem(int index, IItemStack stack)
        {
            var original = _inventoryItems[index];
            _inventoryItems[index].Stack = stack;
            return original;
        }
        
        public bool CanInsert(ItemTrainCarContainer other)
        {
            if (other._inventoryItems.All(slot => slot.Stack.Id == ItemMaster.EmptyItemId)) return false;

            var itemStackMap = new NativeHashMap<ItemId, NativeList<int>>(other._inventoryItems.Length, Allocator.Temp);
            
            for (var i = _inventoryItems.Length - 1; i >= 0; i--)
            {
                var slotId = _inventoryItems[i].Stack.Id;
                if (itemStackMap.TryGetValue(slotId, out NativeList<int> stack))
                {
                    stack.Add(i);
                }
                else
                {
                    var list = new NativeList<int>(Allocator.Temp);
                    list.Add(i);
                    itemStackMap.Add(slotId, list);
                }
            }

            if (itemStackMap.ContainsKey(ItemMaster.EmptyItemId))
            {
                foreach (KVPair<ItemId, NativeList<int>> kvp in itemStackMap) kvp.Value.Dispose();
                itemStackMap.Dispose();
                return true;
            }

            for (var i = 0; i < other._inventoryItems.Length; i++)
            {
                var otherItemId = other._inventoryItems[i].Stack.Id;
                if (otherItemId == ItemMaster.EmptyItemId) continue;

                if (itemStackMap.TryGetValue(otherItemId, out NativeList<int> itemStackIndices))
                {
                    for (int j = 0; j < itemStackIndices.Length; j++)
                    {
                        var slotStack = _inventoryItems[itemStackIndices[j]].Stack;
                        if (slotStack.Count < MasterHolder.ItemMaster.GetItemMaster(slotStack.Id).MaxStack)
                        {
                            foreach (KVPair<ItemId, NativeList<int>> kvp in itemStackMap) kvp.Value.Dispose();
                            itemStackMap.Dispose();
                            return true;
                        }
                    }
                }
            }

            foreach (KVPair<ItemId, NativeList<int>> kvp in itemStackMap) kvp.Value.Dispose();
            itemStackMap.Dispose();
            return false;
        }
        
        public void MergeFrom(ItemTrainCarContainer other)
        {
            var itemStackMap = new NativeHashMap<ItemId, NativeList<int>>(other._inventoryItems.Length, Allocator.Temp);
            
            for (int i = _inventoryItems.Length - 1; i >= 0; i--)
            {
                var slotId = _inventoryItems[i].Stack.Id;
                if (itemStackMap.TryGetValue(slotId, out NativeList<int> stack))
                {
                    stack.Add(i);
                }
                else
                {
                    var list = new NativeList<int>(Allocator.Temp);
                    list.Add(i);
                    itemStackMap.Add(slotId, list);
                }
            }
            
            for (var i = 0; i < other._inventoryItems.Length; i++)
            {
                if (other._inventoryItems[i].Stack.Id == ItemMaster.EmptyItemId) continue;

                var addingItemStack = other._inventoryItems[i].Stack;
                if (itemStackMap.TryGetValue(addingItemStack.Id, out NativeList<int> itemStackIndices))
                {
                    for (int j = itemStackIndices.Length - 1; j >= 0; j--)
                    {
                        var result = _inventoryItems[itemStackIndices[j]].Stack.AddItem(addingItemStack);
                        _inventoryItems[itemStackIndices[j]].Stack = result.ProcessResultItemStack;
                        addingItemStack = result.RemainderItemStack;

                        var updatedStack = _inventoryItems[itemStackIndices[j]].Stack;
                        if (MasterHolder.ItemMaster.GetItemMaster(updatedStack.Id).MaxStack <= updatedStack.Count)
                        {
                            itemStackIndices.RemoveAt(j);
                        }
                    }
                }

                if (addingItemStack.Id != ItemMaster.EmptyItemId && itemStackMap.TryGetValue(ItemMaster.EmptyItemId, out NativeList<int> emptySlotIndices))
                {
                    while (addingItemStack.Id != ItemMaster.EmptyItemId && emptySlotIndices.Length > 0)
                    {
                        var emptyIndex = emptySlotIndices[emptySlotIndices.Length - 1];
                        var result = _inventoryItems[emptyIndex].Stack.AddItem(addingItemStack);
                        _inventoryItems[emptyIndex].Stack = result.ProcessResultItemStack;
                        addingItemStack = result.RemainderItemStack;

                        emptySlotIndices.RemoveAt(emptySlotIndices.Length - 1);
                        if (itemStackMap.TryGetValue(result.ProcessResultItemStack.Id, out NativeList<int> existingIndices))
                        {
                            existingIndices.Add(emptyIndex);
                        }
                        else
                        {
                            var list = new NativeList<int>(Allocator.Temp);
                            list.Add(emptyIndex);
                            itemStackMap.Add(result.ProcessResultItemStack.Id, list);
                        }
                    }
                }

                other._inventoryItems[i].Stack = addingItemStack;
            }
            
            foreach (KVPair<ItemId, NativeList<int>> kvp in itemStackMap) kvp.Value.Dispose();
            itemStackMap.Dispose();
        }
        
        public ReadOnlySpan<ItemTrainCarContainerSlot> InventoryItems => _inventoryItems;
        
        public static ItemTrainCarContainer CreateWithEmptySlots(int size)
        {
            return new ItemTrainCarContainer(Enumerable.Range(0, size).Select(i => ServerContext.ItemStackFactory.CreatEmpty()).ToArray());
        }
        
        public static ItemTrainCarContainer CreateWithInventoryItems(params IItemStack[] inventoryItems)
        {
            return new ItemTrainCarContainer(inventoryItems);
        }
    }
    
    public struct ItemTrainCarContainerSlot
    {
        public int Index;
        public IItemStack Stack;
    }
}