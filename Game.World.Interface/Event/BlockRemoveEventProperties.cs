using Core.Block;
using Game.World.Interface.DataStore;

namespace Game.World.Interface.Event
{
    public class BlockRemoveEventProperties
    {
        public readonly Coordinate Coordinate;
        public readonly IBlock Block;
        public readonly BlockDirection BlockDirection;

        public BlockRemoveEventProperties(Coordinate coordinate, IBlock block,BlockDirection blockDirection)
        {
            Coordinate = coordinate;
            Block = block;
            BlockDirection = blockDirection;
        }
    }
}