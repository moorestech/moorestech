using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Server.Core.Item.Config
{
    public interface IItemConfig
    {
        public IReadOnlyList<ItemConfigData> ItemConfigDataList { get; }
        public ItemConfigData GetItemConfig(int id);
        public ItemConfigData GetItemConfig(long itemHash);
        public int GetItemId(long itemHash);
        public List<int> GetItemIds(string modId);
        int GetItemId(string modId, string itemName, [CallerMemberName] string callerMethodName = "");
    }
}