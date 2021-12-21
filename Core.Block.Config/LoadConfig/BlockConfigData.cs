using Core.Block.Config.LoadConfig.Param;

namespace Core.Block.Config.LoadConfig
{
    public class BlockConfigData
    {
        public readonly int BlockId;
        public readonly string Name;
        public readonly string Type;
        public readonly BlockConfigParamBase Param;

        public BlockConfigData(int blockId, string name, string type, BlockConfigParamBase param)
        {
            this.BlockId = blockId;
            this.Name = name;
            this.Type = type;
            this.Param = param;
        }
    }
}