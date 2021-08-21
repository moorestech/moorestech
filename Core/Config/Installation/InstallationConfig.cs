using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using industrialization.Core.Config.Block;

namespace industrialization.Core.Config.Installation
{
    public static class BlockConfig
    {
        private static BlockData[] _machineDatas;

        public static BlockData GetBlocksConfig(uint id)
        {
            _machineDatas ??= LoadJsonFile();

            return _machineDatas[id];
        }

        private static BlockData[] LoadJsonFile()
        {
            var json = File.ReadAllText(ConfigPath.BlockConfigPath);
            var ms = new MemoryStream(Encoding.UTF8.GetBytes((json)));
            ms.Seek(0, SeekOrigin.Begin);
            var serializer = new DataContractJsonSerializer(typeof(BlockJson));
            var data = serializer.ReadObject(ms) as BlockJson;
            return data?.Blocks;
        }
    }

    [DataContract] 
    public class BlockData
    {
        [DataMember(Name = "name")]
        private string _name;

        [DataMember(Name = "inputSlot")]
        private int _inputSlot;
        
        [DataMember(Name = "outputSlot")]
        private int _outputSlot;
        
        public string Name => _name;

        public int InputSlot => _inputSlot;
        public int OutputSlot => _outputSlot;
    }
}