using System;
using System.Collections.Generic;
using System.Linq;
using Game.Entity.Interface;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse.MessagePack
{
    [MessagePackObject(true)]
    public class EntitiesResponseMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public EntitiesResponseMessagePack()
        {
        }

        public EntitiesResponseMessagePack(List<IEntity> entities)
        {
            Tag = PlayerCoordinateSendProtocol.EntityDataTag;
            Entities = entities.Select(e => new EntityMessagePack(e)).ToArray();
        }

        public EntityMessagePack[] Entities { get; set; }
    }
}