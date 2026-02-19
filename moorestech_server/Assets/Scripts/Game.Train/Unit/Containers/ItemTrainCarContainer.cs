using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Unity.Collections;

namespace Game.Train.Unit.Containers
{
    public class ItemTrainCarContainer : ITrainCarContainer
    {
        private readonly IItemStack[] _inventoryItems;

        public ItemTrainCarContainer(IItemStack[] inventoryItems)
        {
            _inventoryItems = inventoryItems;
        }
        
        public int GetWeight()
        {
            return TrainMotionParameters.WEIGHT_PER_SLOT * _inventoryItems.Length;
        }

        public bool IsFull()
        {
            return _inventoryItems.All(stack => stack.Id != ItemMaster.EmptyItemId && stack.Count >= MasterHolder.ItemMaster.GetItemMaster(stack.Id).MaxStack);
        }
        
        public bool IsEmpty()
        {
            return _inventoryItems.All(stack => stack.Id == ItemMaster.EmptyItemId || stack.Count == 0);
        }
        
        public bool CanInsert(ItemTrainCarContainer other)
        {
            var itemStackMap = new NativeHashMap<ItemId, NativeList<int>>(other._inventoryItems.Length, Allocator.Temp);
            
            for (int i = _inventoryItems.Length - 1; i >= 0; i--)
            {
                var inventoryItemStack = _inventoryItems[i];
                if (itemStackMap.TryGetValue(inventoryItemStack.Id, out NativeList<int> stack))
                {
                    stack.Add(i);
                }
                else
                {
                    var list = new NativeList<int>(Allocator.Temp);
                    list.Add(i);
                    itemStackMap.Add(inventoryItemStack.Id, list);
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
                var otherInventoryItem = other._inventoryItems[i];
                if (otherInventoryItem.Id == ItemMaster.EmptyItemId) continue;
                
                var addingItemStack = otherInventoryItem;
                if (itemStackMap.TryGetValue(otherInventoryItem.Id, out NativeList<int> itemStackIndices))
                {
                    for (int j = 0; j < itemStackIndices.Length; j++)
                    {
                        if (_inventoryItems[itemStackIndices[j]].IsAllowedToAddWithRemain(addingItemStack))
                        {
                            foreach (KVPair<ItemId, NativeList<int>> kvp in itemStackMap) kvp.Value.Dispose();
                            itemStackMap.Dispose();
                            return true;
                        }
                    }
                }
                
                other._inventoryItems[i] = addingItemStack;
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
                var inventoryItemStack = _inventoryItems[i];
                if (itemStackMap.TryGetValue(inventoryItemStack.Id, out NativeList<int> stack))
                {
                    stack.Add(i);
                }
                else
                {
                    var list = new NativeList<int>(Allocator.Temp);
                    list.Add(i);
                    itemStackMap.Add(inventoryItemStack.Id, list);
                }   
            }
            
            for (var i = 0; i < other._inventoryItems.Length; i++)
            {
                var otherInventoryItem = other._inventoryItems[i];
                if (otherInventoryItem.Id == ItemMaster.EmptyItemId) continue;
                
                var addingItemStack = otherInventoryItem;
                if (itemStackMap.TryGetValue(otherInventoryItem.Id, out NativeList<int> itemStackIndices))
                {
                    for (int j = itemStackIndices.Length - 1; j >= 0; j--)
                    {
                        var result = _inventoryItems[itemStackIndices[j]].AddItem(addingItemStack);
                        _inventoryItems[itemStackIndices[j]] = result.ProcessResultItemStack;
                        addingItemStack = result.RemainderItemStack;
                        
                        if (MasterHolder.ItemMaster.GetItemMaster(_inventoryItems[itemStackIndices[j]].Id).MaxStack <= _inventoryItems[itemStackIndices[j]].Count)
                        {
                            itemStackIndices.RemoveAt(j);
                        }
                    }
                }

                // 余りがあれば空スロットに入れる
                // Put remainder into empty slots
                if (addingItemStack.Id != ItemMaster.EmptyItemId && itemStackMap.TryGetValue(ItemMaster.EmptyItemId, out NativeList<int> emptySlotIndices))
                {
                    while (addingItemStack.Id != ItemMaster.EmptyItemId && emptySlotIndices.Length > 0)
                    {
                        var emptyIndex = emptySlotIndices[emptySlotIndices.Length - 1];
                        var result = _inventoryItems[emptyIndex].AddItem(addingItemStack);
                        _inventoryItems[emptyIndex] = result.ProcessResultItemStack;
                        addingItemStack = result.RemainderItemStack;

                        // 空スロットを使用済みとして削除し、アイテムIDのマップに追加
                        // Remove used empty slot and add to item ID map
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
                
                other._inventoryItems[i] = addingItemStack;
            }
            
            foreach (KVPair<ItemId, NativeList<int>> kvp in itemStackMap) kvp.Value.Dispose();
            itemStackMap.Dispose();
        }
    }
}