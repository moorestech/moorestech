using System.Runtime.Serialization;
using industrialization.Config.Installation;

namespace industrialization.Config.Item
{
    [DataContract] 
    class ItemJson
    {
        [DataMember(Name = "installations")]
        private ItemConfigData[] _item;

        public ItemConfigData[] Items => _item;
    }
}