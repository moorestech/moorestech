using Core.Block;

namespace Game.World.Interface.Event
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