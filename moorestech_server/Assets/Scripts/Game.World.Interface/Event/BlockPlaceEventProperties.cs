using Game.Block.Interface;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.World.Interface.Event
{
    public class BlockPlaceEventProperties
    {
        public readonly IBlock Block;
        public readonly BlockDirection BlockDirection;
        public readonly Vector2Int Pos;

        public BlockPlaceEventProperties(Vector2Int pos, IBlock block, BlockDirection blockDirection)
        {
            Pos = pos;
            Block = block;
            BlockDirection = blockDirection;
        }
    }
}