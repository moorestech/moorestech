using Game.Base;

namespace Game.Entity.Interface
{
    public interface IEntityFactory
    {
        public IEntity CreateEntity(string entityType, long instanceId,ServerVector3 serverPosition = default);
    }
}