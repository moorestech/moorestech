using System.Collections.Generic;

namespace Core.Item.Config
{
    public interface IItemConfig
    {
        public ItemConfigData GetItemConfig(int id);
        public ItemConfigData GetItemConfig(ulong itemHash);
        public int GetItemId(ulong itemHash);
        public List<int> GetItemIds(string modId);
        int GetItemId(string modId, string itemName);
    }
}