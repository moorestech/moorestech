using Game.Block;
using Game.Block.Interface;
using Game.World.Interface.DataStore;

namespace Game.World.Interface.Event
{
    public class BlockPlaceEventProperties
    {
        public readonly Coordinate Coordinate;
        public readonly IBlock Block;
        public readonly BlockDirection BlockDirection;

        public BlockPlaceEventProperties(Coordinate coordinate, IBlock block, BlockDirection blockDirection)
        {
            Coordinate = coordinate;
            Block = block;
            BlockDirection = blockDirection;
        }
    }
}