using System;
using Game.Context;
using Game.Map.Interface;
using Game.Map.Interface.MapObject;
using UnityEngine;
using Random = System.Random;

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