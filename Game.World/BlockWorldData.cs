
using Core.Block;
using Game.World.Interface;
using Game.World.Interface.Util;

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

        public int X { get; }
        public int Y { get; }
        public IBlock Block { get; }
        public Coordinate Coordinate => CoordinateCreator.New(X, Y);
        public IBlockInventory BlockInventory { get; }
    }
}