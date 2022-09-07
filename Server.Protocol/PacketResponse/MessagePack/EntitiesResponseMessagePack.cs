using System;
using System.Collections.Generic;
using System.Linq;
using Game.Entity.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse.MessagePack
{
    [MessagePackObject(keyAsPropertyName :true)]
    public class EntitiesResponseMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public EntitiesResponseMessagePack() { }

        public EntitiesResponseMessagePack(List<IEntity> entities)
        {
            Tag = PlayerCoordinateSendProtocol.EntityDataTag;
            Entities = entities.Select(e => new EntityMessagePack(e)).ToArray();
        }
        
        public EntityMessagePack[] Entities { get; set; }

    }
}