using Game.Entity.Interface;

namespace Game.Entity.EntityInstance
{
    public class ItemEntity : IEntity
    {
        public ItemEntity(long instanceId, string entityType, ServerVector3 position)
        {
            InstanceId = instanceId;
            EntityType = entityType;
            Position = position;
        }

        public long InstanceId { get; }
        public string EntityType { get; }
        public ServerVector3 Position { get; private set; }
        public void SetPosition(ServerVector3 serverVector3)
        {
            Position = serverVector3;
        }
    }
}