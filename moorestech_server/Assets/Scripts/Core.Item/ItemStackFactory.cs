using System;
using System.Collections.Generic;
using Core.Const;
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
            if (id == ItemConst.EmptyItemId) return CreatEmpty();
            
            if (count < 1) return CreatEmpty();
            
            metaData = metaData == null ? new Dictionary<string, ItemStackMetaData>() : new Dictionary<string, ItemStackMetaData>(metaData);
            return new ItemStack(id, count, metaData);
        }
        
        public IItemStack Create(ItemId id, int count, ItemInstanceId instanceId, Dictionary<string, ItemStackMetaData> metaData = null)
        {
            if (id == ItemConst.EmptyItemId) return CreatEmpty();
            
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
        
        public IItemStack CreatEmpty()
        {
            return _nullItem;
        }
    }
}