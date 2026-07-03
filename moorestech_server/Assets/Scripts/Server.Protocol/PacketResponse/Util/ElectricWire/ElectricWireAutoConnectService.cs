using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Core.Inventory;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.ElectricWire
{
    /// <summary>
    /// 設置時の電力ワイヤー自動接続を計画・実行する。全検証を設置前に完了させ、通過時のみ状態を変更する
    /// Plans and executes wire auto-connect on placement; validates everything before mutating state
    /// </summary>
    public static class ElectricWireAutoConnectService
    {
        public static ElectricWireAutoConnectPlan EvaluateAutoConnect(BlockId blockId, Vector3Int position, BlockDirection direction, IReadOnlyList<IItemStack> inventoryItems)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);

            // 電柱設置か機械/発電機設置かで対象選定ロジックが異なる
            // Target selection differs between pole placement and machine/generator placement
            var candidates = blockMaster.BlockParam is ElectricPoleBlockParam poleParam
                ? ElectricWireAutoConnectTargetCollector.CollectPoleTargets(poleParam, position)
                : CollectMachineTargets(blockMaster, position, direction);

            if (candidates.Count == 0)
                return ElectricWireAutoConnectPlan.Success(Array.Empty<(BlockInstanceId, ElectricWireConnectionCost)>(), ItemMaster.EmptyItemId);

            // 全ターゲット合計コストを所持数が満たす最初の電線アイテムを選ぶ
            // Pick the first wire item whose held count covers the summed cost across all targets
            return TrySelectWireItem(candidates, inventoryItems, out var targets, out var wireItemId)
                ? ElectricWireAutoConnectPlan.Success(targets, wireItemId)
                : ElectricWireAutoConnectPlan.Failure(ElectricWirePlacementEvaluator.NoWireItemError);

            #region Internal

            List<(BlockInstanceId, IElectricWireConnector, float)> CollectMachineTargets(BlockMasterElement master, Vector3Int pos, BlockDirection dir)
            {
                // 自身の接続容量が0なら探索するまでもなく対象なし
                // No point searching when this block has zero connection capacity
                if (!ElectricWireBlockParamResolver.TryGetWireParam(master.BlockParam, out var ownCapacity, out var ownMaxWireLength) || ownCapacity <= 0)
                    return new List<(BlockInstanceId, IElectricWireConnector, float)>();

                return ElectricWireAutoConnectTargetCollector.CollectMachineTargets(master, pos, dir, ownMaxWireLength);
            }

            #endregion
        }

        public static void ExecuteAutoConnect(ElectricWireAutoConnectPlan plan, IBlock placedBlock, IOpenableInventory inventory)
        {
            if (plan.Targets.Count == 0) return;

            var selfConnector = placedBlock.GetComponent<IElectricWireConnector>();
            var datastore = ServerContext.WorldBlockDatastore;
            var connectedConnectors = new List<IElectricWireConnector> { selfConnector };

            // 事前検証済みだが実行時ズレに備え、実際に張れた接続分の電線だけを消費する
            // Validated ahead, but to survive runtime drift we consume wires only for connections that actually succeeded
            var consumedWireCount = 0;
            foreach (var target in plan.Targets)
            {
                var targetConnector = datastore.GetBlock(target.TargetId)?.GetComponent<IElectricWireConnector>();
                if (targetConnector == null) continue;
                if (!ElectricWireSystemUtil.TryConnectBothSides(selfConnector, targetConnector, target.Cost)) continue;

                connectedConnectors.Add(targetConnector);
                consumedWireCount += target.Cost.Count;
            }

            ConsumeWireItems(inventory, plan.WireItemId, consumedWireCount);
            ServerContext.GetService<IElectricWireNetworkDatastore>().RebuildAround(connectedConnectors.ToArray());
        }

        // 全ターゲット距離を満たす電線アイテムをマスタ設定順に探す
        // Search wire item configs in master order for one covering all target distances
        private static bool TrySelectWireItem(
            IReadOnlyList<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> candidates,
            IReadOnlyList<IItemStack> inventoryItems,
            out List<(BlockInstanceId, ElectricWireConnectionCost)> targets,
            out ItemId wireItemId)
        {
            foreach (var electricWireItem in MasterHolder.BlockMaster.Blocks.ElectricWireItems)
            {
                var candidateItemId = MasterHolder.ItemMaster.GetItemId(electricWireItem.ItemGuid);
                if (!TryBuildTargets(candidates, candidateItemId, out var builtTargets, out var totalRequired)) continue;
                if (!HasEnoughItem(inventoryItems, candidateItemId, totalRequired)) continue;

                targets = builtTargets;
                wireItemId = candidateItemId;
                return true;
            }

            targets = null;
            wireItemId = ItemMaster.EmptyItemId;
            return false;
        }

        private static bool TryBuildTargets(
            IReadOnlyList<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> candidates,
            ItemId wireItemId,
            out List<(BlockInstanceId, ElectricWireConnectionCost)> targets,
            out int totalRequired)
        {
            targets = new List<(BlockInstanceId, ElectricWireConnectionCost)>();
            totalRequired = 0;

            foreach (var candidate in candidates)
            {
                if (!ElectricWirePlacementEvaluator.TryCalculateWireCost(wireItemId, candidate.Distance, out var cost))
                {
                    targets = null;
                    return false;
                }

                targets.Add((candidate.TargetId, cost));
                totalRequired += cost.Count;
            }

            return true;
        }

        private static bool HasEnoughItem(IReadOnlyList<IItemStack> items, ItemId itemId, int required)
        {
            var total = 0;
            foreach (var itemStack in items)
            {
                if (itemStack.Id != itemId) continue;
                total += itemStack.Count;
            }

            return required <= total;
        }

        // 消費した電線アイテムをインベントリのスロットから順に減算する
        // Decrease consumed wire items across inventory slots in order
        private static void ConsumeWireItems(IOpenableInventory inventory, ItemId wireItemId, int amount)
        {
            var remaining = amount;
            for (var i = 0; i < inventory.InventoryItems.Count && 0 < remaining; i++)
            {
                var itemStack = inventory.InventoryItems[i];
                if (itemStack.Id != wireItemId) continue;

                var consumeAmount = Math.Min(itemStack.Count, remaining);
                inventory.SetItem(i, itemStack.SubItem(consumeAmount));
                remaining -= consumeAmount;
            }
        }
    }
}
