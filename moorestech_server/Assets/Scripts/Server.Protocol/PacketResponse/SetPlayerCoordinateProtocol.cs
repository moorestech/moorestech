using System;
using System.Collections.Generic;
using Game.Entity.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     プレイヤー座標のプロトコル
    /// </summary>
    public class SetPlayerCoordinateProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:playerCoordinate";
        
        private readonly IEntitiesDatastore _entitiesDatastore;
        
        public SetPlayerCoordinateProtocol(ServiceProvider serviceProvider)
        {
            _entitiesDatastore = serviceProvider.GetService<IEntitiesDatastore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<PlayerCoordinateSendProtocolMessagePack>(payload.ToArray());
            
            //プレイヤーの座標を更新する
            var newPosition = new Vector3(data.Pos.X, 0, data.Pos.Y);
            _entitiesDatastore.SetPosition(new EntityInstanceId(data.PlayerId), newPosition);
            
            return null;
        }
        
        
        [MessagePackObject]
        public class PlayerCoordinateSendProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public Vector2MessagePack Pos { get; set; }
            
            public PlayerCoordinateSendProtocolMessagePack(int playerId, Vector2 pos)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                Pos = new Vector2MessagePack(pos);
            }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public PlayerCoordinateSendProtocolMessagePack() { }
        }
    }
}