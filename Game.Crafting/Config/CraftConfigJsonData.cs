using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Game.Crafting.Config
{
    [DataContract]
    public class CraftConfigJsonData
    {
        
        [DataMember(Name = "recipes")] private CraftConfigDataElement[] _craftConfigElements;

        public CraftConfigDataElement[] CraftConfigElements => _craftConfigElements;
    }

    [DataContract]
    public class CraftConfigDataElement
    {
        [DataMember(Name = "items")] private List<CraftItem> _items;
        [DataMember(Name = "result")] private CraftItem _result;

        public List<CraftItem> Items => _items;

        public CraftItem Result => _result;
    }

    [DataContract]
    public class CraftItem
    {
        [DataMember(Name = "id")] private int _id;
        [DataMember(Name = "count")] private int _count;

        public int Id => _id;

        public int Count => _count;
    }
}