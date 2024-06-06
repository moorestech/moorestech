using System.Collections.Generic;
using Core.Const;
using Core.Item.Implementation;
using Core.Item.Interface;
using Core.Item.Interface.Config;

namespace Core.Item
{
    public class ItemStackFactory : IItemStackFactory
    {
        private readonly IItemConfig _itemConfig;
        private readonly IItemStack _nullItem;
        
        public ItemStackFactory(IItemConfig itemConfig)
        {
            _itemConfig = itemConfig;
            _nullItem = new NullItemStack();
            new InternalItemContext(this, itemConfig);
        }
        
        public IItemStack Create(int id, int count, Dictionary<string, ItemStackMetaData> metaData = null)
        {
            if (id == ItemConst.EmptyItemId) return CreatEmpty();
            
            if (count < 1) return CreatEmpty();
            
            metaData = metaData == null ? new Dictionary<string, ItemStackMetaData>() : new Dictionary<string, ItemStackMetaData>(metaData);
            return new ItemStack(id, count, metaData);
        }
        
        public IItemStack Create(int id, int count, long instanceId, Dictionary<string, ItemStackMetaData> metaData = null)
        {
            if (id == ItemConst.EmptyItemId) return CreatEmpty();
            
            if (count < 1) return CreatEmpty();
            
            metaData = metaData == null ? new Dictionary<string, ItemStackMetaData>() : new Dictionary<string, ItemStackMetaData>(metaData);
            return new ItemStack(id, count, instanceId, metaData);
        }
        
        public IItemStack Create(long itemHash, int count, Dictionary<string, ItemStackMetaData> metaData = null)
        {
            if (count < 1) return CreatEmpty();
            
            var id = _itemConfig.GetItemId(itemHash);
            if (id == ItemConst.EmptyItemId) return CreatEmpty();
            
            return Create(id, count, metaData);
        }
        
        public IItemStack CreatEmpty()
        {
            return _nullItem;
        }
        
        public IItemStack Create(string modId, string itemName, int count, Dictionary<string, ItemStackMetaData> metaData = null)
        {
            var id = _itemConfig.GetItemId(modId, itemName);
            return Create(id, count, metaData);
        }
    }
}