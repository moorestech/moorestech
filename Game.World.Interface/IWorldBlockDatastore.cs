using System.Collections.Generic;
using Core.Block;
using Core.Block.BlockFactory;
using Core.Block.BlockInventory;
using World;

namespace Game.World.Interface
{
    
    /// <summary>
    /// TODO IBlockInventoryの管理を他のクラスがするようにする
    /// </summary>
    public interface IWorldBlockDatastore
    {
        public bool AddBlock(IBlock block, int x, int y,BlockDirection blockDirection);
        public IBlock GetBlock(int x, int y);
        public List<SaveBlockData> GetSaveBlockDataList();
        public void LoadBlockDataList(List<SaveBlockData> saveBlockDataList);
    }
}