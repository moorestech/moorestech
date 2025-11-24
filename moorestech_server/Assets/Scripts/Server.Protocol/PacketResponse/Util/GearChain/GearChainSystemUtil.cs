using System;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;
using Game.PlayerInventory.Interface;

namespace Server.Protocol.PacketResponse.Util.GearChain
{
    public static class GearChainSystemUtil
    {
        public static bool TryConnect(Vector3Int posA, Vector3Int posB, int playerId, out string error)
        {
            // 接続対象を取得する
            // Acquire target chain poles
            error = string.Empty;
            if (!TryGetGearChainPole(posA, out var poleA, out var transformerA, out var blockA) || !TryGetGearChainPole(posB, out var poleB, out var transformerB, out var blockB))
            {
                error = "InvalidTarget";
                return false;
            }
            
            // 接続距離を算出する
            // Calculate connection distance
            var connectionDistance = Vector3Int.Distance(posA, posB);

            // 同一ターゲットや距離超過を弾く
            // Reject same target or over distance
            if (poleA.BlockInstanceId == poleB.BlockInstanceId)
            {
                error = "InvalidTarget";
                return false;
            }

            var maxDistance = Math.Min(poleA.MaxConnectionDistance, poleB.MaxConnectionDistance);
            if (connectionDistance > maxDistance)
            {
                error = "TooFar";
                return false;
            }

            // 既存接続がある場合は失敗させる
            // Fail when already connected
            if (poleA.ContainsChainConnection(poleB.BlockInstanceId) || poleB.ContainsChainConnection(poleA.BlockInstanceId))
            {
                error = "AlreadyConnected";
                return false;
            }

            // 接続数の上限を確認する
            // Ensure neither pole is at capacity
            if (poleA.IsConnectionFull || poleB.IsConnectionFull)
            {
                error = "ConnectionLimit";
                return false;
            }

            // チェーンアイテムを消費する
            // Consume chain item
            var gearChainItems = ResolveGearChainItems(blockA.BlockMasterElement, blockB.BlockMasterElement);
            if (!TryConsumeChainItem(playerId, connectionDistance, gearChainItems, out var consumedCost))
            {
                error = "NoItem";
                return false;
            }

            var costForA = new GearChainConnectionCost(consumedCost.ItemId, consumedCost.Count, consumedCost.PlayerId, true);
            var costForB = new GearChainConnectionCost(consumedCost.ItemId, consumedCost.Count, consumedCost.PlayerId, true);

            // 接続を確定させる
            // Finalize connection
            var addedA = poleA.TryAddChainConnection(poleB.BlockInstanceId, costForA);
            var addedB = addedA && poleB.TryAddChainConnection(poleA.BlockInstanceId, costForB);
            if (!addedA || !addedB)
            {
                poleA.RemoveChainConnection(poleB.BlockInstanceId);
                poleB.RemoveChainConnection(poleA.BlockInstanceId);
                RefundConsumption(consumedCost);
                error = "ConnectionLimit";
                return false;
            }

            RebuildNetworks(transformerA, transformerB);
            return true;
        }

        public static bool TryDisconnect(Vector3Int posA, Vector3Int posB, out string error)
        {
            // 接続対象を取得する
            // Acquire target chain poles
            error = string.Empty;
            if (!TryGetGearChainPole(posA, out var poleA, out var transformerA, out _) || !TryGetGearChainPole(posB, out var poleB, out var transformerB, out _))
            {
                error = "InvalidTarget";
                return false;
            }

            // 相互接続でない場合は失敗
            // Fail when not connected to each other
            if (!poleA.ContainsChainConnection(poleB.BlockInstanceId) || !poleB.ContainsChainConnection(poleA.BlockInstanceId))
            {
                error = "NotConnected";
                return false;
            }

            poleA.RemoveChainConnection(poleB.BlockInstanceId);
            poleB.RemoveChainConnection(poleA.BlockInstanceId);
            RebuildNetworks(transformerA, transformerB);
            return true;
        }

        private static GearChainItemsElement[] ResolveGearChainItems(BlockMasterElement blockA, BlockMasterElement blockB)
        {
            // ブロックのギアチェーン設定を優先的に取得する
            // Prefer gear chain settings defined on the blocks
            if (blockA?.GearChainItems is { Length: > 0 }) return blockA.GearChainItems;
            if (blockB?.GearChainItems is { Length: > 0 }) return blockB.GearChainItems;
            return Array.Empty<GearChainItemsElement>();
        }

        private static bool TryConsumeChainItem(int playerId, float distance, GearChainItemsElement[] gearChainItems, out GearChainConnectionCost consumedCost)
        {
            // 設定が無ければ消費不要
            // Skip consumption when no configuration is provided
            if (gearChainItems == null || gearChainItems.Length == 0)
            {
                consumedCost = new GearChainConnectionCost(ItemMaster.EmptyItemId, 0, playerId, false);
                return true;
            }

            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;

            // 設定順に使用可能なアイテムを探す
            // Find the first usable item by configuration order
            foreach (var gearChainItem in gearChainItems)
            {
                var required = Mathf.CeilToInt(distance * gearChainItem.ConsumptionPerLength);
                if (required <= 0)
                {
                    consumedCost = new GearChainConnectionCost(ItemMaster.EmptyItemId, 0, playerId, false);
                    return true;
                }

                var itemId = MasterHolder.ItemMaster.GetItemId(gearChainItem.ItemGuid);

                // 手持ちが足りなければ次の候補へ
                // Skip when inventory is insufficient
                var totalCount = CountItem(inventory, itemId);
                if (totalCount < required) continue;

                // 指定数を減算する
                // Consume the required amount
                Consume(inventory, itemId, required);
                consumedCost = new GearChainConnectionCost(itemId, required, playerId, false);
                return true;
            }

            consumedCost = new GearChainConnectionCost(ItemMaster.EmptyItemId, 0, playerId, false);
            return false;

            #region Internal

            int CountItem(IOpenableInventory openableInventory, ItemId itemId)
            {
                // 対象アイテムの合計数を数える
                // Sum item counts for the target item
                var total = 0;
                foreach (var itemStack in openableInventory.InventoryItems)
                {
                    if (itemStack.Id != itemId) continue;
                    total += itemStack.Count;
                }

                return total;
            }

            void Consume(IOpenableInventory openableInventory, ItemId itemId, int required)
            {
                // スロットを順に減算する
                // Decrease stacks across slots
                var remaining = required;
                for (var i = 0; i < openableInventory.InventoryItems.Count && 0 < remaining; i++)
                {
                    var itemStack = openableInventory.InventoryItems[i];
                    if (itemStack.Id != itemId) continue;

                    var consumeAmount = Math.Min(itemStack.Count, remaining);
                    var updated = itemStack.SubItem(consumeAmount);
                    openableInventory.SetItem(i, updated);
                    remaining -= consumeAmount;
                }
            }

            #endregion
        }

        private static void RefundConsumption(GearChainConnectionCost cost)
        {
            // 消費したアイテムをインベントリへ戻す
            // Return consumed items to inventory
            if (cost.Count <= 0 || cost.ItemId == ItemMaster.EmptyItemId) return;
            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(cost.PlayerId).MainOpenableInventory;
            var remainder = inventory.InsertItem(cost.ItemId, cost.Count);
            if (remainder.Count <= 0) return;
            inventory.InsertItem(remainder);
        }

        private static bool TryGetGearChainPole(Vector3Int position, out IGearChainPole chainPole, out IGearEnergyTransformer transformer, out IBlock block)
        {
            // 指定座標からコンポーネントを解決する
            // Resolve component from position
            block = null;
            chainPole = null;
            transformer = null;
            if (!ServerContext.WorldBlockDatastore.TryGetBlock(position, out block)) return false;
            chainPole = block.GetComponent<IGearChainPole>();
            transformer = block.GetComponent<IGearEnergyTransformer>();
            return chainPole != null && transformer != null;
        }

        private static void RebuildNetworks(params IGearEnergyTransformer[] transformers)
        {
            // ネットワークを再構築して回転を再計算する
            // Rebuild gear networks to recalc rotation
            
            foreach (var transformer in transformers) GearNetworkDatastore.RemoveGear(transformer);
            foreach (var transformer in transformers) GearNetworkDatastore.AddGear(transformer);
        }
    }
}
