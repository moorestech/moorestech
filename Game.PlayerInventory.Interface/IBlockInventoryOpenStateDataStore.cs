using Game.World.Interface.DataStore;

namespace Game.PlayerInventory.Interface
{
    public interface IBlockInventoryOpenStateDataStore
    {
        public bool IsOpen(int playerId);
        public Coordinate GetOpenCoordinates(int playerId);
    }
}