
using Core.Block;
using Core.Block.BlockInventory;

namespace World
{
    internal class BlockWorldData
    {
        public BlockWorldData(IBlock block,int x, int y)
        {
            X = x;
            Y = y;
            Block = block;
        }

        private int X { get; }
        private int Y { get; }
        public IBlock Block { get; }
    }
}