using System.Collections.Generic;
using Game.Entity.Interface;
using Game.Entity.Interface.EntityInstance;
using UnityEngine;

namespace Game.Entity
{
    public class EntityFactory : IEntityFactory
    {
        public IEntity CreateEntity(string entityType, long instanceId, Vector3 position = default)
        {
            if (entityType == VanillaEntityType.VanillaPlayer) return new PlayerEntity(instanceId, position);

            if (entityType == VanillaEntityType.VanillaItem) return new ItemEntity(instanceId, position);

            throw new KeyNotFoundException("Entity type not found : " + entityType);
        }
    }
}