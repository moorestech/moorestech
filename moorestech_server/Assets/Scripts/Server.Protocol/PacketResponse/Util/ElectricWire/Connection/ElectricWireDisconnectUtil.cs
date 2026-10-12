using Game.Context;
using Game.PlayerInventory.Interface;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.Connection
{
    /// <summary>
    /// 電線1本の切断と返却。返却が入らなければ切断させない
    /// Disconnect one wire and refund it; refuse the disconnect when the refund cannot fit
    /// </summary>
    public static class ElectricWireDisconnectUtil
    {
        public static bool TryDisconnect(Vector3Int posA, Vector3Int posB, int playerId, out ElectricWirePlacementFailureReason failureReason)
        {
            // 接続対象を取得する
            // Acquire target wire connectors
            failureReason = ElectricWirePlacementFailureReason.None;
            if (!ElectricWireSystemUtil.TryGetWireConnector(posA, out var connectorA) || !ElectricWireSystemUtil.TryGetWireConnector(posB, out var connectorB))
            {
                failureReason = ElectricWirePlacementFailureReason.InvalidTarget;
                return false;
            }

            // 相互接続でない場合は失敗
            // Fail when not connected to each other
            if (!connectorA.ContainsWireConnection(connectorB.BlockInstanceId) || !connectorB.ContainsWireConnection(connectorA.BlockInstanceId))
            {
                failureReason = ElectricWirePlacementFailureReason.NotConnected;
                return false;
            }

            // 返却アイテムが入らない場合は切断させない（返却消滅の防止）
            // Reject the disconnect when the refund cannot fit, preventing item loss
            var record = connectorA.WireConnections[connectorB.BlockInstanceId].Record;
            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            if (!ConnectToolMaterialConsumer.TryCreateFittingRefund(record.Materials, inventory, out var refundStacks))
            {
                failureReason = ElectricWirePlacementFailureReason.InventoryFull;
                return false;
            }

            // 切断し、アイテムを返却する
            // Disconnect and refund items
            connectorA.TryRemoveWireConnection(connectorB.BlockInstanceId, out _);
            connectorB.TryRemoveWireConnection(connectorA.BlockInstanceId, out _);
            foreach (var refundStack in refundStacks) inventory.InsertItem(refundStack);
            return true;
        }
    }
}
