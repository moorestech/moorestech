using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Core.Item.Config
{
    public interface IItemConfig
    {
        public ItemConfigData GetItemConfig(int id);
        public ItemConfigData GetItemConfig(ulong itemHash);
        public int GetItemId(ulong itemHash);
        public List<int> GetItemIds(string modId);
        int GetItemId(string modId, string itemName, [CallerMemberName] string callerMethodName = "");
    }
}