using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    /// <summary>
    /// TODO こういうのinterfaceにする
    /// </summary>
    public class WorldBlockData
    {
        public WorldBlockData(IBlock block, Vector3Int originalPos, BlockDirection blockDirection, IBlockConfig blockConfig)
        {
            Block = block;
            var blockSize = blockConfig.GetBlockConfig(block.BlockId).BlockSize;
            BlockPositionInfo = new BlockPositionInfo(originalPos, blockDirection, blockSize);
        }
        public IBlock Block { get; }
        public BlockPositionInfo BlockPositionInfo { get; }
    }
}