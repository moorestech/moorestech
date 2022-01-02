using Core.Block;
using Core.Block.Blocks;
using Game.World.Interface.DataStore;

namespace Game.World.Interface.Event
{
    public class BlockRemoveEventProperties
    {
        public readonly Coordinate Coordinate;
        public readonly IBlock Block;

        public BlockRemoveEventProperties(Coordinate coordinate, IBlock block)
        {
            Coordinate = coordinate;
            Block = block;
        }
    }
}