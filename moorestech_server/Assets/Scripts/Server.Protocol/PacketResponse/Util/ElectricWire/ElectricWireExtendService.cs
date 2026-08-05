using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.ConnectTool;
using Server.Protocol.PacketResponse.Util.Construction;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Server.Protocol.PacketResponse.Util.ElectricWire
{
    /// <summary>
    /// レール式延長設置・既存ブロック接続を実行。設置前に全検証し通過時のみ状態変更する
    /// Runs rail-style extend placement and existing-block connection; validates before placing, mutates only on pass
    /// </summary>
    public static class ElectricWireExtendService
    {
        public static ExtendResult Execute(ElectricWireExtendProtocol.ElectricWireExtendOperation operation, Vector3Int fromPos, Vector3Int toPos, PlaceInfoMessagePack polePlaceInfo, int playerId, BlockId poleBlockId, Guid connectToolGuid)
        {
            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;

            // 既存ブロック接続は設置系検証を通らず既存utilに委ねる
            // ConnectToExisting skips placement validations and delegates to the existing util
            if (operation == ElectricWireExtendProtocol.ElectricWireExtendOperation.ConnectToExisting)
                return ExecuteConnectToExisting();

            // 設置先が既に埋まっていないか確認する
            // Ensure the target position is not already occupied
            if (ServerContext.WorldBlockDatastore.Exists(polePlaceInfo.Position))
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.PositionOccupied);

            // ブロックの解放状態を検証する（解放判定は基底ブロック）
            // Validate the unlock state (judged on the base block)
            var baseBlockGuid = MasterHolder.BlockMaster.GetBlockMaster(poleBlockId).BlockGuid;
            if (!ServerContext.GetService<IGameUnlockStateDataController>().BlockUnlockStateInfos[baseBlockGuid].IsUnlocked)
                return ExtendResult.Failure(ElectricWirePlacementFailureReason.NotUnlocked);

            // 起点あり延長のみ未解放connectToolでの延長を拒否する
            // Only extend-with-origin rejects extension using a connectTool that is not unlocked
            if (operation == ElectricWireExtendProtocol.ElectricWireExtendOperation.ExtendToNewPole && !ElectricWireSystemUtil.IsConnectToolUnlocked(connectToolGuid))
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

            // 起点ありは起点との明示1本のみ、起点なしは接続なしの単体設置
            // With origin: only the explicit origin wire; without: place the pole alone with no wiring
            return operation == ElectricWireExtendProtocol.ElectricWireExtendOperation.ExtendToNewPole
                ? ExecuteExtendWithOrigin()
                : ExecuteIsolatedPlace();

            #region Internal

            // 既存ブロック同士を接続し、成功時は接続先を終点として返す
            // Connect two existing blocks; on success return the target as the endpoint
            ExtendResult ExecuteConnectToExisting()
            {
                if (!ElectricWireSystemUtil.TryConnect(fromPos, toPos, playerId, connectToolGuid, out var failureReason))
                    return ExtendResult.Failure(failureReason);

                // TryConnect成功直後なので終点コネクタは必ず解決できる
                // The endpoint connector always resolves right after a successful TryConnect
                ElectricWireSystemUtil.TryGetWireConnector(toPos, out var toConnector);
                return ExtendResult.Success(toPos, toConnector.BlockInstanceId.AsPrimitive());
            }

            // 起点との明示接続をアトミックに行う
            // Atomically wire the origin
            ExtendResult ExecuteExtendWithOrigin()
            {
                // 起点コネクタを解決し、距離・上限・コストを検証する
                // Resolve the origin connector and validate distance, capacity and cost
                if (!ElectricWireSystemUtil.TryGetWireConnector(fromPos, out var fromConnector))
                    return ExtendResult.Failure(ElectricWirePlacementFailureReason.InvalidTarget);

                var poleGhostInfo = new BlockPositionInfo(polePlaceInfo.Position, polePlaceInfo.Direction, blockMaster.BlockSize);

                // 起点と新設電柱の相互範囲判定を行う。距離はコスト計算専用に残す
                // Mutual range check between origin and the new pole; distance remains for cost only
                var fromBlock = ServerContext.WorldBlockDatastore.GetBlock(fromConnector.BlockInstanceId);
                if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(fromBlock.BlockMasterElement.BlockParam, out _, out var fromProfile, out var fromIsPole))
                    return ExtendResult.Failure(ElectricWirePlacementFailureReason.InvalidTarget);
                if (!ElectricConnectionRangeService.IsMutuallyConnectable(fromBlock.BlockPositionInfo, fromProfile, fromIsPole, poleGhostInfo, ConnectionRangeProfile.CreatePole(poleParam), true))
                    return ExtendResult.Failure(ElectricWirePlacementFailureReason.OutOfRange);
                var distance = Vector3Int.Distance(fromPos, polePlaceInfo.Position);
                if (fromConnector.IsWireConnectionFull)
                    return ExtendResult.Failure(ElectricWirePlacementFailureReason.ConnectionLimit);

                // 設置する電柱自身が1本も張れない設定なら失敗させる
                // Fail when the pole to be placed cannot hold even one wire
                if (poleParam.MaxWireConnectionCount < 1)
                    return ExtendResult.Failure(ElectricWirePlacementFailureReason.ConnectionLimit);
                if (!ElectricWirePlacementEvaluator.TryCalculateWireCost(connectToolGuid, distance, out var fromCost))
                    return ExtendResult.Failure(ElectricWirePlacementFailureReason.NoWireItem);

                // 素材ごとの必要総数を集計する。まず起点接続分
                // Aggregate required totals per material; start with the origin connection
                var targets = new List<(BlockInstanceId TargetId, ElectricWireConnectionCost Cost)> { (fromConnector.BlockInstanceId, fromCost) };
                var requiredByItem = new Dictionary<ItemId, int>();
                AddMaterials(requiredByItem, fromCost);

                // 電線素材合計＋建設コスト中の同一アイテム分を合算で判定する
                // Judge by total wire materials plus the same-item amount reserved by the construction cost
                foreach (var (itemId, required) in requiredByItem)
                {
                    var reserved = 0;
                    foreach (var (costItemId, count) in costItemCounts)
                    {
                        if (costItemId == itemId) reserved += count;
                    }
                    if (ElectricWireSystemUtil.CountItem(inventory, itemId) < required + reserved)
                        return ExtendResult.Failure(ElectricWirePlacementFailureReason.NoWireItem);
                }

                // 検証をすべて通過したのでここから状態を変更する
                // All validation passed; start mutating state from here
                if (!TryPlacePole(polePlaceInfo, blockId, out var selfConnector))
                    return ExtendResult.Failure(ElectricWirePlacementFailureReason.PositionOccupied);

                // 事前検証済みだが実行時ズレに備え、実際に張れた接続分の素材だけを消費する
                // Validated ahead, but to survive runtime drift we consume materials only for connections that actually succeeded
                foreach (var (targetId, cost) in targets)
                {
                    var targetConnector = ServerContext.WorldBlockDatastore.GetBlock(targetId)?.GetComponent<IElectricWireConnector>();
                    if (targetConnector == null) continue;
                    if (!ElectricWireSystemUtil.TryConnectBothSides(selfConnector, targetConnector, cost)) continue;
                    ConnectToolMaterialConsumer.Consume(cost.Materials, inventory);
                }

                // 建設コストを消費する（dirty化は接続処理内で行われる）
                // Consume the construction cost; the connection mutation itself marks the topology dirty
                ConstructionCostService.ConsumeRequiredItems(costItemCounts, inventory);

                return ExtendResult.Success(polePlaceInfo.Position, selfConnector.BlockInstanceId.AsPrimitive());

                void AddMaterials(Dictionary<ItemId, int> accumulator, ElectricWireConnectionCost cost)
                {
                    // 接続コストの各素材を必要総数へ加算する
                    // Add each material of a connection cost to the running required totals
                    if (cost.Materials == null) return;
                    foreach (var material in cost.Materials)
                    {
                        accumulator.TryGetValue(material.ItemId, out var current);
                        accumulator[material.ItemId] = current + material.Count;
                    }
                }
            }

            // 起点なし設置。自動接続は行わず電柱単体のみを設置する
            // Placement without origin; place the pole alone with no auto-connect
            ExtendResult ExecuteIsolatedPlace()
            {
                if (!TryPlacePole(polePlaceInfo, blockId, out var selfConnector))
                    return ExtendResult.Failure(ElectricWirePlacementFailureReason.PositionOccupied);

                // 建設コストのみ消費する
                // Consume only the construction cost
                ConstructionCostService.ConsumeRequiredItems(costItemCounts, inventory);

                return ExtendResult.Success(polePlaceInfo.Position, selfConnector.BlockInstanceId.AsPrimitive());
            }

            #endregion
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
    }
}
