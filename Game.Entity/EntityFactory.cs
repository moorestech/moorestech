using System.Collections.Generic;
using Game.Entity.EntityInstance;
using Game.Entity.Interface;

namespace Game.Entity
{
    public class EntityFactory : IEntityFactory
    {
        public IEntity CreateEntity(string entityType, long instanceId)
        {
            if (entityType == EntityType.VanillaPlayer)
            {
                return new PlayerEntity(instanceId, 0, entityType, default);
            }

            throw new KeyNotFoundException("Entity type not found : " + entityType);
        }
    }
}