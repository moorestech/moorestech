using System;
using Core.Master;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using Server.Util.MessagePack;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Server.Protocol.PacketResponse
{
    public class ElectricWireDisconnectProtocol : IPacketResponse
    {
        public const string Tag = "va:electricWireDisconnect";

        public ElectricWireDisconnectProtocol(ServiceProvider serviceProvider)
        {
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            // 要求データをデシリアライズし切断を実行する
            // Deserialize the request and run the disconnect
            var request = MessagePackSerializer.Deserialize<ElectricWireDisconnectRequest>(payload);
            var success = ElectricWireSystemUtil.TryDisconnect(request.PosAVector, request.PosBVector, request.PlayerId, out var failureReason);
            return new ElectricWireDisconnectResponse(success, failureReason);
        }

        [MessagePackObject]
        public class ElectricWireDisconnectRequest : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack PosA { get; set; }
            [Key(3)] public Vector3IntMessagePack PosB { get; set; }
            [Key(4)] public int PlayerId { get; set; }

            [IgnoreMember] public Vector3Int PosAVector => PosA;
            [IgnoreMember] public Vector3Int PosBVector => PosB;

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ElectricWireDisconnectRequest() { Tag = ElectricWireDisconnectProtocol.Tag; }

            public static ElectricWireDisconnectRequest CreateDisconnectRequest(Vector3Int posA, Vector3Int posB, int playerId)
            {
                return new ElectricWireDisconnectRequest
                {
                    Tag = ElectricWireDisconnectProtocol.Tag,
                    PosA = new Vector3IntMessagePack(posA),
                    PosB = new Vector3IntMessagePack(posB),
                    PlayerId = playerId,
                };
            }
        }

        [MessagePackObject]
        public class ElectricWireDisconnectResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool IsSuccess { get; set; }
            [Key(3)] public ElectricWirePlacementFailureReason FailureReason { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ElectricWireDisconnectResponse() { }

            public ElectricWireDisconnectResponse(bool isSuccess, ElectricWirePlacementFailureReason failureReason)
            {
                IsSuccess = isSuccess;
                FailureReason = failureReason;
            }
        }
    }
}
