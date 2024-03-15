using UnityEngine;

namespace Game.Map.Interface
{
    public interface IMapObjectFactory
    {
        public IMapObject Create(int instanceId, string type, Vector3 position, bool isDestroyed);
    }
}