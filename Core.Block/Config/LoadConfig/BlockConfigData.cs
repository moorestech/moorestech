using Core.Block.Config.LoadConfig.Param;

namespace Core.Block.Config.LoadConfig
{
    public class BlockConfigData
    {
        public readonly int BlockId;
        public readonly string Name;
        public readonly string Type;
        public readonly IBlockConfigParam Param;
        public readonly int ItemId;
        public readonly ulong BlockHash;

        public BlockConfigData(int blockId, string name, ulong blockHash, string type, IBlockConfigParam param, int itemId)
        {
            BlockId = blockId;
            Name = name;
            Type = type;
            Param = param;
            ItemId = itemId;
            BlockHash = blockHash;
        }
    }
}