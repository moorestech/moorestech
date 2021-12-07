using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Core.Block.Config
{
    public class TestBlockConfig : IBlockConfig
    {
        private readonly BlockData[] _machineData;
        private BlockData _nullData;

        public TestBlockConfig()
        {
            var json = File.ReadAllText(ConfigPath.ConfigPath.BlockConfigPath);
            var ms = new MemoryStream(Encoding.UTF8.GetBytes((json)));
            ms.Seek(0, SeekOrigin.Begin);
            var serializer = new DataContractJsonSerializer(typeof(BlockJson));
            var data = serializer.ReadObject(ms) as BlockJson;
            _machineData = data?.Blocks;
        }

        public BlockData GetBlocksConfig(int id)
        {
            if ( _machineData.Length <= id)
            {
                return _nullData ??= new BlockData("null", 0, 0);
            }

            return _machineData[id];
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

        public BlockData(string name, int inputSlot, int outputSlot)
        {
            _name = name;
            _inputSlot = inputSlot;
            _outputSlot = outputSlot;
        }

        public string Name => _name;

        public int InputSlot => _inputSlot;
        public int OutputSlot => _outputSlot;
    }
}