using System.Runtime.Serialization;
using industrialization.Core.Config.Installation;

namespace industrialization.Core.Config.Block
{
    [DataContract] 
    class BlockJson
    {
        [DataMember(Name = "Blocks")]
        private BlockData[] _blocks;

        public BlockData[] Blocks => _blocks;
    }
}