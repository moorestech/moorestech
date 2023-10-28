using Core.Util;
using Game.Block.Interface;
using Game.World.Interface.DataStore;

namespace Game.World.Interface.Event
{
    public class BlockPlaceEventProperties
    {
        public readonly IBlock Block;
        public readonly BlockDirection BlockDirection;
        public readonly CoreVector2Int CoreVector2Int;

        public BlockPlaceEventProperties(CoreVector2Int coreVector2Int, IBlock block, BlockDirection blockDirection)
        {
            CoreVector2Int = coreVector2Int;
            Block = block;
            BlockDirection = blockDirection;
        }
    }
}