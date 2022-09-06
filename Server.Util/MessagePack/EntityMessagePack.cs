using System;
using Game.Entity.Interface;
using MessagePack;

namespace Server.Util.MessagePack
{
    [MessagePackObject(false)]
    public class EntityMessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public EntityMessagePack() { }

        public EntityMessagePack(IEntity entity)
        {
            InstanceId = entity.InstanceId;
            Type = entity.EntityType;
            Position = new Vector3MessagePack(entity.Position);
        }
        public long InstanceId { get; set; }
        public string Type { get; set; }
        public Vector3MessagePack Position { get; set; }
    }
}