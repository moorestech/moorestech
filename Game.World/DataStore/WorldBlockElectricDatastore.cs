using System;
using Core.Block.BlockInventory;
using Core.Electric;
using Game.World.Interface.DataStore;

namespace World.DataStore
{
    public class WorldBlockElectricDatastore : IWorldBlockElectricDatastore
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        
        public WorldBlockElectricDatastore(IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;
        }
        public bool ExistsBlockElectric(int x, int y)
        {
            return _worldBlockDatastore.GetBlock(x, y) is IBlockElectric;
        }

        public IBlockElectric GetBlock(int x, int y)
        {
            var block = _worldBlockDatastore.GetBlock(x, y);
            if (block is IBlockElectric electric)
            {
                return electric;
            }
            throw new Exception("Block is not electric");
        }
    }
}