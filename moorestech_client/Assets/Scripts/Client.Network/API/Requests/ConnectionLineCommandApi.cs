using System;
using Server.Protocol.PacketResponse;
using UnityEngine;
using static Server.Protocol.PacketResponse.GearChainConnectionEditProtocol;

namespace Client.Network.API.Requests
{
    // 電線と歯車チェーンの送信要求をまとめる
    // Group request sends for electric wires and gear chains
    public sealed class ConnectionLineCommandApi
    {
        private readonly PacketSender _packetSender;

        public ConnectionLineCommandApi(PacketSender packetSender)
        {
            _packetSender = packetSender;
        }

        /// <summary>
        /// 既存の電気ブロック間に電線を引く（応答は待たず、拒否は通知で受ける）
        /// Draw a wire between existing electric blocks (no response awaited; refusals arrive as notifications)
        /// </summary>
        public void ConnectElectricWire(Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
        {
            var request = ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateConnectRequest(posA, posB, connectToolGuid);
            _packetSender.Send(request);
        }

        /// <summary>
        /// ギアチェーンポール間の接続を作成する
        /// Create a connection between GearChainPoles
        /// </summary>
        public void ConnectGearChain(Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
        {
            var request = GearChainConnectionEditRequest.CreateConnectRequest(posA, posB, connectToolGuid);
            _packetSender.Send(request);
        }

        // ポール間の記録済み素材を返す切断を要求する
        // Request a disconnect that refunds the recorded pole connection materials
        public void DisconnectGearChain(Vector3Int posA, Vector3Int posB)
        {
            var request = GearChainConnectionEditRequest.CreateDisconnectRequest(posA, posB);
            _packetSender.Send(request);
        }

        /// <summary>
        /// 電気系ブロック間の電線を切断する
        /// Disconnect an electric wire between electric blocks
        /// </summary>
        public void DisconnectElectricWire(Vector3Int posA, Vector3Int posB)
        {
            var request = ElectricWireDisconnectProtocol.ElectricWireDisconnectRequest.CreateDisconnectRequest(posA, posB);
            _packetSender.Send(request);
        }

    }
}
