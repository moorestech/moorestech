using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Core.Config.Item;
using Core.ConfigJson;
using static System.Int32;

namespace Core.Item.Config
{
    public class ItemConfig : IItemConfig
    {
        private ItemConfigData[] _itemDatas;

        public ItemConfig(ConfigPath configPath)
        {
            var json = File.ReadAllText(configPath.ItemConfigPath);
            var ms = new MemoryStream(Encoding.UTF8.GetBytes((json)));
            ms.Seek(0, SeekOrigin.Begin);
            var serializer = new DataContractJsonSerializer(typeof(ItemJson));
            var data = serializer.ReadObject(ms) as ItemJson;
            _itemDatas = data.Items;
        }

        public ItemConfigData GetItemConfig(int id)
        {
            //アイテムが登録されてないときの仮
            if (_itemDatas.Length - 1 < id)
            {
                return new ItemConfigData("undefined id " + id, id, MaxValue);
            }

            return _itemDatas[id];
        }
    }

    [DataContract]
    public class ItemConfigData
    {
        [DataMember(Name = "name")] private string _name;
        [DataMember(Name = "id")] private int _id;
        [DataMember(Name = "max_stacks")] private int _maxStack;

        public ItemConfigData(string name, int id, int maxStack)
        {
            _name = name;
            _id = id;
            _maxStack = maxStack;
        }

        public string Name => _name;
        public int Id => _id;
        public int MaxStack => _maxStack;
    }
}