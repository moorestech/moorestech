using System;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using MessagePack;
using MessagePack.Formatters;
using Unity.Collections;

namespace Game.Train.Unit.Containers
{
    [MessagePackObject]
    public class ItemTrainCarContainer : ITrainCarContainer
    {
        [Key(0)] public ItemTrainCarContainerSlot[] InventoryItems;
        
        [Obsolete]
        public ItemTrainCarContainer() { }
        
        private ItemTrainCarContainer(params IItemStack[] inventoryItems)
        {
            InventoryItems = inventoryItems.Select((stack, i) => new ItemTrainCarContainerSlot { Index = i, Stack = stack }).ToArray();
        }
        
        public int GetWeight()
        {
            return TrainMotionParameters.WEIGHT_PER_SLOT * InventoryItems.Length;
        }

        public bool IsFull()
        {
            return InventoryItems.All(slot => slot.Stack.Id != ItemMaster.EmptyItemId && slot.Stack.Count >= MasterHolder.ItemMaster.GetItemMaster(slot.Stack.Id).MaxStack);
        }
        
        public bool IsEmpty()
        {
            return InventoryItems.All(stack => stack.Stack.Id == ItemMaster.EmptyItemId || stack.Stack.Count == 0);
        }
        
        public ItemTrainCarContainerSlot SetItem(int index, IItemStack stack)
        {
            var original = InventoryItems[index];
            InventoryItems[index].Stack = stack;
            return original;
        }
        
        public bool CanInsert(ItemTrainCarContainer other)
        {
            if (other.InventoryItems.All(slot => slot.Stack.Id == ItemMaster.EmptyItemId)) return false;
            
            var itemStackMap = new NativeHashMap<ItemId, NativeList<int>>(other.InventoryItems.Length, Allocator.Temp);
            
            for (var i = InventoryItems.Length - 1; i >= 0; i--)
            {
                var slotId = InventoryItems[i].Stack.Id;
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
            
            for (var i = 0; i < other.InventoryItems.Length; i++)
            {
                var otherItemId = other.InventoryItems[i].Stack.Id;
                if (otherItemId == ItemMaster.EmptyItemId) continue;

                if (itemStackMap.TryGetValue(otherItemId, out NativeList<int> itemStackIndices))
                {
                    for (int j = 0; j < itemStackIndices.Length; j++)
                    {
                        var slotStack = InventoryItems[itemStackIndices[j]].Stack;
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
            var itemStackMap = new NativeHashMap<ItemId, NativeList<int>>(InventoryItems.Length, Allocator.Temp);
            
            for (var i = InventoryItems.Length - 1; i >= 0; i--)
            {
                var slotId = InventoryItems[i].Stack.Id;
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
            
            for (var i = 0; i < other.InventoryItems.Length; i++)
            {
                if (other.InventoryItems[i].Stack.Id == ItemMaster.EmptyItemId) continue;
                
                var addingItemStack = other.InventoryItems[i].Stack;
                if (itemStackMap.TryGetValue(addingItemStack.Id, out NativeList<int> itemStackIndices))
                {
                    for (int j = itemStackIndices.Length - 1; j >= 0; j--)
                    {
                        var result = InventoryItems[itemStackIndices[j]].Stack.AddItem(addingItemStack);
                        InventoryItems[itemStackIndices[j]].Stack = result.ProcessResultItemStack;
                        addingItemStack = result.RemainderItemStack;
                        
                        var updatedStack = InventoryItems[itemStackIndices[j]].Stack;
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
                        var result = InventoryItems[emptyIndex].Stack.AddItem(addingItemStack);
                        InventoryItems[emptyIndex].Stack = result.ProcessResultItemStack;
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
                
                other.InventoryItems[i].Stack = addingItemStack;
            }
            
            foreach (KVPair<ItemId, NativeList<int>> kvp in itemStackMap) kvp.Value.Dispose();
            itemStackMap.Dispose();
        }
        
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
    
    // TODO: IItemStack自体をMessagePackでシリアライズできるようにしてこのformatterは消す
    public class ItemTrainCarContainerSlotFormatter : IMessagePackFormatter<ItemTrainCarContainerSlot>
    {
        public void Serialize(ref MessagePackWriter writer, ItemTrainCarContainerSlot value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(3);
            writer.WriteInt32(value.Index);
            writer.WriteInt32(value.Stack.Count);
            MessagePackSerializer.Serialize(ref writer, value.Stack.Id, options);
        }
        
        public ItemTrainCarContainerSlot Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            reader.ReadArrayHeader();
            var index = reader.ReadInt32();
            var count = reader.ReadInt32();
            var id = MessagePackSerializer.Deserialize<ItemId>(ref reader, options);
            return new ItemTrainCarContainerSlot { Index = index, Stack = ServerContext.ItemStackFactory.Create(id, count) };
        }
    }
}