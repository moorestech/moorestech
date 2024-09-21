using System;
using UnityEngine;

namespace Game.Map.Interface.MapObject
{
    public interface IMapObjectFactory
    {
        public IMapObject Create(int instanceId, Guid mapObjectGuid, int currentHp, bool isDestroyed, Vector3 position);
    }
}