using System;
using UnityEngine;

namespace Game.Entity.Interface.EntityInstance
{
    public class PlayerEntity : IEntity
    {
        public Vector3 Position { get; private set; }
        
        public EntityInstanceId InstanceId { get; }
        public string EntityType => VanillaEntityType.VanillaPlayer;
        
        public PlayerEntity(EntityInstanceId instanceId, Vector3 position)
        {
            InstanceId = instanceId;
            Position = position;
        }
        
        public void SetPosition(Vector3 serverVector3)
        {
            Position = serverVector3;
        }
        public byte[] GetEntityData()
        {
            return Array.Empty<byte>();
        }
    }
}
