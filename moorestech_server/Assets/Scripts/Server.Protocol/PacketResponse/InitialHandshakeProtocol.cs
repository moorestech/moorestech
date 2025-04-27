using System;
using System.Collections.Generic;
using Game.Entity.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class InitialHandshakeProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:initialHandshake";
        
        private static readonly Vector3 DefaultPlayerPosition = new(186, 15.7f, -37.401f);
        
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
            
            var response = new ResponseInitialHandshakeMessagePack(GetPlayerPosition(new EntityInstanceId(data.PlayerId)));
            
            return response;
        }
        
        
        private Vector3MessagePack GetPlayerPosition(EntityInstanceId playerId)
        {
            if (_entitiesDatastore.Exists(playerId))
            {
                //プレイヤーがいるのでセーブされた座標を返す
                var pos = _entitiesDatastore.GetPosition(playerId);
                return new Vector3MessagePack(pos.x, pos.y, pos.z);
            }
            
            var playerEntity = _entityFactory.CreateEntity(VanillaEntityType.VanillaPlayer, playerId, DefaultPlayerPosition);
            _entitiesDatastore.Add(playerEntity);
            
            
            //プレイヤーのデータがなかったのでスポーン地点を取得する
            return new Vector3MessagePack(_worldSettingsDatastore.WorldSpawnPoint);
        }
        
        [MessagePackObject]
        public class RequestInitialHandshakeMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public string PlayerName { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestInitialHandshakeMessagePack() { }
            
            public RequestInitialHandshakeMessagePack(int playerId, string playerName)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                PlayerName = playerName;
            }
        }
        
        [MessagePackObject]
        public class ResponseInitialHandshakeMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3MessagePack PlayerPos { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseInitialHandshakeMessagePack() { }
            
            public ResponseInitialHandshakeMessagePack(Vector3MessagePack playerPos)
            {
                Tag = ProtocolTag;
                PlayerPos = playerPos;
            }
        }
    }
}