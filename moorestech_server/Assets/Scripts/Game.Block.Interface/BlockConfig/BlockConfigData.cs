using Core.Util;

namespace Game.Block.Interface.BlockConfig
{
    public class BlockConfigData
    {
        public readonly long BlockHash;

        public readonly int BlockId;
        public readonly int ItemId;
        public readonly ModelTransform ModelTransform;
        public readonly string ModId;
        public readonly string Name;
        public readonly IBlockConfigParam Param;
        public readonly string Type;
        public readonly CoreVector2Int BlockSize;

        public BlockConfigData(string modId, int blockId, string name, long blockHash, string type, IBlockConfigParam param, int itemId, ModelTransform modelTransform, CoreVector2Int blockSize)
        {
            BlockId = blockId;
            Name = name;
            Type = type;
            Param = param;
            ItemId = itemId;
            ModelTransform = modelTransform;
            BlockSize = blockSize;
            ModId = modId;
            BlockHash = blockHash;
        }
    }

    public class ModelTransform
    {
        public CoreVector3 Position;
        public CoreVector3 Rotation;
        public CoreVector3 Scale;

        public ModelTransform(CoreVector3 position, CoreVector3 rotation, CoreVector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public ModelTransform()
        {
        }
    }
}