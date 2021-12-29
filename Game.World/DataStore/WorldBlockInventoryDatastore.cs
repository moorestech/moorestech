using Core.Block.BlockInventory;
using Game.World.Interface;
using Game.World.Interface.DataStore;

namespace World.DataStore
{
    public class WorldBlockInventoryDatastore : IWorldBlockComponentDatastore<IBlockInventory>
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        public WorldBlockInventoryDatastore(IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;
        }
        public bool ExistsComponentBlock(int x, int y)
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