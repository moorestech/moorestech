using System.Collections.Generic;
using Core.Block;

namespace Game.World.Interface.DataStore
{
    public interface IWorldBlockDatastore
    {
        public bool AddBlock(IBlock block, int x, int y,BlockDirection blockDirection);
        public bool RemoveBlock(int x, int y);
        public IBlock GetBlock(int x, int y);
        public BlockDirection GetBlockDirection(int x, int y);
        public List<SaveBlockData> GetSaveBlockDataList();
        public void LoadBlockDataList(List<SaveBlockData> saveBlockDataList);
    }
}