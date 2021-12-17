
using Core.Block;
using Core.Block.BlockInventory;

namespace World
{
    internal class BlockWorldData
    {
        public BlockWorldData(IBlock block,int x, int y,  IBlockInventory blockInventory)
        {
            X = x;
            Y = y;
            Block = block;
            BlockInventory = blockInventory;
        }

        private int X { get; }
        private int Y { get; }
        public IBlock Block { get; }
        public IBlockInventory BlockInventory { get; }
    }
}