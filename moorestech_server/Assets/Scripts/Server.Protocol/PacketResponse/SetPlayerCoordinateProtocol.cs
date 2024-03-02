using System;
using System.Collections.Generic;
using Game.Block.Interface.BlockConfig;
using Game.Entity.Interface;
using Game.World.Interface.DataStore;
using Game.WorldMap;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Player;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     プレイヤー座標のプロトコル
    /// </summary>
    public class SetPlayerCoordinateProtocol : IPacketResponse
    {
        public const string Tag = "va:playerCoordinate";
        
        private readonly IEntitiesDatastore _entitiesDatastore;

        public SetPlayerCoordinateProtocol(ServiceProvider serviceProvider)
        {
            _entitiesDatastore = serviceProvider.GetService<IEntitiesDatastore>();
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<PlayerCoordinateSendProtocolMessagePack>(payload.ToArray());

            //プレイヤーの座標を更新する
            var newPosition = new Vector3(data.X, 0, data.Y);
            _entitiesDatastore.SetPosition(data.PlayerId, newPosition);

            return null;
        }
    }


    [MessagePackObject]
    public class PlayerCoordinateSendProtocolMessagePack : ProtocolMessagePackBase
    {
        [Key(2)]
        public int PlayerId { get; set; }
        [Key(3)]
        public float X { get; set; }
        [Key(4)]
        public float Y { get; set; }
        
        public PlayerCoordinateSendProtocolMessagePack(int playerId, float x, float y)
        {
            Tag = SetPlayerCoordinateProtocol.Tag;
            PlayerId = playerId;
            X = x;
            Y = y;
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public PlayerCoordinateSendProtocolMessagePack() { }
    }
}