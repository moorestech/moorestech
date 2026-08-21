using System;
using System.Collections.Generic;
using Core.Item.Implementation;
using Core.Item.Interface;
using Core.Master;

namespace Core.Item
{
    public class ItemStackFactory : IItemStackFactory
    {
        private readonly IItemStack _nullItem;
        
        public ItemStackFactory()
        {
            _nullItem = new NullItemStack();
            new InternalItemContext(this);
        }
        
        public IItemStack Create(ItemId id, int count, Dictionary<string, ItemStackMetaData> metaData = null)
        {
            if (id == ItemMaster.EmptyItemId) return CreatEmpty();
            
            if (count < 1) return CreatEmpty();
            
            metaData = metaData == null ? new Dictionary<string, ItemStackMetaData>() : new Dictionary<string, ItemStackMetaData>(metaData);
            return new ItemStack(id, count, metaData);
        }
        
        public IItemStack Create(ItemId id, int count, ItemInstanceId instanceId, Dictionary<string, ItemStackMetaData> metaData = null)
        {
            if (id == ItemMaster.EmptyItemId) return CreatEmpty();
            
            if (count < 1) return CreatEmpty();
            
            metaData = metaData == null ? new Dictionary<string, ItemStackMetaData>() : new Dictionary<string, ItemStackMetaData>(metaData);
            return new ItemStack(id, count, instanceId, metaData);
        }
        public IItemStack Create(Guid itemGuid, int count, Dictionary<string, ItemStackMetaData> metaData = null)
        {
            if (count < 1) return CreatEmpty();
            
            var id = MasterHolder.ItemMaster.GetItemId(itemGuid);
            return Create(id, count, metaData);
        }
        
        public List<IItemStack> CreateSplitStacks(ItemId id, int totalCount)
        {
            var stacks = new List<IItemStack>();
            if (id == ItemMaster.EmptyItemId || totalCount < 1) return stacks;

            // 最大スタック数を超える場合は分割して追加
            // Split into multiple stacks if exceeding max stack size
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(id);
            var fullStackCount = totalCount / maxStack;
            for (var i = 0; i < fullStackCount; i++)
            {
                stacks.Add(Create(id, maxStack));
            }

            // あまりを追加する
            // Add remainder
            var remainCount = totalCount % maxStack;
            if (remainCount != 0) stacks.Add(Create(id, remainCount));

            return stacks;
        }

        public IItemStack CreatEmpty()
        {
            return _nullItem;
        }
    }
}