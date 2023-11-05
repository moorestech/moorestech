using Game.Block.Interface;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.World.Interface.Event
{
    public class BlockPlaceEventProperties
    {
        public readonly IBlock Block;
        public readonly BlockDirection BlockDirection;
        public readonly Vector2Int CoreVector2Int;

        public BlockPlaceEventProperties(Vector2Int coreVector2Int, IBlock block, BlockDirection blockDirection)
        {
            CoreVector2Int = coreVector2Int;
            Block = block;
            BlockDirection = blockDirection;
        }
    }
}