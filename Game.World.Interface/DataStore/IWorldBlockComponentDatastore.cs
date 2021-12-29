namespace Game.World.Interface.DataStore
{
    public interface IWorldBlockComponentDatastore<T>
    {
        public bool ExistsComponentBlock(int x, int y);
        public T GetBlock(int x, int y);
    }
}