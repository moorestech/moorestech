using UnityEngine;

namespace Game.Entity.Interface
{
    public interface IEntityFactory
    {
        public IEntity CreateEntity(string entityType, EntityInstanceId instanceId, Vector3 serverPosition = default);
        public IEntity LoadEntity(string entityType, EntityInstanceId instanceId, Vector3 serverPosition = default);
    }
}