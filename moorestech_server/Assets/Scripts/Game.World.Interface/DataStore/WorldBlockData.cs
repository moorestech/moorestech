using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    /// <summary>
    ///     TODO こういうのinterfaceにする
    /// </summary>
    public class WorldBlockData
    {
        public IBlock Block { get; }
        public BlockPositionInfo BlockPositionInfo { get; }
        
        public WorldBlockData(IBlock block, Vector3Int originalPos, BlockDirection blockDirection)
        {
            Block = block;
            var blockSize = MasterHolder.BlockMaster.GetBlockMaster(block.BlockId).BlockSize;
            BlockPositionInfo = new BlockPositionInfo(originalPos, blockDirection, blockSize);
        }
    }
}