using System;
using Game.Entity.Interface;
using MessagePack;

namespace Game.Common.MessagePack
{
    [MessagePackObject]
    public class EntityMessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
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
