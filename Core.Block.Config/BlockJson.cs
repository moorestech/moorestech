using System.Runtime.Serialization;

namespace Core.Block.Config
{
    [DataContract] 
    class BlockJson
    {
        [DataMember(Name = "Blocks")]
        private BlockData[] _blocks;

        public BlockData[] Blocks => _blocks;
    }
}