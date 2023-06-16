using System;
using System.Collections.Generic;
using System.Linq;
using Game.Entity.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.Base;
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

        public List<ToClientProtocolMessagePackBase> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestInitialHandshakeMessagePack>(payload.ToArray());


            var response = new ResponseInitialHandshakeMessagePack(GetPlayerPosition(data.PlayerId));
            
            return new List<ToClientProtocolMessagePackBase>() { response };
        }




        private Vector2MessagePack GetPlayerPosition(int playerId)
        {
            if (_entitiesDatastore.Exists(playerId))
            {
                //プレイヤーがいるのでセーブされた座標を返す
                var pos = _entitiesDatastore.GetPosition(playerId);
                return new Vector2MessagePack(pos.X, pos.Z);
            }
            var playerEntity = _entityFactory.CreateEntity(VanillaEntityType.VanillaPlayer, playerId);
            _entitiesDatastore.Add(playerEntity);

            
            //プレイヤーのデータがなかったのでスポーン地点を取得する
            var x = _worldSettingsDatastore.WorldSpawnPoint.X;
            var y = _worldSettingsDatastore.WorldSpawnPoint.Y;
            return new Vector2MessagePack(x, y);
        }
    }
    
    
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class RequestInitialHandshakeMessagePack : ToServerProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RequestInitialHandshakeMessagePack()
        {
        }

        public RequestInitialHandshakeMessagePack(int playerId, string playerName)
        {
            ToServerTag = InitialHandshakeProtocol.Tag;
            PlayerId = playerId;
            PlayerName = playerName;
        }

        public int PlayerId { get; set; }
        public string PlayerName { get; set; }
    }
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class ResponseInitialHandshakeMessagePack : ToClientProtocolMessagePackBase 
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ResponseInitialHandshakeMessagePack()
        {
        }

        public ResponseInitialHandshakeMessagePack(Vector2MessagePack playerPos)
        {
            ToClientTag = InitialHandshakeProtocol.Tag;
            PlayerPos = playerPos;
        }

        public Vector2MessagePack PlayerPos { get; set; }
    }
}