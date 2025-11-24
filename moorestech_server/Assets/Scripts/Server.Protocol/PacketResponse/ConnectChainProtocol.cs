using System.Collections.Generic;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class ConnectChainProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:connectChain";
        private readonly IChainSystem _chainSystem;
        private readonly ChainConnectionEventPacket _chainEventPacket;

        public ConnectChainProtocol(ServiceProvider serviceProvider)
        {
            _chainSystem = serviceProvider.GetService<IChainSystem>();
            _chainEventPacket = serviceProvider.GetService<ChainConnectionEventPacket>();
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<ConnectChainRequestMessagePack>(payload.ToArray());
            var success = _chainSystem.TryConnect(request.PosAVector, request.PosBVector, request.PlayerId, out var error);
            if (success) _chainEventPacket.PublishConnection(request.PosAVector, request.PosBVector);
            return new ConnectChainResponseMessagePack(success, error);
        }

        [MessagePackObject]
        public class ConnectChainRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack PosA { get; set; }
            [Key(3)] public Vector3IntMessagePack PosB { get; set; }
            [Key(4)] public int PlayerId { get; set; }

            [IgnoreMember] public Vector3Int PosAVector => PosA;
            [IgnoreMember] public Vector3Int PosBVector => PosB;

            [System.Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ConnectChainRequestMessagePack() { }

            public ConnectChainRequestMessagePack(Vector3Int posA, Vector3Int posB, int playerId)
            {
                Tag = ProtocolTag;
                PosA = new Vector3IntMessagePack(posA);
                PosB = new Vector3IntMessagePack(posB);
                PlayerId = playerId;
            }
        }

        [MessagePackObject]
        public class ConnectChainResponseMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public bool IsSuccess { get; set; }
            [Key(3)] public string Error { get; set; }

            [IgnoreMember] public bool HasError => !string.IsNullOrEmpty(Error);

            [System.Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ConnectChainResponseMessagePack() { }

            public ConnectChainResponseMessagePack(bool isSuccess, string error)
            {
                IsSuccess = isSuccess;
                Error = error ?? string.Empty;
            }
        }
    }
}
