using System;
using System.Collections.Generic;
using System.Linq;
using Game.Entity.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class InitialHandshakeProtocol : IPacketResponse
    {
        public const string Tag = "va:initialHandshake";

        private readonly IEntitiesDatastore _entitiesDatastore;
        private readonly IEntityFactory _entityFactory;
        private readonly IWorldSettingsDatastore _worldSettingsDatastore;

        public InitialHandshakeProtocol(ServiceProvider serviceProvider)
        {
            _entitiesDatastore = serviceProvider.GetService<IEntitiesDatastore>();
            _entityFactory = serviceProvider.GetService<IEntityFactory>();
            _worldSettingsDatastore = serviceProvider.GetService<IWorldSettingsDatastore>();
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestInitialHandshakeMessagePack>(payload.ToArray());


            var response = new ResponseInitialHandshakeMessagePack(GetPlayerPosition(data.PlayerId));

            return response;
        }


        private Vector2MessagePack GetPlayerPosition(int playerId)
        {
            if (_entitiesDatastore.Exists(playerId))
            {
                //プレイヤーがいるのでセーブされた座標を返す
                var pos = _entitiesDatastore.GetPosition(playerId);
                return new Vector2MessagePack(pos.x, pos.z);
            }

            var playerEntity = _entityFactory.CreateEntity(VanillaEntityType.VanillaPlayer, playerId);
            _entitiesDatastore.Add(playerEntity);


            //プレイヤーのデータがなかったのでスポーン地点を取得する
            var x = _worldSettingsDatastore.WorldSpawnPoint.x;
            var y = _worldSettingsDatastore.WorldSpawnPoint.y;
            return new Vector2MessagePack(x, y);
        }
    }


    [MessagePackObject]
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

        [Key(2)]
        public int PlayerId { get; set; }
        [Key(3)]
        public string PlayerName { get; set; }
    }

    [MessagePackObject]
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

        [Key(2)]
        public Vector2MessagePack PlayerPos { get; set; }
    }
}