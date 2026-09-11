using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.UnlockState;
using Core.Inventory;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.BuildMenuModule;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ConnectTool;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// 設置時の電力ワイヤー自動接続を計画・実行する。使用connectToolは解放済み（無料設置では全件）electricWireのSortPriority最小を採用する
    /// Plans and executes wire auto-connect on placement; adopts the unlocked (any, under free placement) electricWire connectTool with the smallest SortPriority
    /// </summary>
    public static class ElectricWireAutoConnectService
    {
        public static ElectricWireAutoConnectPlan EvaluateAutoConnect(BlockId blockId, Vector3Int position, BlockDirection direction, IReadOnlyList<(ItemId itemId, int count)> reservedItems, IReadOnlyList<IItemStack> inventoryItems, bool isFreePlacement)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var ownInfo = new BlockPositionInfo(position, direction, blockMaster.BlockSize);

            // 電柱/機械の振り分けは選定コアが担う
            // The selection core dispatches pole vs machine placement
            var candidates = ElectricWireAutoConnectTargetCollector.CollectTargets(blockMaster, ownInfo);

            // 無料設置は素材を消費しない
            // Free placement never consumes materials
            var consumesMaterials = !isFreePlacement;

            if (candidates.Count == 0)
                return ElectricWireAutoConnectPlan.Success(Array.Empty<(BlockInstanceId, ElectricWireConnectionCost)>(), Guid.Empty, consumesMaterials);

            // electricWire connectToolをSortPriority昇順で取得する。無料設置は解放を無視する（ADR 0056）
            // Fetch electricWire connectTools ascending by SortPriority; free placement ignores unlock (ADR 0056)
            var unlockState = ServerContext.GetService<IGameUnlockStateDataController>();
            var candidateTools = ConnectToolSelector.CandidatesByToolType(ConnectToolMasterElement.ToolTypeConst.electricWire, unlockState, isFreePlacement).ToList();

            // 電線connectToolが未解放の世界では配線せず設置のみ許可する（設置自体はブロックしない）
            // With no unlocked wire connectTool, allow placement without wiring (do not block the placement itself)
            if (candidateTools.Count == 0)
                return ElectricWireAutoConnectPlan.Success(Array.Empty<(BlockInstanceId, ElectricWireConnectionCost)>(), Guid.Empty, consumesMaterials);

            // 候補の中から全素材が賄える最初のものを選ぶ。賄えないなら従来通り設置を失敗させる
            // Pick the first candidate whose materials are all affordable; when unaffordable, fail placement as before
            return TrySelectConnectTool(candidateTools, out var targets, out var connectToolGuid)
                ? ElectricWireAutoConnectPlan.Success(targets, connectToolGuid, consumesMaterials)
                : ElectricWireAutoConnectPlan.Failure(ElectricWirePlacementFailureReason.NoWireItem);

            #region Internal

            // 必要コストを賄えるconnectToolをSortPriority昇順で探す
            // Search connectTools in ascending SortPriority for one covering all target costs
            bool TrySelectConnectTool(List<ConnectToolMasterElement> candidateElements, out List<(BlockInstanceId, ElectricWireConnectionCost)> selectedTargets, out Guid selectedConnectToolGuid)
            {
                foreach (var element in candidateElements)
                {
                    if (!TryBuildTargets(element.ConnectToolGuid, out var builtTargets, out var requiredByItem)) continue;

                    // 建設コスト等で予約済みの数量を上乗せして所持数を判定する。無料設置は所持数を見ない
                    // Add quantities reserved by construction costs when judging held counts; free placement skips the check
                    if (!isFreePlacement && !HasEnoughAll(requiredByItem)) continue;

                    selectedTargets = builtTargets;
                    selectedConnectToolGuid = element.ConnectToolGuid;
                    return true;
                }

                selectedTargets = null;
                selectedConnectToolGuid = Guid.Empty;
                return false;
            }

            bool TryBuildTargets(Guid connectToolGuid, out List<(BlockInstanceId, ElectricWireConnectionCost)> builtTargets, out Dictionary<ItemId, int> requiredByItem)
            {
                builtTargets = new List<(BlockInstanceId, ElectricWireConnectionCost)>();
                requiredByItem = new Dictionary<ItemId, int>();

                foreach (var candidate in candidates)
                {
                    if (!ElectricWirePlacementEvaluator.TryCalculateWireCost(connectToolGuid, candidate.Distance, out var cost))
                    {
                        builtTargets = null;
                        return false;
                    }

                    builtTargets.Add((candidate.TargetId, cost));
                    foreach (var material in cost.Materials)
                    {
                        requiredByItem.TryGetValue(material.ItemId, out var current);
                        requiredByItem[material.ItemId] = current + material.Count;
                    }
                }

                return true;
            }

            bool HasEnoughAll(Dictionary<ItemId, int> requiredByItem)
            {
                foreach (var (itemId, required) in requiredByItem)
                {
                    var reserved = 0;
                    foreach (var reservedItem in reservedItems)
                    {
                        if (reservedItem.itemId == itemId) reserved += reservedItem.count;
                    }
                    if (CountItem(itemId) < required + reserved) return false;
                }
                return true;
            }

            int CountItem(ItemId itemId)
            {
                var total = 0;
                foreach (var itemStack in inventoryItems)
                {
                    if (itemStack.Id != itemId) continue;
                    total += itemStack.Count;
                }
                return total;
            }

            #endregion
        }

        public static void ExecuteAutoConnect(ElectricWireAutoConnectPlan plan, IBlock placedBlock, IOpenableInventory inventory)
        {
            if (plan.Targets.Count == 0) return;

            var selfConnector = placedBlock.GetComponent<IElectricWireConnector>();
            var datastore = ServerContext.WorldBlockDatastore;
            // 事前検証済みだが実行時ズレに備え、実際に張れた接続分の素材だけを消費する
            // Validated ahead, but to survive runtime drift we consume materials only for connections that actually succeeded
            foreach (var target in plan.Targets)
            {
                var targetConnector = datastore.GetBlock(target.TargetId)?.GetComponent<IElectricWireConnector>();
                if (targetConnector == null) continue;
                if (!ElectricWireSystemUtil.TryConnectBothSides(selfConnector, targetConnector, target.Cost)) continue;

                if (plan.ConsumesMaterials) ConnectToolMaterialConsumer.Consume(target.Cost.Materials, inventory);
            }

        }
    }
}
