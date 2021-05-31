using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using static System.Int32;

namespace industrialization.Config.Item
{
    public static class ItemConfig
    {
        private static ItemConfigData[] _itemDatas;

        public static ItemConfigData GetItemConfig(int id)
        {
            _itemDatas ??= LoadJsonFile();
            
            //アイテムが登録されてないときの仮
            if (_itemDatas.Length-1 < id)
            {
                return new ItemConfigData("Null",id,MaxValue);
            }
            
            return _itemDatas[id];
        }

        private static ItemConfigData[] LoadJsonFile()
        {
            var json = File.ReadAllText(ConfigPath.ItemConfigPath);
            var ms = new MemoryStream(Encoding.UTF8.GetBytes((json)));
            ms.Seek(0, SeekOrigin.Begin);
            var serializer = new DataContractJsonSerializer(typeof(ItemJson));
            var data = serializer.ReadObject(ms) as ItemJson;
            return data?.Items;
        }
        
    }

    [DataContract] 
    public class ItemConfigData
    {
        [DataMember(Name = "name")]
        private string _name;
        [DataMember(Name = "id")]
        private int _id;
        [DataMember(Name = "stacks")]
        private int _stack;

        public ItemConfigData(string name, int id, int stack)
        {
            _name = name;
            _id = id;
            _stack = stack;
        }

        public string Name => _name;
        public int Id => _id;
        public int Stack => _stack;
    }
}