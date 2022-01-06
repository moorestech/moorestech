using Core.Block;
using Core.Block.Blocks;
using Game.World.Interface.DataStore;

namespace World.DataStore
{
    public class WorldBlockData
    {
        public WorldBlockData(IBlock block, int x, int y, BlockDirection blockDirection)
        {
            X = x;
            Y = y;
            BlockDirection = blockDirection;
            Block = block;
        }

        public int X { get; }
        public int Y { get; }
        public IBlock Block { get; }
        public BlockDirection BlockDirection { get; }
    }
}