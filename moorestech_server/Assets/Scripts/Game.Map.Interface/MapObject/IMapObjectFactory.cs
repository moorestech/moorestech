using UnityEngine;

namespace Game.Map.Interface.MapObject
{
    public interface IMapObjectFactory
    {
        public IMapObject Create(int instanceId, string type, int currentHp, bool isDestroyed, Vector3 position);
    }
}