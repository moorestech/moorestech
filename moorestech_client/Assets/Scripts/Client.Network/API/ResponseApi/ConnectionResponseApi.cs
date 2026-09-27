using System;
using System.Threading;
using Client.Network.Settings;
using Core.Master;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Network.API
{
    public class ConnectionResponseApi
    {
        private readonly PacketExchangeManager _packetExchange;
        private readonly PlayerConnectionSetting _connectionSetting;

        public ConnectionResponseApi(PacketExchangeManager packetExchangeManager, PlayerConnectionSetting playerConnectionSetting)
        {
            _packetExchange = packetExchangeManager;
            _connectionSetting = playerConnectionSetting;
        }

        public async UniTask<RailConnectionEditProtocol.ResponseRailConnectionEditMessagePack> DisconnectRailAsync(
            int playerId,
            int fromNodeId,
            Guid fromGuid,
            int toNodeId,
            Guid toGuid,
            CancellationToken ct)
        {
            var request = RailConnectionEditProtocol.RailConnectionEditRequest.CreateDisconnectRequest(playerId, fromNodeId, fromGuid, toNodeId, toGuid);
            return await _packetExchange.GetPacketResponse<RailConnectionEditProtocol.ResponseRailConnectionEditMessagePack>(request, ct);
        }

        public async UniTask<RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse> PlaceRailWithPier(
            int fromNodeId,
            Guid fromGuid,
            BlockId pierBlockId,
            PlaceInfo pierPlaceInfo,
            Guid railTypeGuid,
            CancellationToken ct)
        {
            var request = RailConnectWithPlacePierProtocol.RailConnectWithPlacePierRequest.Create(_connectionSetting.PlayerId, fromNodeId, fromGuid, pierBlockId, pierPlaceInfo, railTypeGuid);
            return await _packetExchange.GetPacketResponse<RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse>(request, ct);
        }

        // 電線延長プロトコルの唯一の送信口。Operationごとの組み立てはRequestのstatic factoryに委ねる
        // Sole send entry for the wire-extend protocol; per-operation assembly is delegated to the Request's static factories
        public async UniTask<ElectricWireExtendProtocol.ElectricWireExtendResponse> SendElectricWireExtend(
            ElectricWireExtendProtocol.ElectricWireExtendRequest request,
            CancellationToken ct)
        {
            return await _packetExchange.GetPacketResponse<ElectricWireExtendProtocol.ElectricWireExtendResponse>(request, ct);
        }

        // 起点ポールから新規ポールを自動設置しつつチェーン接続する
        // Place a new pole from the source pole and connect the chain
        public async UniTask<GearChainPoleExtendProtocol.GearChainPoleExtendResponse> ExtendGearChainPole(
            Vector3Int fromPolePos,
            BlockId poleBlockId,
            PlaceInfo polePlaceInfo,
            Guid connectToolGuid,
            CancellationToken ct)
        {
            var request = GearChainPoleExtendProtocol.GearChainPoleExtendRequest.CreateExtendRequest(_connectionSetting.PlayerId, fromPolePos, poleBlockId, polePlaceInfo, connectToolGuid);
            return await _packetExchange.GetPacketResponse<GearChainPoleExtendProtocol.GearChainPoleExtendResponse>(request, ct);
        }

        // 接続なしの孤立ポールを設置する
        // Place an isolated pole without any connection
        public async UniTask<GearChainPoleExtendProtocol.GearChainPoleExtendResponse> PlaceIsolatedGearChainPole(
            BlockId poleBlockId,
            PlaceInfo polePlaceInfo,
            CancellationToken ct)
        {
            var request = GearChainPoleExtendProtocol.GearChainPoleExtendRequest.CreateIsolatedPlaceRequest(_connectionSetting.PlayerId, poleBlockId, polePlaceInfo);
            return await _packetExchange.GetPacketResponse<GearChainPoleExtendProtocol.GearChainPoleExtendResponse>(request, ct);
        }
    }
}
