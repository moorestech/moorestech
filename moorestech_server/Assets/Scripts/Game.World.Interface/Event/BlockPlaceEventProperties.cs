using Game.Block.Interface;
using UnityEngine;

namespace Game.World.Interface.Event
{
    public class BlockPlaceEventProperties
    {
        public readonly IBlock Block;
        public readonly BlockDirection BlockDirection;
        public readonly Vector3Int Pos;

        public BlockPlaceEventProperties(Vector3Int pos, IBlock block, BlockDirection blockDirection)
        {
            Pos = pos;
            Block = block;
            BlockDirection = blockDirection;
        }
    }
}