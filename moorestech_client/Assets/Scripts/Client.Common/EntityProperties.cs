using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Common
{
    public class EntityProperties
    {
        public readonly long InstanceId;
        public readonly Vector3 Position;
        public readonly byte[] State;
        public readonly string Type;
        
        public EntityProperties(EntityMessagePack entityMessagePack)
        {
            InstanceId = entityMessagePack.InstanceId;
            Type = entityMessagePack.Type;
            var x = entityMessagePack.Position.X;
            var y = entityMessagePack.Position.Y;
            var z = entityMessagePack.Position.Z;
            Position = new Vector3(x, y, z);
            State = entityMessagePack.State;
        }
    }
}
