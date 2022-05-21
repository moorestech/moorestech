namespace Core.Item.Config
{
    public interface IItemConfig
    {
        public ItemConfigData GetItemConfig(int id);
        public ItemConfigData GetItemConfig(ulong itemHash);
        public int GetItemId(ulong itemHash);
    }
}