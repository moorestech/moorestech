using Core.Block;

namespace Game.World.Interface
{
    
    /// <summary>
    /// TODO IBlockInventoryの管理を他のクラスがするようにする
    /// </summary>
    public interface IWorldBlockDatastore
    {
        public bool AddBlock(IBlock block, int x, int y, IBlockInventory blockInventory);
        public IBlock GetBlock(int intId);
        public IBlockInventory GetBlockInventory(int intId);
        public IBlockInventory GetBlockInventory(int x, int y);
        public IBlock GetBlock(int x, int y);
        public int GetIntId(int x, int y);
    }
}