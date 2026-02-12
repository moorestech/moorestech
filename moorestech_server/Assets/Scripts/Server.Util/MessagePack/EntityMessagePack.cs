using System;
using Game.Entity.Interface;
using MessagePack;

namespace Server.Util.MessagePack
{
    [MessagePackObject]
    public class EntityMessagePack
    {
        [Obsolete("This constructor is for deserialization. Do not use directly.")]
        public EntityMessagePack()
        {
        }
        
        public EntityMessagePack(IEntity entity)
        {
            InstanceId = entity.InstanceId.AsPrimitive();
            Type = entity.EntityType;
            EntityData = entity.GetEntityData();
            Position = new Vector3MessagePack(entity.Position);
        }
        
        [Key(0)] public long InstanceId { get; set; }
        
        [Key(1)] public string Type { get; set; }
        
        [Key(2)] public Vector3MessagePack Position { get; set; }
        
        [Key(3)] public byte[] EntityData { get; set; }
    }
}
