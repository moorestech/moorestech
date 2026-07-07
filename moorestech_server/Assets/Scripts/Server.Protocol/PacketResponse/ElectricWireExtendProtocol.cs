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
                request.HasFromConnector, request.FromPosVector, request.PolePlaceInfo,
                request.PlayerId, request.PoleInventorySlot, new ItemId(request.WireItemId));

            return result.IsSuccess
                ? ElectricWireExtendResponse.CreateSuccess(result.PlacedPolePos, result.PlacedBlockInstanceId)
                : ElectricWireExtendResponse.CreateFailure(result.FailureReason);
        }

        [MessagePackObject]
        public class ElectricWireExtendRequest : ProtocolMessagePackBase
        {
            [Key(2)] public bool HasFromConnector { get; set; }
            [Key(3)] public Vector3IntMessagePack FromPos { get; set; }
            [Key(4)] public PlaceBlockFromHotBarProtocol.PlaceInfoMessagePack PolePlaceInfo { get; set; }
            [Key(5)] public int PlayerId { get; set; }
            [Key(6)] public int PoleInventorySlot { get; set; }
            [Key(7)] public int WireItemId { get; set; }

            [IgnoreMember] public Vector3Int FromPosVector => FromPos;

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ElectricWireExtendRequest() { Tag = ElectricWireExtendProtocol.Tag; }

            public static ElectricWireExtendRequest CreateExtendRequest(int playerId, Vector3Int fromPos, int poleInventorySlot, PlaceInfo polePlaceInfo, ItemId wireItemId)
            {
                return new ElectricWireExtendRequest
                {
                    Tag = ElectricWireExtendProtocol.Tag,
                    HasFromConnector = true,
                    FromPos = new Vector3IntMessagePack(fromPos),
                    PolePlaceInfo = new PlaceBlockFromHotBarProtocol.PlaceInfoMessagePack(polePlaceInfo),
                    PlayerId = playerId,
                    PoleInventorySlot = poleInventorySlot,
                    WireItemId = wireItemId.AsPrimitive(),
                };
            }

            public static ElectricWireExtendRequest CreateIsolatedPlaceRequest(int playerId, int poleInventorySlot, PlaceInfo polePlaceInfo, ItemId wireItemId)
            {
                return new ElectricWireExtendRequest
                {
                    Tag = ElectricWireExtendProtocol.Tag,
                    HasFromConnector = false,
                    FromPos = new Vector3IntMessagePack(Vector3Int.zero),
                    PolePlaceInfo = new PlaceBlockFromHotBarProtocol.PlaceInfoMessagePack(polePlaceInfo),
                    PlayerId = playerId,
                    PoleInventorySlot = poleInventorySlot,
                    WireItemId = wireItemId.AsPrimitive(),
                };
            }
        }

        [MessagePackObject]
        public class ElectricWireExtendResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool IsSuccess { get; set; }
            [Key(3)] public ElectricWirePlacementFailureReason FailureReason { get; set; }
            [Key(4)] public Vector3IntMessagePack PlacedPolePos { get; set; }
            [Key(5)] public int PlacedBlockInstanceId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ElectricWireExtendResponse() { Tag = ElectricWireExtendProtocol.Tag; }

            public static ElectricWireExtendResponse CreateSuccess(Vector3Int placedPolePos, int placedBlockInstanceId)
            {
                return new ElectricWireExtendResponse
                {
                    Tag = ElectricWireExtendProtocol.Tag,
                    IsSuccess = true,
                    FailureReason = ElectricWirePlacementFailureReason.None,
                    PlacedPolePos = new Vector3IntMessagePack(placedPolePos),
                    PlacedBlockInstanceId = placedBlockInstanceId,
                };
            }

            public static ElectricWireExtendResponse CreateFailure(ElectricWirePlacementFailureReason failureReason)
            {
                return new ElectricWireExtendResponse
                {
                    Tag = ElectricWireExtendProtocol.Tag,
                    IsSuccess = false,
                    FailureReason = failureReason,
                    PlacedPolePos = new Vector3IntMessagePack(Vector3Int.zero),
                    PlacedBlockInstanceId = 0,
                };
            }
        }
    }
}
