using System.Runtime.Serialization;
using industrialization.Config.Installation;

namespace industrialization.Config.Item
{
    [DataContract]
    internal class ItemJson
    {
        [DataMember(Name = "items")]
        private ItemConfigData[] _item;

        public ItemConfigData[] Items => _item;
    }
}