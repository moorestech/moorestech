using Game.Base;
using Game.MapObject.Interface;

namespace Game.MapObject
{
    public class VanillaStaticMapObject : IMapObject
    {
        public int Id { get; }
        public string Type { get; }
        public bool IsDestroyed { get; private set; }
        public ServerVector3 Position { get; }
        public int ItemId { get; }
        
        public VanillaStaticMapObject(int id, string type, bool isDestroyed, ServerVector3 position, int itemId)
        {
            Id = id;
            Type = type;
            IsDestroyed = isDestroyed;
            Position = position;
            ItemId = itemId;
        }

        public void Destroy()
        {
            IsDestroyed = true;
        }
    }
}