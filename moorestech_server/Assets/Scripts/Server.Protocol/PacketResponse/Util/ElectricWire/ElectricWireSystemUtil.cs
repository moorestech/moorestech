using System;
using Core.Master;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.ElectricWire
{
    public static class ElectricWireSystemUtil
    {
        public static bool TryConnect(Vector3Int posA, Vector3Int posB, int playerId, ItemId wireItemId, out string error)
        {
            // 接続対象を取得する
            // Acquire target wire connectors
            error = string.Empty;
            var foundA = TryGetWireConnector(posA, out var connectorA);
            var foundB = TryGetWireConnector(posB, out var connectorB);

            if (!foundA || !foundB)
            {
                error = ElectricWirePlacementEvaluator.InvalidTargetError;
                return false;
            }

            if (connectorA.BlockInstanceId == connectorB.BlockInstanceId)
            {
                error = ElectricWirePlacementEvaluator.InvalidTargetError;
                return false;
            }

            // 距離・既存接続・所持アイテムを評価に渡す
            // Feed distance, existing connection state and held items into the evaluation
            var distance = Vector3Int.Distance(posA, posB);
            var alreadyConnected = connectorA.ContainsWireConnection(connectorB.BlockInstanceId) || connectorB.ContainsWireConnection(connectorA.BlockInstanceId);
            var anyConnectionFull = connectorA.IsWireConnectionFull || connectorB.IsWireConnectionFull;
            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;

            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                distance, connectorA.MaxWireLength, connectorB.MaxWireLength,
                alreadyConnected, anyConnectionFull, wireItemId, inventory.InventoryItems, ItemMaster.EmptyItemId);

            if (!judgement.IsPlaceable)
            {
                error = judgement.FailureReason;
                return false;
            }

            // 接続を確定させる。片方が失敗した場合はロールバックする
            // Finalize the connection; roll back when either side fails
            var addedA = connectorA.TryAddWireConnection(connectorB.BlockInstanceId, judgement.WireCost);
            var addedB = addedA && connectorB.TryAddWireConnection(connectorA.BlockInstanceId, judgement.WireCost);
            if (!addedA || !addedB)
            {
                connectorA.TryRemoveWireConnection(connectorB.BlockInstanceId, out _);
                connectorB.TryRemoveWireConnection(connectorA.BlockInstanceId, out _);
                error = ElectricWirePlacementEvaluator.ConnectionLimitError;
                return false;
            }

            Consume(judgement.WireCost);
            ServerContext.GetService<IElectricWireNetworkDatastore>().RebuildAround(connectorA, connectorB);

            return true;

            #region Internal

            void Consume(ElectricWireConnectionCost consumedCost)
            {
                var remaining = consumedCost.Count;

                // スロットを順に減算する
                // Decrease stacks across slots
                for (var i = 0; i < inventory.InventoryItems.Count && 0 < remaining; i++)
                {
                    var itemStack = inventory.InventoryItems[i];
                    if (itemStack.Id != wireItemId) continue;

                    var consumeAmount = Math.Min(itemStack.Count, remaining);
                    var updated = itemStack.SubItem(consumeAmount);
                    inventory.SetItem(i, updated);

                    remaining -= consumeAmount;
                }
            }

            #endregion
        }

        public static bool TryDisconnect(Vector3Int posA, Vector3Int posB, int playerId, out string error)
        {
            // 接続対象を取得する
            // Acquire target wire connectors
            error = string.Empty;
            if (!TryGetWireConnector(posA, out var connectorA) || !TryGetWireConnector(posB, out var connectorB))
            {
                error = ElectricWirePlacementEvaluator.InvalidTargetError;
                return false;
            }

            // 相互接続でない場合は失敗
            // Fail when not connected to each other
            if (!connectorA.ContainsWireConnection(connectorB.BlockInstanceId) || !connectorB.ContainsWireConnection(connectorA.BlockInstanceId))
            {
                error = "NotConnected";
                return false;
            }

            // 切断し、アイテムを返却する
            // Disconnect and refund items
            connectorA.TryRemoveWireConnection(connectorB.BlockInstanceId, out var cost);
            connectorB.TryRemoveWireConnection(connectorA.BlockInstanceId, out _);

            RefundConsumption(cost, playerId);

            ServerContext.GetService<IElectricWireNetworkDatastore>().RebuildAround(connectorA, connectorB);

            return true;

            #region Internal

            void RefundConsumption(ElectricWireConnectionCost connectionCost, int player)
            {
                // 消費したアイテムをインベントリへ戻す
                // Return consumed items to inventory
                var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(player).MainOpenableInventory;

                var remainder = inventory.InsertItem(connectionCost.ItemId, connectionCost.Count);

                if (remainder.Count <= 0) return;
                inventory.InsertItem(remainder);
            }

            #endregion
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
    }
}
