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
            
            for (var i = 0; i < other._inventoryItems.Length; i++)
            {
                var otherInventoryItem = other._inventoryItems[i];
                if (otherInventoryItem.Id == ItemMaster.EmptyItemId) continue;
                
                var addingItemStack = otherInventoryItem;
                if (itemStackMap.TryGetValue(otherInventoryItem.Id, out NativeList<int> itemStackIndices))
                {
                    for (int j = 0; j < itemStackIndices.Length; j++)
                    {
                        if (_inventoryItems[j].IsAllowedToAddWithRemain(addingItemStack))
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
                    for (int j = 0; j < itemStackIndices.Length; j++)
                    {
                        var result = _inventoryItems[j].AddItem(addingItemStack);
                        _inventoryItems[j] = result.ProcessResultItemStack;
                        addingItemStack = result.RemainderItemStack;
                    }
                }
                
                other._inventoryItems[i] = addingItemStack;
            }
            
            foreach (KVPair<ItemId, NativeList<int>> kvp in itemStackMap) kvp.Value.Dispose();
            itemStackMap.Dispose();
        }
    }
}