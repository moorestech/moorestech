using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.Connection
{
    public static class ElectricWireSystemUtil
    {
        public static bool TryConnect(Vector3Int posA, Vector3Int posB, int playerId, ItemId wireItemId, out ElectricWirePlacementFailureReason failureReason)
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
                failureReason = judgement.FailureReason;
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
                failureReason = ElectricWirePlacementFailureReason.ConnectionLimit;
                return false;
            }

            ConsumeItem(inventory, wireItemId, judgement.WireCost.Count);
            ServerContext.GetService<IElectricWireNetworkMutation>().MarkTopologyDirty();

            return true;
        }

        // 指定アイテムをインベントリのスロット順に減算する
        // Decrease the given item across inventory slots in order
        public static void ConsumeItem(IOpenableInventory inventory, ItemId itemId, int amount)
        {
            var remaining = amount;
            for (var i = 0; i < inventory.InventoryItems.Count && 0 < remaining; i++)
            {
                var itemStack = inventory.InventoryItems[i];
                if (itemStack.Id != itemId) continue;

                var consumeAmount = Math.Min(itemStack.Count, remaining);
                inventory.SetItem(i, itemStack.SubItem(consumeAmount));
                remaining -= consumeAmount;
            }
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

        public static bool TryDisconnect(Vector3Int posA, Vector3Int posB, int playerId, out ElectricWirePlacementFailureReason failureReason)
        {
            // 接続対象を取得する
            // Acquire target wire connectors
            failureReason = ElectricWirePlacementFailureReason.None;
            if (!TryGetWireConnector(posA, out var connectorA) || !TryGetWireConnector(posB, out var connectorB))
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
            var cost = connectorA.WireConnections[connectorB.BlockInstanceId].Cost;
            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            var hasRefund = 0 < cost.Count && cost.ItemId != ItemMaster.EmptyItemId;
            var refundStack = hasRefund ? ServerContext.ItemStackFactory.Create(cost.ItemId, cost.Count) : null;
            if (hasRefund && !inventory.InsertionCheck(new List<IItemStack> { refundStack }))
            {
                failureReason = ElectricWirePlacementFailureReason.InventoryFull;
                return false;
            }

            // 切断し、アイテムを返却する
            // Disconnect and refund items
            connectorA.TryRemoveWireConnection(connectorB.BlockInstanceId, out _);
            connectorB.TryRemoveWireConnection(connectorA.BlockInstanceId, out _);
            if (hasRefund) inventory.InsertItem(refundStack);

            ServerContext.GetService<IElectricWireNetworkMutation>().MarkTopologyDirty();

            return true;
        }

        // 両側にワイヤーを張り、片側が失敗したら自分が追加したエッジだけ戻す。成功時のみtrueを返す
        // Wire both connectors; on failure roll back only the edge this call added. Returns true only on success
        public static bool TryConnectBothSides(IElectricWireConnector self, IElectricWireConnector target, ElectricWireConnectionCost cost)
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
    }
}
