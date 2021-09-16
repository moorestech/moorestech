using System.Runtime.Serialization;

namespace Core.Config.Installation
{
    [DataContract] 
    class BlockJson
    {
        [DataMember(Name = "Blocks")]
        private BlockData[] _blocks;

        public BlockData[] Blocks => _blocks;
    }
}