using Core.Block.BlockInventory;

namespace Game.World.Interface
{
    public interface IWorldBlockInventoryDatastore
    {
        public IBlockInventory GetBlock(int x, int y);
    }
}