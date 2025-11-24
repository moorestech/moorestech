using System.Collections.Generic;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class DisconnectChainProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:disconnectChain";
        private readonly IChainSystem _chainSystem;
        private readonly ChainConnectionEventPacket _chainEventPacket;

        public DisconnectChainProtocol(ServiceProvider serviceProvider)
        {
            _chainSystem = serviceProvider.GetService<IChainSystem>();
            _chainEventPacket = serviceProvider.GetService<ChainConnectionEventPacket>();
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<DisconnectChainRequestMessagePack>(payload.ToArray());
            var success = _chainSystem.TryDisconnect(request.PosAVector, request.PosBVector, out var error);
            if (success) _chainEventPacket.PublishDisconnection(request.PosAVector, request.PosBVector);
            return new DisconnectChainResponseMessagePack(success, error);
        }

        [MessagePackObject]
        public class DisconnectChainRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack PosA { get; set; }
            [Key(3)] public Vector3IntMessagePack PosB { get; set; }

            [IgnoreMember] public Vector3Int PosAVector => PosA;
            [IgnoreMember] public Vector3Int PosBVector => PosB;

            [System.Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public DisconnectChainRequestMessagePack() { }

            public DisconnectChainRequestMessagePack(Vector3Int posA, Vector3Int posB)
            {
                Tag = ProtocolTag;
                PosA = new Vector3IntMessagePack(posA);
                PosB = new Vector3IntMessagePack(posB);
            }
        }

        [MessagePackObject]
        public class DisconnectChainResponseMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public bool IsSuccess { get; set; }
            [Key(3)] public string Error { get; set; }

            [IgnoreMember] public bool HasError => !string.IsNullOrEmpty(Error);

            [System.Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public DisconnectChainResponseMessagePack() { }

            public DisconnectChainResponseMessagePack(bool isSuccess, string error)
            {
                IsSuccess = isSuccess;
                Error = error ?? string.Empty;
            }
        }
    }
}
