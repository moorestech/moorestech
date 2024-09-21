using System;
using Game.Map.Interface.MapObject;
using UnityEngine;

namespace Game.Map
{
    public class MapObjectFactory : IMapObjectFactory
    {
        public IMapObject Create(int instanceId, Guid mapObjectGuid, int currentHp, bool isDestroyed, Vector3 position)
        {
            return new VanillaStaticMapObject(instanceId, mapObjectGuid, isDestroyed, currentHp, position);
        }
    }
}