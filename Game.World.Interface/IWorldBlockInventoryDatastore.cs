using Core.Block.BlockInventory;

namespace Game.World.Interface
{
    public interface IWorldBlockInventoryDatastore
    {
        public bool ExistsBlockInventory(int x, int y);
        public IBlockInventory GetBlock(int x, int y);
    }
}