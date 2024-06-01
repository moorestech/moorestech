using System.Runtime.Serialization;

namespace Core.Item.Config
{
    [DataContract]
    internal class ItemJson
    {
        [DataMember(Name = "items")] private ItemConfigData[] _item;
        
        public ItemConfigData[] Items => _item;
    }
}