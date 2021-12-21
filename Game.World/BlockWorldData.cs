
using Core.Block;
using Core.Block.BlockInventory;

namespace World
{
    internal class BlockWorldData
    {
        public BlockWorldData(IBlock block,int x, int y, BlockDirection direction)
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