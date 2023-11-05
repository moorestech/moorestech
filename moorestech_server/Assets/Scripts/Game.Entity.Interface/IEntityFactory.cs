using UnityEngine;

namespace Game.Entity.Interface
{
    public interface IEntityFactory
    {
        public IEntity CreateEntity(string entityType, long instanceId, Vector3 serverPosition = default);
    }
}