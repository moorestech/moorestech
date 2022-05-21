namespace Core.Item.Config
{
    public interface IItemConfig
    {
        public ItemConfigData GetItemConfig(int id);
        public int GetItemId(ulong itemHash);
    }
}