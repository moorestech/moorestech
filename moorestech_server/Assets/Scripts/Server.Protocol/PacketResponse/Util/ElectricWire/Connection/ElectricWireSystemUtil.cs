using Game.Block.Interface.Component;
using System;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ConnectTool;
using Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect;
using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.Connection
{
    public static class ElectricWireSystemUtil
    {
        public static bool TryConnect(Vector3Int posA, Vector3Int posB, int playerId, Guid connectToolGuid, out ElectricWirePlacementFailureReason failureReason)
        {
            // 接続対象を取得する
            // Acquire target wire connectors
            failureReason = ElectricWirePlacementFailureReason.None;
            var foundA = TryGetWireConnector(posA, out var connectorA);
            var foundB = TryGetWireConnector(posB, out var connectorB);

            if (!foundA || !foundB)
            {
                failureReason = ElectricWirePlacementFailureReason.InvalidTarget;
                return false;
            }

            if (connectorA.BlockInstanceId == connectorB.BlockInstanceId)
            {
                failureReason = ElectricWirePlacementFailureReason.InvalidTarget;
                return false;
            }

            // 未解放のconnectToolによる接続要求は拒否する
            // Reject connection requests using a connectTool that is not unlocked
            if (!IsConnectToolUnlocked(connectToolGuid))
            {
                failureReason = ElectricWirePlacementFailureReason.NotUnlocked;
                return false;
            }

            // 双方の範囲ボックス相互判定を行う。範囲外なら接続不可
            // Mutual range-box check between both endpoints; out of range fails
            var datastore = ServerContext.WorldBlockDatastore;
            var blockA = datastore.GetBlock(connectorA.BlockInstanceId);
            var blockB = datastore.GetBlock(connectorB.BlockInstanceId);
            if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(blockA.BlockMasterElement.BlockParam, out _, out var profileA, out var isPoleA) ||
                !ElectricWireBlockParamResolver.TryGetWireRangeParam(blockB.BlockMasterElement.BlockParam, out _, out var profileB, out var isPoleB))
            {
                failureReason = ElectricWirePlacementFailureReason.InvalidTarget;
                return false;
            }
            if (!ElectricConnectionRangeService.IsMutuallyConnectable(blockA.BlockPositionInfo, profileA, isPoleA, blockB.BlockPositionInfo, profileB, isPoleB))
            {
                failureReason = ElectricWirePlacementFailureReason.OutOfRange;
                return false;
            }

            // 距離はコスト計算専用。既存接続・所持アイテムを評価に渡す
            // Distance feeds cost only; pass existing connection state and held items to the evaluation
            var distance = Vector3Int.Distance(posA, posB);
            var alreadyConnected = AreConnected(connectorA, connectorB);
            var anyConnectionFull = connectorA.IsWireConnectionFull || connectorB.IsWireConnectionFull;
            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;

            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                distance, alreadyConnected, anyConnectionFull, connectToolGuid, inventory.InventoryItems, null);

            if (!judgement.IsPlaceable)
            {
                failureReason = judgement.FailureReason;
                return false;
            }

            // 接続を確定させる。片方が失敗した場合はロールバックする
            // Finalize the connection; roll back when either side fails
            if (!TryConnectBothSides(connectorA, connectorB, judgement.WireRecord))
            {
                failureReason = ElectricWirePlacementFailureReason.ConnectionLimit;
                return false;
            }

            ConnectToolMaterialConsumer.Consume(judgement.WireRecord.Materials, inventory);

            return true;
        }

        // connectToolの解放状態を確認する
        // Check whether the connectTool is unlocked
        public static bool IsConnectToolUnlocked(Guid connectToolGuid)
        {
            var infos = ServerContext.GetService<IGameUnlockStateDataController>().ConnectToolUnlockStateInfos;
            return infos.TryGetValue(connectToolGuid, out var info) && info.IsUnlocked;
        }

        // 指定アイテムの所持合計を数える
        // Count the total held amount of the given item
        public static int CountItem(IOpenableInventory inventory, ItemId itemId)
        {
            var total = 0;
            foreach (var itemStack in inventory.InventoryItems)
                if (itemStack.Id == itemId)
                    total += itemStack.Count;
            return total;
        }



        // 両側にワイヤーを張り、片側が失敗したら自分が追加したエッジだけ戻す。成功時のみtrueを返す
        // Wire both connectors; on failure roll back only the edge this call added. Returns true only on success
        public static bool TryConnectBothSides(IElectricWireConnector self, IElectricWireConnector target, ConnectionLineRecord cost)
        {
            // 自分側が張れない（既接続・上限）なら既存エッジに触れず失敗させる
            // When the self side cannot add (already connected / full), fail without touching existing edges
            if (!self.TryAddWireConnection(target.BlockInstanceId, cost)) return false;
            if (target.TryAddWireConnection(self.BlockInstanceId, cost)) return true;

            self.TryRemoveWireConnection(target.BlockInstanceId, out _);
            return false;
        }

        public static bool TryGetWireConnector(Vector3Int position, out IElectricWireConnector connector)
        {
            // 指定座標からコンポーネントを解決する
            // Resolve component from position
            connector = null;

            var blockFound = ServerContext.WorldBlockDatastore.TryGetBlock(position, out var block);
            if (!blockFound) return false;

            connector = block.GetComponent<IElectricWireConnector>();
            return connector != null;
        }

        public static bool TryGetExistingConnection(Vector3Int posA, Vector3Int posB, out IElectricWireConnector connectorB)
        {
            connectorB = null;
            if (!TryGetWireConnector(posA, out var connectorA) || !TryGetWireConnector(posB, out connectorB)) return false;
            if (connectorA.BlockInstanceId == connectorB.BlockInstanceId) return false;
            return AreConnected(connectorA, connectorB);
        }

        private static bool AreConnected(IElectricWireConnector connectorA, IElectricWireConnector connectorB)
        {
            return connectorA.ContainsWireConnection(connectorB.BlockInstanceId) || connectorB.ContainsWireConnection(connectorA.BlockInstanceId);
        }
    }
}
