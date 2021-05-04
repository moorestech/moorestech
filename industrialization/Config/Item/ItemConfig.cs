using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace industrialization.Config.Item
{
    public class ItemConfig
    {
        private static ItemConfigData[] _itemDatas;

        public static ItemConfigData GetInstallationsConfig(int id)
        {
            _itemDatas ??= LoadJsonFile();

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
        
        public string Name => _name;
        public int Id => _id;
        public int Stack => _stack;
    }
}