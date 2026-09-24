using System;
using System.Threading;
using Core.Master;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Network.API
{
    public static class ConnectionResponseApi
    {
        public static async UniTask<RailConnectionEditProtocol.ResponseRailConnectionEditMessagePack> DisconnectRailAsync(
            this VanillaApiWithResponse api,
            int playerId,
            int fromNodeId,
            Guid fromGuid,
            int toNodeId,
            Guid toGuid,
            CancellationToken ct)
        {
            var request = RailConnectionEditProtocol.RailConnectionEditRequest.CreateDisconnectRequest(playerId, fromNodeId, fromGuid, toNodeId, toGuid);
            return await api.PacketExchange.GetPacketResponse<RailConnectionEditProtocol.ResponseRailConnectionEditMessagePack>(request, ct);
        }

        public static async UniTask<RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse> PlaceRailWithPier(
            this VanillaApiWithResponse api,
            int fromNodeId,
            Guid fromGuid,
            BlockId pierBlockId,
            PlaceInfo pierPlaceInfo,
            Guid railTypeGuid,
            CancellationToken ct)
        {
            var request = RailConnectWithPlacePierProtocol.RailConnectWithPlacePierRequest.Create(api.ConnectionSetting.PlayerId, fromNodeId, fromGuid, pierBlockId, pierPlaceInfo, railTypeGuid);
            return await api.PacketExchange.GetPacketResponse<RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse>(request, ct);
        }

        // 電線延長プロトコルの唯一の送信口。Operationごとの組み立てはRequestのstatic factoryに委ねる
        // Sole send entry for the wire-extend protocol; per-operation assembly is delegated to the Request's static factories
        public static async UniTask<ElectricWireExtendProtocol.ElectricWireExtendResponse> SendElectricWireExtend(
            this VanillaApiWithResponse api,
            ElectricWireExtendProtocol.ElectricWireExtendRequest request,
            CancellationToken ct)
        {
            return await api.PacketExchange.GetPacketResponse<ElectricWireExtendProtocol.ElectricWireExtendResponse>(request, ct);
        }

        // 起点ポールから新規ポールを自動設置しつつチェーン接続する
        // Place a new pole from the source pole and connect the chain
        public static async UniTask<GearChainPoleExtendProtocol.GearChainPoleExtendResponse> ExtendGearChainPole(
            this VanillaApiWithResponse api,
            Vector3Int fromPolePos,
            BlockId poleBlockId,
            PlaceInfo polePlaceInfo,
            Guid connectToolGuid,
            CancellationToken ct)
        {
            var request = GearChainPoleExtendProtocol.GearChainPoleExtendRequest.CreateExtendRequest(api.ConnectionSetting.PlayerId, fromPolePos, poleBlockId, polePlaceInfo, connectToolGuid);
            return await api.PacketExchange.GetPacketResponse<GearChainPoleExtendProtocol.GearChainPoleExtendResponse>(request, ct);
        }

        // 接続なしの孤立ポールを設置する
        // Place an isolated pole without any connection
        public static async UniTask<GearChainPoleExtendProtocol.GearChainPoleExtendResponse> PlaceIsolatedGearChainPole(
            this VanillaApiWithResponse api,
            BlockId poleBlockId,
            PlaceInfo polePlaceInfo,
            CancellationToken ct)
        {
            var request = GearChainPoleExtendProtocol.GearChainPoleExtendRequest.CreateIsolatedPlaceRequest(api.ConnectionSetting.PlayerId, poleBlockId, polePlaceInfo);
            return await api.PacketExchange.GetPacketResponse<GearChainPoleExtendProtocol.GearChainPoleExtendResponse>(request, ct);
        }
    }
}
