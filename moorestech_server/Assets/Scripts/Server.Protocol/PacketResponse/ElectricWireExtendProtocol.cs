using System;
using Core.Master;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using Server.Util.MessagePack;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Server.Protocol.PacketResponse
{
    public class ElectricWireExtendProtocol : IPacketResponse
    {
        public const string Tag = "va:electricWireExtend";

        public ElectricWireExtendProtocol(ServiceProvider serviceProvider)
        {
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            // 要求データをデシリアライズする
            // Deserialize request payload
            var request = MessagePackSerializer.Deserialize<ElectricWireExtendRequest>(payload);

            // 検証と設置・接続・消費をサービスに委ね、結果を応答へ変換する
            // Delegate validation, placement, wiring and consumption to the service; map its result to a response
            var result = ElectricWireExtendService.Execute(
                request.Operation, request.FromPosVector, request.ToPosVector, request.PolePlaceInfo,
                request.PlayerId, request.PoleBlockId, request.ConnectToolGuid);

            return result.IsSuccess
                ? ElectricWireExtendResponse.CreateSuccess(result.EndpointPos, result.EndpointBlockInstanceId)
                : ElectricWireExtendResponse.CreateFailure(result.FailureReason);
        }

        [MessagePackObject]
        public class ElectricWireExtendRequest : ProtocolMessagePackBase
        {
            [Key(2)] public ElectricWireExtendOperation Operation { get; set; }
            [Key(3)] public Vector3IntMessagePack FromPos { get; set; }
            [Key(4)] public Vector3IntMessagePack ToPos { get; set; }
            [Key(5)] public PlaceInfoMessagePack PolePlaceInfo { get; set; }
            [Key(6)] public int PlayerId { get; set; }
            [Key(7)] public int PoleBlockIdInt { get; set; }
            [Key(8)] public Guid ConnectToolGuid { get; set; }

            [IgnoreMember] public Vector3Int FromPosVector => FromPos;
            [IgnoreMember] public Vector3Int ToPosVector => ToPos;
            [IgnoreMember] public BlockId PoleBlockId => new(PoleBlockIdInt);

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ElectricWireExtendRequest() { Tag = ElectricWireExtendProtocol.Tag; }

            // Operationごとに必要フィールドが異なるため、生成はstatic factory経由に限定する
            // Creation is restricted to static factories since required fields differ per operation
            private ElectricWireExtendRequest(ElectricWireExtendOperation operation, Vector3Int fromPos, Vector3Int toPos, PlaceInfoMessagePack polePlaceInfo, int playerId, int poleBlockIdInt, Guid connectToolGuid)
            {
                Tag = ElectricWireExtendProtocol.Tag;
                Operation = operation;
                FromPos = new Vector3IntMessagePack(fromPos);
                ToPos = new Vector3IntMessagePack(toPos);
                PolePlaceInfo = polePlaceInfo;
                PlayerId = playerId;
                PoleBlockIdInt = poleBlockIdInt;
                ConnectToolGuid = connectToolGuid;
            }

            public static ElectricWireExtendRequest CreateConnectRequest(int playerId, Vector3Int fromPos, Vector3Int toPos, Guid connectToolGuid)
                => new(ElectricWireExtendOperation.ConnectToExisting, fromPos, toPos, new PlaceInfoMessagePack(new PlaceInfo()), playerId, 0, connectToolGuid);

            public static ElectricWireExtendRequest CreateExtendRequest(int playerId, Vector3Int fromPos, BlockId poleBlockId, PlaceInfo polePlaceInfo, Guid connectToolGuid)
                => new(ElectricWireExtendOperation.ExtendToNewPole, fromPos, Vector3Int.zero, new PlaceInfoMessagePack(polePlaceInfo), playerId, poleBlockId.AsPrimitive(), connectToolGuid);

            public static ElectricWireExtendRequest CreateIsolatedPlaceRequest(int playerId, BlockId poleBlockId, PlaceInfo polePlaceInfo)
                => new(ElectricWireExtendOperation.PlaceIsolatedPole, Vector3Int.zero, Vector3Int.zero, new PlaceInfoMessagePack(polePlaceInfo), playerId, poleBlockId.AsPrimitive(), Guid.Empty);
        }

        [MessagePackObject]
        public class ElectricWireExtendResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool IsSuccess { get; set; }
            [Key(3)] public ElectricWirePlacementFailureReason FailureReason { get; set; }
            [Key(4)] public Vector3IntMessagePack EndpointPos { get; set; }
            [Key(5)] public int EndpointBlockInstanceId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ElectricWireExtendResponse() { Tag = ElectricWireExtendProtocol.Tag; }

            public static ElectricWireExtendResponse CreateSuccess(Vector3Int endpointPos, int endpointBlockInstanceId)
            {
                return new ElectricWireExtendResponse
                {
                    Tag = ElectricWireExtendProtocol.Tag,
                    IsSuccess = true,
                    FailureReason = ElectricWirePlacementFailureReason.None,
                    EndpointPos = new Vector3IntMessagePack(endpointPos),
                    EndpointBlockInstanceId = endpointBlockInstanceId,
                };
            }

            public static ElectricWireExtendResponse CreateFailure(ElectricWirePlacementFailureReason failureReason)
            {
                return new ElectricWireExtendResponse
                {
                    Tag = ElectricWireExtendProtocol.Tag,
                    IsSuccess = false,
                    FailureReason = failureReason,
                    EndpointPos = new Vector3IntMessagePack(Vector3Int.zero),
                    EndpointBlockInstanceId = 0,
                };
            }
        }
    }
}
