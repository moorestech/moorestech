using System;
using Core.Block.BlockInventory;
using Core.Electric;
using Game.World.Interface.DataStore;

namespace World.DataStore
{
    //TODO ブロック設置を検知　そのブロックが電気系のブロックかどうか判定　電気系なら周りに電気系のブロックがないか探す　あったらそのブロックが所属している電力セグメントにそのブロックを追加
    public class WorldBlockElectricDatastore : IWorldBlockComponentDatastore<IBlockElectric>
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        
        public WorldBlockElectricDatastore(IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;
        }
        public bool ExistsComponentBlock(int x, int y)
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