using System;
using System.Collections.Generic;
using System.Linq;
using Game.Entity.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class InitialHandshakeProtocol : IPacketResponse
    {
        public const string Tag = "va:invItemMove";

        private readonly IEntitiesDatastore _entitiesDatastore;
        private readonly IEntityFactory _entityFactory;

        public InitialHandshakeProtocol(ServiceProvider serviceProvider)
        {
            _entitiesDatastore = serviceProvider.GetService<IEntitiesDatastore>();
            _entityFactory = serviceProvider.GetService<IEntityFactory>();
        }

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestInitialHandshakeMessagePack>(payload.ToArray());


            var response = new ResponseInitialHandshakeMessagePack(GetPlayerPosition(data.PlayerId));
            
            return new List<List<byte>>(){MessagePackSerializer.Serialize(response).ToList()};
        }




        private Vector2MessagePack GetPlayerPosition(int playerId)
        {
            if (_entitiesDatastore.Exists(playerId))
            {
                var pos = _entitiesDatastore.GetPosition(playerId);
                return new Vector2MessagePack(pos.X, pos.Z);
            }

            var playerEntity = _entityFactory.CreateEntity(EntityType.VanillaPlayer, playerId);
            _entitiesDatastore.Add(playerEntity);

            
            return new Vector2MessagePack(0, 0);
        }
    }
    
    
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class RequestInitialHandshakeMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RequestInitialHandshakeMessagePack()
        {
        }

        public RequestInitialHandshakeMessagePack(int playerId, string playerName)
        {
            Tag = InitialHandshakeProtocol.Tag;
            PlayerId = playerId;
            PlayerName = playerName;
        }

        public int PlayerId { get; set; }
        public string PlayerName { get; set; }
    }
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class ResponseInitialHandshakeMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ResponseInitialHandshakeMessagePack()
        {
        }

        public ResponseInitialHandshakeMessagePack(Vector2MessagePack playerPos)
        {
            Tag = InitialHandshakeProtocol.Tag;
            PlayerPos = playerPos;
        }

        public Vector2MessagePack PlayerPos { get; set; }
    }
}