using Core.Block;
using World.DataStore;

namespace World.Event
{
    public class BlockPlaceEventProperties
    {
        public readonly Coordinate Coordinate;
        public readonly IBlock Block;

        public BlockPlaceEventProperties(Coordinate coordinate, IBlock block)
        {
            Coordinate = coordinate;
            Block = block;
        }
    }
}