using Core.Block.Config.LoadConfig.Param;

namespace Core.Block.Config.LoadConfig
{
    public class BlockConfigData
    {
        public readonly int BlockId;
        public readonly string Name;
        public readonly string Type;
        public readonly BlockConfigParamBase Param;
        public readonly int ItemId;

        public BlockConfigData(int blockId, string name, string type, BlockConfigParamBase param, int itemId)
        {
            BlockId = blockId;
            Name = name;
            Type = type;
            Param = param;
            ItemId = itemId;
        }
    }
}