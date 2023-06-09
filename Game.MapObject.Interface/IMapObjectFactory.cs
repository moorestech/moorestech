using Game.Base;
using Game.MapObject.Interface.Json;

namespace Game.MapObject.Interface
{
    public interface IMapObjectFactory
    {
        public IMapObject Create(int instanceId,string type, ServerVector3 position, bool isDestroyed);
    }
}