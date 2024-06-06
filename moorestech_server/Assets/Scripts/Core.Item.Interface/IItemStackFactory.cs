using System.Collections.Generic;

namespace Core.Item.Interface
{
    public interface IItemStackFactory
    {
        public IItemStack Create(int id, int count, Dictionary<string, ItemStackMetaData> metaData = null);
        public IItemStack Create(int id, int count, long instanceId, Dictionary<string, ItemStackMetaData> metaData = null);
        public IItemStack Create(long itemHash, int count, Dictionary<string, ItemStackMetaData> metaData = null);
        public IItemStack Create(string modId, string itemName, int count, Dictionary<string, ItemStackMetaData> metaData = null);
        
        public IItemStack CreatEmpty();
    }
}