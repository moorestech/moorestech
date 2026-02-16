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
        
        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<PlayerCoordinateSendProtocolMessagePack>(payload);
            
            //プレイヤーの座標を更新する
            _entitiesDatastore.SetPosition(new EntityInstanceId(data.PlayerId), data.Pos.Vector3);
            
            return null;
        }
        
        
        [MessagePackObject]
        public class PlayerCoordinateSendProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public Vector3MessagePack Pos { get; set; }
            
            public PlayerCoordinateSendProtocolMessagePack(int playerId, Vector3 pos)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                Pos = new Vector3MessagePack(pos);
            }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public PlayerCoordinateSendProtocolMessagePack() { }
        }
    }
}