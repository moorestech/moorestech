namespace Game.Entity.Interface.EntityInstance
{
    public class PlayerEntity : IEntity
    {
        public PlayerEntity(long instanceId, ServerVector3 position)
        {
            InstanceId = instanceId;
            Position = position;
        }

        public long InstanceId { get; }
        public string EntityType => VanillaEntityType.VanillaPlayer;
        public ServerVector3 Position { get; private set; }
        public string State => string.Empty;

        public void SetPosition(ServerVector3 serverVector3)
        {
            Position = serverVector3;
        }
    }
}