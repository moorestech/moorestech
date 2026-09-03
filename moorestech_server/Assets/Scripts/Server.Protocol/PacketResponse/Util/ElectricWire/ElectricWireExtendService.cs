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
        public static ElectricWireExtendResult Execute(ElectricWireExtendOperation operation, Vector3Int fromPos, Vector3Int toPos, PlaceInfoMessagePack polePlaceInfo, int playerId, BlockId poleBlockId, Guid connectToolGuid)
        {
            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;

            // 設置系検証が確定させる共有値。TryValidatePolePlacement通過後のみ有効
            // Shared values settled by the placement validation; valid only after TryValidatePolePlacement passes
            BlockMasterElement blockMaster = null;
            ElectricPoleBlockParam poleParam = null;
            IConstructionPlacementPlan placementPlan = null;
            IReadOnlyList<(ItemId itemId, int count)> costItemCounts = null;
            var constructionWallet = ServerContext.GetService<ConstructionWalletService>();

            // Operationごとの経路をこの1箇所で振り分ける。外部入力由来の列挙域外は不正Modeとして拒否する
            // All per-operation branching lives here; out-of-range values from external input are rejected as an invalid mode
            switch (operation)
            {
                case ElectricWireExtendOperation.ConnectToExisting:
                    return ExecuteConnectToExisting();
                case ElectricWireExtendOperation.ExtendToNewPole:
                    return ExecuteExtendWithOrigin();
                case ElectricWireExtendOperation.PlaceIsolatedPole:
                    return ExecuteIsolatedPlace();
                default:
                    return ElectricWireExtendResult.Failure(ElectricWirePlacementFailureReason.InvalidMode);
            }

            #region Internal

            // 既存ブロック同士を接続し、成功時は接続先を終点として返す
            // Connect two existing blocks; on success return the target as the endpoint
            ElectricWireExtendResult ExecuteConnectToExisting()
            {
                if (!ElectricWireSystemUtil.TryConnect(fromPos, toPos, playerId, connectToolGuid, out var failureReason))
                    return ElectricWireExtendResult.Failure(failureReason);

                // TryConnect成功直後なので終点コネクタは必ず解決できる
                // The endpoint connector always resolves right after a successful TryConnect
                ElectricWireSystemUtil.TryGetWireConnector(toPos, out var toConnector);
                return ElectricWireExtendResult.Success(toPos, toConnector.BlockInstanceId.AsPrimitive());
            }

            // 設置系2Operationに共通する事前検証。通過時のみ共有値が確定する
            // Pre-validation shared by both placement operations; the shared values are settled only on pass
            bool TryValidatePolePlacement(out ElectricWirePlacementFailureReason failureReason)
            {
                // 設置先が既に埋まっていないか確認する
                // Ensure the target position is not already occupied
                failureReason = ElectricWirePlacementFailureReason.PositionOccupied;
                if (ServerContext.WorldBlockDatastore.Exists(polePlaceInfo.Position)) return false;

                // ブロックの解放状態を検証する（解放判定は基底ブロック）
                // Validate the unlock state (judged on the base block)
                blockMaster = MasterHolder.BlockMaster.GetBlockMaster(poleBlockId);
                failureReason = ElectricWirePlacementFailureReason.NotUnlocked;
                if (!ServerContext.GetService<IGameUnlockStateDataController>().BlockUnlockStateInfos[blockMaster.BlockGuid].IsUnlocked) return false;

                // 指定BlockIdから電柱パラメータを解決する
                // Resolve the pole parameter from the requested BlockId
                failureReason = ElectricWirePlacementFailureReason.InvalidTarget;
                if (blockMaster.BlockParam is not ElectricPoleBlockParam requestedPoleParam) return false;
                poleParam = requestedPoleParam;

                // 建設コストは財布に問い合わせる。残りで賄えるセルは素材を要求しない
                // Ask the wallet for the construction cost; a cell covered by the remainder demands no materials
                placementPlan = constructionWallet.PlanPlacement(blockMaster, playerId);
                costItemCounts = placementPlan.ItemsToConsume;
                failureReason = ElectricWirePlacementFailureReason.InsufficientItems;
                if (!ConstructionCostService.HasRequiredItems(costItemCounts, inventory.InventoryItems)) return false;

                failureReason = ElectricWirePlacementFailureReason.None;
                return true;
            }

            // 起点との明示接続1本をアトミックに行う
            // Atomically wire the single explicit connection to the origin
            ElectricWireExtendResult ExecuteExtendWithOrigin()
            {
                if (!TryValidatePolePlacement(out var placementFailure)) return ElectricWireExtendResult.Failure(placementFailure);

                // 起点あり延長のみ未解放connectToolでの延長を拒否する
                // Only extend-with-origin rejects extension using a connectTool that is not unlocked
                if (!ElectricWireSystemUtil.IsConnectToolUnlocked(connectToolGuid))
                    return ElectricWireExtendResult.Failure(ElectricWirePlacementFailureReason.NotUnlocked);

                // 起点コネクタを解決し、距離・上限・コストを検証する
                // Resolve the origin connector and validate distance, capacity and cost
                if (!ElectricWireSystemUtil.TryGetWireConnector(fromPos, out var fromConnector))
                    return ElectricWireExtendResult.Failure(ElectricWirePlacementFailureReason.InvalidTarget);

                var poleGhostInfo = new BlockPositionInfo(polePlaceInfo.Position, polePlaceInfo.Direction, blockMaster.BlockSize);

                // 起点と新設電柱の相互範囲判定を行う。距離はコスト計算専用に残す
                // Mutual range check between origin and the new pole; distance remains for cost only
                var fromBlock = ServerContext.WorldBlockDatastore.GetBlock(fromConnector.BlockInstanceId);
                if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(fromBlock.BlockMasterElement.BlockParam, out _, out var fromProfile, out var fromIsPole))
                    return ElectricWireExtendResult.Failure(ElectricWirePlacementFailureReason.InvalidTarget);
                if (!ElectricConnectionRangeService.IsMutuallyConnectable(fromBlock.BlockPositionInfo, fromProfile, fromIsPole, poleGhostInfo, ConnectionRangeProfile.CreatePole(poleParam), true))
                    return ElectricWireExtendResult.Failure(ElectricWirePlacementFailureReason.OutOfRange);
                var distance = Vector3Int.Distance(fromPos, polePlaceInfo.Position);
                if (fromConnector.IsWireConnectionFull)
                    return ElectricWireExtendResult.Failure(ElectricWirePlacementFailureReason.ConnectionLimit);

                // 設置する電柱自身が1本も張れない設定なら失敗させる
                // Fail when the pole to be placed cannot hold even one wire
                if (poleParam.MaxWireConnectionCount < 1)
                    return ElectricWireExtendResult.Failure(ElectricWirePlacementFailureReason.ConnectionLimit);
                if (!ElectricWirePlacementEvaluator.TryCalculateWireCost(connectToolGuid, distance, out var wireCost))
                    return ElectricWireExtendResult.Failure(ElectricWirePlacementFailureReason.NoWireItem);
                // 建設コストの予約分を上乗せした所持判定は共有の正本へ委ねる
                // The held check with the construction cost reserved on top is delegated to the shared definition
                if (!ConnectToolMaterialConsumer.HasEnough(wireCost.Materials, inventory.InventoryItems, ConnectToolMaterialConsumer.ToMaterials(costItemCounts)))
                    return ElectricWireExtendResult.Failure(ElectricWirePlacementFailureReason.NoWireItem);

                // 検証をすべて通過したのでここから状態を変更する
                // All validation passed; start mutating state from here
                if (!TryPlacePole(polePlaceInfo, poleBlockId, out var selfConnector))
                    return ElectricWireExtendResult.Failure(ElectricWirePlacementFailureReason.PositionOccupied);

                // 起点1本が張れなければ配線なしの成功で潰さず失敗として返す（素材も建設コストも消費しない）
                // If the single origin wire cannot be strung, report failure instead of a wireless success; nothing is consumed
                if (!ElectricWireSystemUtil.TryConnectBothSides(selfConnector, fromConnector, wireCost))
                {
                    // 事前検証済みのため通常到達しないが、孤立電柱を残さないよう設置を取り消す（前例: GearChainPoleExtendProtocol）
                    // Unreachable after pre-validation; remove the block to avoid leaving an orphan pole (precedent: GearChainPoleExtendProtocol)
                    ServerContext.WorldBlockDatastore.RemoveBlock(polePlaceInfo.Position, BlockRemoveReason.ManualRemove);
                    return ElectricWireExtendResult.Failure(ElectricWirePlacementFailureReason.ConnectionLimit);
                }

                // 電線素材と建設コストを消費する（dirty化は接続処理内で行われる）
                // Consume the wire materials and the construction cost; the connection mutation itself marks the topology dirty
                ConnectToolMaterialConsumer.Consume(wireCost.Materials, inventory);
                constructionWallet.CommitPlacement(placementPlan, inventory, selfConnector.BlockInstanceId);
                constructionWallet.FlushRemainingCountChanges();

                return ElectricWireExtendResult.Success(polePlaceInfo.Position, selfConnector.BlockInstanceId.AsPrimitive());
            }

            // 起点なし設置。自動接続は行わず電柱単体のみを設置する
            // Placement without origin; place the pole alone with no auto-connect
            ElectricWireExtendResult ExecuteIsolatedPlace()
            {
                if (!TryValidatePolePlacement(out var placementFailure)) return ElectricWireExtendResult.Failure(placementFailure);

                if (!TryPlacePole(polePlaceInfo, poleBlockId, out var selfConnector))
                    return ElectricWireExtendResult.Failure(ElectricWirePlacementFailureReason.PositionOccupied);

                // 建設コストのみ消費する
                // Consume only the construction cost
                constructionWallet.CommitPlacement(placementPlan, inventory, selfConnector.BlockInstanceId);
                constructionWallet.FlushRemainingCountChanges();

                return ElectricWireExtendResult.Success(polePlaceInfo.Position, selfConnector.BlockInstanceId.AsPrimitive());
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
