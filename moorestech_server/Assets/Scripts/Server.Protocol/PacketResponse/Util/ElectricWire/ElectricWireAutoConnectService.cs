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
    /// 設置時の電力ワイヤー自動接続を計画・実行する
    /// Plans and executes wire auto-connect on placement
    /// </summary>
    public static class ElectricWireAutoConnectService
    {
        public static ElectricWireAutoConnectPlan EvaluateAutoConnect(BlockId blockId, Vector3Int position, BlockDirection direction, ItemId placingItemId, IReadOnlyList<IItemStack> inventoryItems)
        {
            // 電線アイテム未設定のマスタでは自動接続なしで設置を許可する
            // With no wire items configured, allow placement without auto-connect
            if (MasterHolder.BlockMaster.Blocks.ElectricWireItems.Length == 0)
                return ElectricWireAutoConnectPlan.Success(Array.Empty<(BlockInstanceId, ElectricWireConnectionCost)>(), ItemMaster.EmptyItemId);

            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);

            // 電柱設置か機械/発電機設置かで対象選定ロジックが異なる
            // Target selection differs between pole placement and machine/generator placement
            var candidates = blockMaster.BlockParam is ElectricPoleBlockParam poleParam
                ? ElectricWireAutoConnectTargetCollector.CollectPoleTargets(poleParam, position)
                : CollectMachineTargets(blockMaster, position, direction);

            if (candidates.Count == 0)
                return ElectricWireAutoConnectPlan.Success(Array.Empty<(BlockInstanceId, ElectricWireConnectionCost)>(), ItemMaster.EmptyItemId);

            // 合計コストを所持数が満たす最初の電線を選ぶ
            // Pick the first wire item whose held count covers the summed cost across all targets
            return TrySelectWireItem(out var targets, out var wireItemId)
                ? ElectricWireAutoConnectPlan.Success(targets, wireItemId)
                : ElectricWireAutoConnectPlan.Failure(ElectricWirePlacementFailureReason.NoWireItem);

            #region Internal

            List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> CollectMachineTargets(BlockMasterElement master, Vector3Int pos, BlockDirection dir)
            {
                // 自身の接続容量が0なら探索するまでもなく対象なし
                // No point searching when this block has zero connection capacity
                if (!ElectricWireBlockParamResolver.TryGetWireParam(master.BlockParam, out var ownCapacity, out var ownMaxWireLength) || ownCapacity <= 0)
                    return new List<(BlockInstanceId, IElectricWireConnector, float)>();

                return ElectricWireAutoConnectTargetCollector.CollectMachineTargets(master, pos, dir, ownMaxWireLength);
            }

            // 距離を満たす電線をマスタ設定順に探す
            // Search wire item configs in master order for one covering all target distances
            bool TrySelectWireItem(out List<(BlockInstanceId, ElectricWireConnectionCost)> selectedTargets, out ItemId selectedWireItemId)
            {
                foreach (var electricWireItem in MasterHolder.BlockMaster.Blocks.ElectricWireItems)
                {
                    var candidateItemId = MasterHolder.ItemMaster.GetItemId(electricWireItem.ItemGuid);
                    if (!TryBuildTargets(candidateItemId, out var builtTargets, out var totalRequired)) continue;

                    // 設置ブロックと電線が同一アイテムなら設置分の1個を上乗せして判定する
                    // Require one extra when the placed block shares the wire item
                    var requiredTotal = totalRequired + (candidateItemId == placingItemId ? 1 : 0);
                    if (!HasEnoughItem(candidateItemId, requiredTotal)) continue;

                    selectedTargets = builtTargets;
                    selectedWireItemId = candidateItemId;
                    return true;
                }

                selectedTargets = null;
                selectedWireItemId = ItemMaster.EmptyItemId;
                return false;
            }

            bool TryBuildTargets(ItemId candidateItemId, out List<(BlockInstanceId, ElectricWireConnectionCost)> builtTargets, out int totalRequired)
            {
                builtTargets = new List<(BlockInstanceId, ElectricWireConnectionCost)>();
                totalRequired = 0;

                foreach (var candidate in candidates)
                {
                    if (!ElectricWirePlacementEvaluator.TryCalculateWireCost(candidateItemId, candidate.Distance, out var cost))
                    {
                        builtTargets = null;
                        return false;
                    }

                    builtTargets.Add((candidate.TargetId, cost));
                    totalRequired += cost.Count;
                }

                return true;
            }

            bool HasEnoughItem(ItemId itemId, int required)
            {
                var total = 0;
                foreach (var itemStack in inventoryItems)
                {
                    if (itemStack.Id != itemId) continue;
                    total += itemStack.Count;
                }

                return required <= total;
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

            ElectricWireSystemUtil.ConsumeItem(inventory, plan.WireItemId, consumedWireCount);
            ServerContext.GetService<IElectricWireNetworkDatastore>().RebuildAround(connectedConnectors.ToArray());
        }
    }
}
