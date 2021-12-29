namespace Game.World.Interface.DataStore
{
    public interface IWorldBlockComponentDatastore<T>
    {
        public bool ExistsBlockElectric(int x, int y);
        public T GetBlock(int x, int y);
    }
}