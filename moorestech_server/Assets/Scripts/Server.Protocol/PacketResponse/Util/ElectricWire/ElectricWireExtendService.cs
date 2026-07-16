using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.Construction;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Server.Protocol.PacketResponse.Util.ElectricWire
{
    /// <summary>
    /// レール式延長設置を実行。設置前に全検証し通過時のみ状態変更する
    /// Runs rail-style extend placement; validates before placing, mutates only on pass
    /// </summary>
    public static class ElectricWireExtendService
    {
        public static ExtendResult Execute(bool hasFromConnector, Vector3Int fromPos, PlaceInfoMessagePack polePlaceInfo, int playerId, BlockId poleBlockId, ItemId wireItemId)
        {
            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;

            // 設置先が既に埋まっていないか確認する
            // Ensure the target position is not already occupied
            if (ServerContext.WorldBlockDatastore.Exists(polePlaceInfo.Position))
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.PositionOccupied);

            // ブロックの解放状態を検証する（解放判定は基底ブロック）
            // Validate the unlock state (judged on the base block)
            var baseBlockGuid = MasterHolder.BlockMaster.GetBlockMaster(poleBlockId).BlockGuid;
            if (!ServerContext.GetService<IGameUnlockStateDataController>().BlockUnlockStateInfos[baseBlockGuid].IsUnlocked)
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.NotUnlocked);

            // 指定BlockIdから電柱パラメータを解決する
            // Resolve the pole parameter from the requested BlockId
            var blockId = poleBlockId;
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            if (blockMaster.BlockParam is not ElectricPoleBlockParam poleParam)
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.InvalidTarget);

            // 建設コストの充足を検証する
            // Validate the construction cost
            var costItemCounts = ConstructionCostService.ToItemCounts(blockMaster.RequiredItems);
            if (!ConstructionCostService.HasRequiredItems(costItemCounts, inventory.InventoryItems))
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.InsufficientItems);

            // 起点ありは明示接続＋機械収集、起点なしは通常設置と同じフル自動接続
            // With origin: explicit wire + machine collection; without: same full auto-connect as normal placement
            return hasFromConnector
                ? ExecuteExtendWithOrigin(inventory, fromPos, polePlaceInfo, blockId, poleParam, wireItemId, costItemCounts)
                : ExecuteIsolatedPlace(inventory, polePlaceInfo, blockId, costItemCounts);
        }

        // 起点との明示接続＋設置電柱の未接続機械収集をアトミックに行う
        // Atomically wire the origin plus collect unconnected machines around the placed pole
        private static ExtendResult ExecuteExtendWithOrigin(IOpenableInventory inventory, Vector3Int fromPos, PlaceInfoMessagePack polePlaceInfo, BlockId blockId, ElectricPoleBlockParam poleParam, ItemId wireItemId, (ItemId itemId, int count)[] costItemCounts)
        {
            // 起点コネクタを解決し、距離・上限・コストを検証する
            // Resolve the origin connector and validate distance, capacity and cost
            if (!ElectricWireSystemUtil.TryGetWireConnector(fromPos, out var fromConnector))
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.InvalidTarget);

            var distance = Vector3Int.Distance(fromPos, polePlaceInfo.Position);
            if (Mathf.Min(fromConnector.MaxWireLength, poleParam.MaxWireLength) < distance)
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.TooFar);
            if (fromConnector.IsWireConnectionFull)
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.ConnectionLimit);

            // 設置する電柱自身が1本も張れない設定なら失敗させる
            // Fail when the pole to be placed cannot hold even one wire
            if (poleParam.MaxWireConnectionCount < 1)
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.ConnectionLimit);
            if (!ElectricWirePlacementEvaluator.TryCalculateWireCost(wireItemId, distance, out var fromCost))
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.NoWireItem);

            var targets = new List<(BlockInstanceId TargetId, ElectricWireConnectionCost Cost)> { (fromConnector.BlockInstanceId, fromCost) };
            var totalWire = fromCost.Count;

            // 起点接続で1本使う前提で残り本数まで未接続機械を収集する
            // Collect unconnected machines up to remaining capacity, accounting for the origin wire slot
            var machineTargets = ElectricWireAutoConnectTargetCollector.CollectPoleMachineTargets(poleParam, polePlaceInfo.Position, 1);
            foreach (var machineTarget in machineTargets)
            {
                // 起点自身が未接続機械として再収集された場合は除外する（二重接続・二重計上の防止）
                // Skip the origin when re-collected as an unconnected machine, preventing double wiring and counting
                if (machineTarget.TargetId == fromConnector.BlockInstanceId) continue;
                if (!ElectricWirePlacementEvaluator.TryCalculateWireCost(wireItemId, machineTarget.Distance, out var cost))
                    return ExtendResult.Failure(ElectricWirePlacementFailureReason.NoWireItem);
                targets.Add((machineTarget.TargetId, cost));
                totalWire += cost.Count;
            }

            // 電線合計＋建設コスト中の同一アイテム分を合算で判定する
            // Judge by total wires plus the same-item amount reserved by the construction cost
            var reservedWire = 0;
            foreach (var (itemId, count) in costItemCounts)
            {
                if (itemId == wireItemId) reservedWire += count;
            }
            if (ElectricWireSystemUtil.CountItem(inventory, wireItemId) < totalWire + reservedWire)
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.NoWireItem);

            // 検証をすべて通過したのでここから状態を変更する
            // All validation passed; start mutating state from here
            if (!TryPlacePole(polePlaceInfo, blockId, out var selfConnector))
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.PositionOccupied);

            // 事前検証済みだが実行時ズレに備え、実際に張れた接続分の電線だけを消費する
            // Validated ahead, but to survive runtime drift we consume wires only for connections that actually succeeded
            var consumedWire = 0;
            foreach (var (targetId, cost) in targets)
            {
                var targetConnector = ServerContext.WorldBlockDatastore.GetBlock(targetId)?.GetComponent<IElectricWireConnector>();
                if (targetConnector == null) continue;
                if (!ElectricWireSystemUtil.TryConnectBothSides(selfConnector, targetConnector, cost)) continue;
                consumedWire += cost.Count;
            }

            // 費用消費後に再構築を予約する
            // Consume construction and wire costs, then mark topology for the next tick rebuild
            ConstructionCostService.ConsumeRequiredItems(costItemCounts, inventory);
            ElectricWireSystemUtil.ConsumeItem(inventory, wireItemId, consumedWire);

            return ExtendResult.Success(polePlaceInfo.Position, selfConnector.BlockInstanceId.AsPrimitive());
        }

        // 起点なし設置。通常のブロック設置と同じ自動接続（最寄り電柱1本＋未接続機械）を適用する
        // Placement without origin; applies the same auto-connect as normal placement (nearest pole + unconnected machines)
        private static ExtendResult ExecuteIsolatedPlace(IOpenableInventory inventory, PlaceInfoMessagePack polePlaceInfo, BlockId blockId, (ItemId itemId, int count)[] costItemCounts)
        {
            // 設置前に自動接続計画を検証する。建設コストは予約として渡し電線不足なら設置しない
            // Validate the auto-connect plan before placement; pass the construction cost as reservations and do not place when wires are insufficient
            var plan = ElectricWireAutoConnectService.EvaluateAutoConnect(blockId, polePlaceInfo.Position, polePlaceInfo.Direction, costItemCounts, inventory.InventoryItems);
            if (!plan.IsPlaceable)
                return ExtendResult.Failure(plan.FailureReason);

            if (!TryPlacePole(polePlaceInfo, blockId, out var selfConnector))
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.PositionOccupied);

            // 建設コストを消費し、検証済み計画でワイヤーを張って電線を消費する
            // Consume the construction cost, then execute the validated plan to add wires and consume wire items
            ConstructionCostService.ConsumeRequiredItems(costItemCounts, inventory);
            ElectricWireAutoConnectService.ExecuteAutoConnect(plan, ServerContext.WorldBlockDatastore.GetBlock(selfConnector.BlockInstanceId), inventory);

            return ExtendResult.Success(polePlaceInfo.Position, selfConnector.BlockInstanceId.AsPrimitive());
        }

        private static bool TryPlacePole(PlaceInfoMessagePack polePlaceInfo, BlockId blockId, out IElectricWireConnector selfConnector)
        {
            // ブロックを設置しワイヤー端点を解決する
            // Place the block and resolve its wire connector component
            selfConnector = null;
            var createParams = polePlaceInfo.BlockCreateParams.Select(v => new BlockCreateParam(v.Key, v.Value)).ToArray();
            if (!ServerContext.WorldBlockDatastore.TryAddBlock(blockId, polePlaceInfo.Position, polePlaceInfo.Direction, createParams, out var placedBlock)) return false;

            selfConnector = placedBlock.GetComponent<IElectricWireConnector>();
            return true;
        }

        public readonly struct ExtendResult
        {
            public readonly bool IsSuccess;
            public readonly ElectricWirePlacementFailureReason FailureReason;
            public readonly Vector3Int PlacedPolePos;
            public readonly int PlacedBlockInstanceId;

            private ExtendResult(bool isSuccess, ElectricWirePlacementFailureReason failureReason, Vector3Int placedPolePos, int placedBlockInstanceId)
            {
                IsSuccess = isSuccess;
                FailureReason = failureReason;
                PlacedPolePos = placedPolePos;
                PlacedBlockInstanceId = placedBlockInstanceId;
            }

            public static ExtendResult Success(Vector3Int placedPolePos, int placedBlockInstanceId)
            {
                return new ExtendResult(true, ElectricWirePlacementFailureReason.None, placedPolePos, placedBlockInstanceId);
            }

            public static ExtendResult Failure(ElectricWirePlacementFailureReason failureReason)
            {
                return new ExtendResult(false, failureReason, Vector3Int.zero, 0);
            }
        }
    }
}
