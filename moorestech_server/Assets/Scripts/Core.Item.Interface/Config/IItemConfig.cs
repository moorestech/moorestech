using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Core.Item.Config
{
    public interface IItemConfig
    {
        public IReadOnlyList<IItemConfigData> ItemConfigDataList { get; }
        public IItemConfigData GetItemConfig(int id);
        public IItemConfigData GetItemConfig(long itemHash);
        public int GetItemId(long itemHash);
        public List<int> GetItemIds(string modId);
        int GetItemId(string modId, string itemName, [CallerMemberName] string callerMethodName = "");
    }
}