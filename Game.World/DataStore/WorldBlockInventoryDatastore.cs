using Core.Block.BlockInventory;
using Game.World.Interface;

namespace World.DataStore
{
    public class WorldBlockInventoryDatastore : IWorldBlockInventoryDatastore
    {
        private IWorldBlockDatastore _worldBlockDatastore;

        public WorldBlockInventoryDatastore(IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;
        }

        public bool ExistsBlockInventory(int x, int y)
        {
            return _worldBlockDatastore.GetBlock(x, y) is IBlockInventory;
        }

        public IBlockInventory GetBlock(int x, int y)
        {
            var block = _worldBlockDatastore.GetBlock(x, y);
            if (block is IBlockInventory inventory)
            {
                return inventory;
            }
            else
            {
                return new NullIBlockInventory();
            }
        }
    }
}