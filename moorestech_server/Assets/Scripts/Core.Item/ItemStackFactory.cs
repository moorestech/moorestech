using System;
using System.Collections.Generic;
using Core.Item.Implementation;
using Core.Item.Interface;
using Core.Master;

namespace Core.Item
{
    public class ItemStackFactory : IItemStackFactory
    {
        private static readonly Dictionary<string, ItemStackMetaData> EmptyMetaData = new();
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
            
            metaData = CopyMetaData(metaData);
            return new ItemStack(id, count, metaData);
        }
        
        public IItemStack Create(ItemId id, int count, ItemInstanceId instanceId, Dictionary<string, ItemStackMetaData> metaData = null)
        {
            if (id == ItemMaster.EmptyItemId) return CreatEmpty();
            
            if (count < 1) return CreatEmpty();
            
            metaData = CopyMetaData(metaData);
            return new ItemStack(id, count, instanceId, metaData);
        }

        // 属性の無いアイテムは空の辞書1個を共有する。ItemStackは辞書を書き換えず、SetMetaは必ず複製へ書く
        // Items without metadata share one empty dictionary; ItemStack never mutates it, and SetMeta always writes to a copy
        private static Dictionary<string, ItemStackMetaData> CopyMetaData(Dictionary<string, ItemStackMetaData> metaData)
        {
            if (metaData == null || metaData.Count == 0) return EmptyMetaData;
            return new Dictionary<string, ItemStackMetaData>(metaData);
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