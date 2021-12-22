using Core.Block;

namespace World
{
    public class WorldBlockData
    {
        public WorldBlockData(IBlock block,int x, int y, BlockDirection direction)
        {
            X = x;
            Y = y;
            Direction = direction;
            Block = block;
        }

        public int X { get; }
        public int Y { get; }
        public IBlock Block { get; }
        public BlockDirection Direction { get; }
    }
}