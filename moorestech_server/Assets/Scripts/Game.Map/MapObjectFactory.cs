using Game.Map.Interface.MapObject;
using UnityEngine;

namespace Game.Map
{
    public class MapObjectFactory : IMapObjectFactory
    {
        public IMapObject Create(int instanceId, string type, int currentHp, bool isDestroyed, Vector3 position)
        {
            return new VanillaStaticMapObject(instanceId, type, isDestroyed, currentHp, position);
        }
    }
}