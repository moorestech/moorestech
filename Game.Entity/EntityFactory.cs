using System.Collections.Generic;
using Game.Entity.EntityInstance;
using Game.Entity.Interface;

namespace Game.Entity
{
    public class EntityFactory : IEntityFactory
    {
        public IEntity CreateEntity(string entityType, long instanceId,ServerVector3 position = default)
        {
            if (entityType == EntityType.VanillaPlayer)
            {
                return new PlayerEntity(instanceId, entityType, position);
            }

            if (entityType == EntityType.VanillaItem)
            {
                return new ItemEntity(instanceId,entityType, position);
            }

            throw new KeyNotFoundException("Entity type not found : " + entityType);
        }
    }
}