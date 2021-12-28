using Core.Electric;

namespace Game.World.Interface.DataStore
{
    public interface IWorldBlockElectricDatastore
    {
        public bool ExistsBlockElectric(int x, int y);
        public IBlockElectric GetBlock(int x, int y);
    }
}