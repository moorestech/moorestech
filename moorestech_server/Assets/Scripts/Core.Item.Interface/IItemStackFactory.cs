using System;
using System.Collections.Generic;
using Core.Master;

namespace Core.Item.Interface
{
    public interface IItemStackFactory
    {
        public IItemStack Create(ItemId id, int count, Dictionary<string, ItemStackMetaData> metaData = null);
        public IItemStack Create(ItemId id, int count, ItemInstanceId instanceId, Dictionary<string, ItemStackMetaData> metaData = null);
        public IItemStack Create(Guid itemGuid, int count, Dictionary<string, ItemStackMetaData> metaData = null);
        
        // maxStackを超える個数は1回の生成要求で分割して返す。呼び出し側ごとの分割複製を作らない
        // Counts above maxStack are split inside one creation request, so no caller re-implements the split
        public List<IItemStack> CreateSplitStacks(ItemId id, int totalCount);

        public IItemStack CreatEmpty();
    }
}