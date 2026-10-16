using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Network.API.Requests.Removal
{
    // 既存の個別撤去応答を要求元へ返す
    // Return existing individual removal responses to their requester
    public sealed class RemovalRequestApi
    {
        private readonly PacketExchangeManager _exchange;
        public RemovalRequestApi(PacketExchangeManager exchange) { _exchange = exchange; }

        public UniTask<RailConnectionEditProtocol.ResponseRailConnectionEditMessagePack> DisconnectRail(int from, Guid fromGuid, int to, Guid toGuid, CancellationToken ct)
        {
            var request = RailConnectionEditProtocol.RailConnectionEditRequest.CreateDisconnectRequest(from, fromGuid, to, toGuid);
            return _exchange.GetPacketResponse<RailConnectionEditProtocol.ResponseRailConnectionEditMessagePack>(request, ct);
        }

        public UniTask<ElectricWireDisconnectProtocol.ElectricWireDisconnectResponse> DisconnectElectricWire(Vector3Int from, Vector3Int to, CancellationToken ct)
        {
            var request = ElectricWireDisconnectProtocol.ElectricWireDisconnectRequest.CreateDisconnectRequest(from, to);
            return _exchange.GetPacketResponse<ElectricWireDisconnectProtocol.ElectricWireDisconnectResponse>(request, ct);
        }

        public UniTask<GearChainConnectionEditProtocol.GearChainConnectionEditResponse> DisconnectGearChain(Vector3Int from, Vector3Int to, CancellationToken ct)
        {
            var request = GearChainConnectionEditProtocol.GearChainConnectionEditRequest.CreateDisconnectRequest(from, to);
            return _exchange.GetPacketResponse<GearChainConnectionEditProtocol.GearChainConnectionEditResponse>(request, ct);
        }
    }
}
