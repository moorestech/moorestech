using Core.Util;

namespace Game.Block.Interface.BlockConfig
{
    public class BlockConfigData
    {
        public readonly ulong BlockHash;

        public readonly int BlockId;
        public readonly int ItemId;
        public readonly ModelTransform ModelTransform;
        public readonly string ModId;
        public readonly string Name;
        public readonly IBlockConfigParam Param;
        public readonly string Type;

        public BlockConfigData(string modId, int blockId, string name, ulong blockHash, string type, IBlockConfigParam param, int itemId, ModelTransform modelTransform)
        {
            BlockId = blockId;
            Name = name;
            Type = type;
            Param = param;
            ItemId = itemId;
            ModelTransform = modelTransform;
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