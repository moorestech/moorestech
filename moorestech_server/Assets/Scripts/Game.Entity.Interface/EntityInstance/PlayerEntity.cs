using UnityEngine;

namespace Game.Entity.Interface.EntityInstance
{
    public class PlayerEntity : IEntity
    {
        public PlayerEntity(long instanceId, Vector3 position)
        {
            InstanceId = instanceId;
            Position = position;
        }
        
        public Vector3 Position { get; private set; }
        
        public long InstanceId { get; }
        public string EntityType => VanillaEntityType.VanillaPlayer;
        public string State => string.Empty;
        
        public void SetPosition(Vector3 serverVector3)
        {
            Position = serverVector3;
        }
    }
}