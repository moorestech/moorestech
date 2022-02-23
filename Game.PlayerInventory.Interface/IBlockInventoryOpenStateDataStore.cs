using Game.World.Interface.DataStore;

namespace Game.PlayerInventory.Interface
{
    public interface IBlockInventoryOpenStateDataStore
    {
        public bool IsOpen(int playerId);
        public int GetOpenCoordinates(int playerId);
        public void Open(int playerId, int x ,int y);
        public void Close(int playerId);
    }
}