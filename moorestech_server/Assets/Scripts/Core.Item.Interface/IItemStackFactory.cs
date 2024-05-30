namespace Core.Item.Interface
{
    public interface IItemStackFactory
    {
        public IItemStack Create(int id, int count);
        public IItemStack Create(int id, int count, long instanceId);
        public IItemStack Create(long itemHash, int count);
        public IItemStack Create(string modId, string itemName, int count);
        
        public IItemStack CreatEmpty();
    }
}