namespace Game.MapObject.Interface
{
    public interface IMapObjectDatastore
    {
        public void Add(IMapObject mapObject);
        public void Destroy(int id);
    }
}