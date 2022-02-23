using System.Collections.Generic;

namespace Game.PlayerInventory.Interface
{
    public interface IBlockInventoryOpenStateDataStore
    {
        public List<int> GetBlockInventoryOpenPlayers(int blockEntityId);
        public void Open(int playerId, int x ,int y);
        public void Close(int playerId);
    }
}