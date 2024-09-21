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
        
        public IItemStack CreatEmpty();
    }
}