using UnityEngine;

namespace Game.Entity.Interface.EntityInstance
{
    public class PlayerEntity : IEntity
    {
        public PlayerEntity(EntityInstanceId instanceId, Vector3 position)
        {
            InstanceId = instanceId;
            Position = position;
        }
        
        public Vector3 Position { get; private set; }
        
        public EntityInstanceId InstanceId { get; }
        public string EntityType => VanillaEntityType.VanillaPlayer;
        
        public void SetPosition(Vector3 serverVector3)
        {
            Position = serverVector3;
        }
    }
}