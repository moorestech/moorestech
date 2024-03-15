using UnityEngine;

namespace Game.Block.Interface.BlockConfig
{
    public class BlockConfigData
    {
        public readonly long BlockHash;

        public readonly int BlockId;
        public readonly Vector3Int BlockSize;
        public readonly int ItemId;
        public readonly ModelTransform ModelTransform;
        public readonly string ModId;
        public readonly string Name;
        public readonly IBlockConfigParam Param;
        public readonly string Type;

        public BlockConfigData(string modId, int blockId, string name, long blockHash, string type,
            IBlockConfigParam param, int itemId, ModelTransform modelTransform, Vector3Int blockSize)
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
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale;

        public ModelTransform(Vector3 position, Vector3 rotation, Vector3 scale)
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