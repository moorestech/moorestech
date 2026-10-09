using System;
using System.Collections.Generic;
using System.Linq;
using Common.Debug;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.PlacementTarget;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.Notification;
using Server.Protocol.PacketResponse.Util.Construction;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// BlockId指定と建設コストで複数セル設置
    /// Places blocks across multiple cells by direct BlockId with construction-cost consumption
    /// </summary>
    public class PlaceBlockProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:placeBlock";

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;
        private readonly NotificationService _notificationService;
        private readonly BlockCellPlacementExecutor _cellPlacementExecutor;
        private readonly PlacementTargetCatalog _placementTargetCatalog;

        public PlaceBlockProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _gameUnlockStateDataController = serviceProvider.GetService<IGameUnlockStateDataController>();
            _notificationService = serviceProvider.GetService<NotificationService>();
            _cellPlacementExecutor = new BlockCellPlacementExecutor(serviceProvider.GetService<ConstructionWalletService>());
            _placementTargetCatalog = serviceProvider.GetService<PlacementTargetCatalog>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            var data = MessagePackSerializer.Deserialize<SendPlaceBlockProtocolMessagePack>(payload);
            var inventoryData = _playerInventoryDataStore.GetInventoryData(requesterPlayerId);

            // デバッグ: ブロック設置無料化トグル（設置ごとのファイルIOを避け一度だけ読む）
            // Debug: free block placement toggle (read once to avoid per-cell file IO)
            var isFreePlacement = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);

            // スキップ理由を集約し末尾で通知
            // Aggregate skip reasons and notify at the end
            var notUnlockedCount = 0;
            var costShortageCount = 0;
            var wireShortageCount = 0;
            var restoreFailedCount = 0;

            foreach (var placeInfo in data.PlacePositions)
            {
                PlaceBlock(placeInfo);
            }

            // ドラッグ設置でセル数分に増幅させないため、財布の変更通知は最後に1通へ集約する
            // Collapse the wallet notifications into one at the very end so a drag never amplifies them per cell
            _cellPlacementExecutor.FlushRemainingCountChanges();

            if (0 < notUnlockedCount) _notificationService.Notify(requesterPlayerId, NotificationMessagePack.CreateOperationDenied("denied.placeBlockNotUnlocked", Array.Empty<string>()));
            if (0 < costShortageCount) _notificationService.Notify(requesterPlayerId, NotificationMessagePack.CreateOperationDenied("denied.placeBlockCostShortage", Array.Empty<string>()));
            if (0 < wireShortageCount) _notificationService.Notify(requesterPlayerId, NotificationMessagePack.CreateOperationDenied("denied.placeBlockWireShortage", Array.Empty<string>()));
            if (0 < restoreFailedCount) _notificationService.Notify(requesterPlayerId, NotificationMessagePack.CreateOperationDenied("denied.undoRestoreSkipped", new[] { restoreFailedCount.ToString() }));

            return null;

            #region Internal

            void PlaceBlock(PlaceInfoMessagePack placeInfo)
            {
                // すでにブロックがある場合は何もしない。復元なら失敗として数える
                // Do nothing when a block already exists; a restoration counts it as a failure
                if (ServerContext.WorldBlockDatastore.Exists(placeInfo.Position)) { CountRestoreFailure(placeInfo, "position occupied"); return; }

                var placeBlockId = placeInfo.BlockId;
                var createParams = placeInfo.BlockCreateParams.Select(v => new BlockCreateParam(v.Key, v.Value)).ToArray();

                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(placeBlockId);

                // 未解放セルはスキップ（無料設置は解放・コスト・電線素材の判定と支払いのみ免除し設置と配線は通常どおり）。解放判定はカタログへ集約
                // Skip locked cells (free placement waives only unlock/cost/wire-material checks and payments; placing and wiring run as usual); the unlock rule lives in the catalog
                if (!_placementTargetCatalog.IsBlockUnlocked(blockMaster.BlockGuid, _gameUnlockStateDataController, isFreePlacement)) { notUnlockedCount++; LogRestoreSkip(placeInfo, "not unlocked"); return; }

                // 財布に問い合わせ、賄えないセルはスキップ
                // Ask the wallet; skip cells it cannot cover
                var inventory = inventoryData.MainOpenableInventory;
                var cellPlacement = _cellPlacementExecutor.PlanCell(placeBlockId, requesterPlayerId, inventory, isFreePlacement);
                if (!cellPlacement.IsAffordable) { costShortageCount++; LogRestoreSkip(placeInfo, "construction cost shortage"); return; }

                // 自動接続の電気ブロックだけ事前検証
                // Validate wiring only for auto-connect electric blocks
                var isAutoConnectElectric = data.Wiring == BlockPlacementWiring.AutoConnect && ElectricWireBlockParamResolver.TryGetWireRangeParam(blockMaster.BlockParam, out _, out _, out _);
                var plan = default(ElectricWireAutoConnectPlan);
                if (isAutoConnectElectric)
                {
                    // 建設コストで消費予定の素材を予約として渡し、電線の所持数判定から除外する
                    // Pass construction-cost materials as reservations to exclude them from wire availability
                    plan = ElectricWireAutoConnectService.EvaluateAutoConnect(placeBlockId, placeInfo.Position, placeInfo.Direction, cellPlacement.ItemsToConsume, inventory.InventoryItems, isFreePlacement);
                    if (!plan.IsPlaceable) { wireShortageCount++; LogRestoreSkip(placeInfo, "wire shortage"); return; }
                }

                // 設置に失敗した場合はコストを消費しない
                // Do not consume the cost when placement fails
                if (!_cellPlacementExecutor.TryPlaceCell(cellPlacement, placeBlockId, placeInfo.Position, placeInfo.Direction, createParams, inventory, out var block)) { CountRestoreFailure(placeInfo, "TryAddBlock failed"); return; }

                // 計画を実行しワイヤー消費
                // Execute the validated plan: add wires and consume wire items
                if (isAutoConnectElectric) ElectricWireAutoConnectService.ExecuteAutoConnect(plan, block, inventory);
            }

            // 復元（Undo）でスキップしたセルの理由を残す。通知は解放・不足それぞれのキーで出る
            // Log why a restoration (undo) skipped a cell; the notification goes out under the unlock/shortage keys
            void LogRestoreSkip(PlaceInfoMessagePack placeInfo, string reason)
            {
                if (data.Wiring != BlockPlacementWiring.NoAutoConnect) return;
                UnityEngine.Debug.LogWarning($"[PlaceBlock] restore skipped: {reason} pos={(UnityEngine.Vector3Int)placeInfo.Position} block={placeInfo.BlockId} player={requesterPlayerId}");
            }

            // 復元（Undo）の再設置失敗は黙って捨てず、ログを残して末尾で件数を通知する
            // A restoration (undo) re-place failure is not dropped silently: log it and notify the count at the end
            void CountRestoreFailure(PlaceInfoMessagePack placeInfo, string reason)
            {
                if (data.Wiring != BlockPlacementWiring.NoAutoConnect) return;
                restoreFailedCount++;
                UnityEngine.Debug.LogWarning($"[PlaceBlock] restore failed: {reason} pos={(UnityEngine.Vector3Int)placeInfo.Position} block={placeInfo.BlockId} player={requesterPlayerId}");
            }

            #endregion
        }

        [MessagePackObject]
        public class SendPlaceBlockProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(3)] public List<PlaceInfoMessagePack> PlacePositions { get; set; }
            [Key(4)] public BlockPlacementWiring Wiring { get; set; }

            public SendPlaceBlockProtocolMessagePack(List<PlaceInfo> placeInfos, BlockPlacementWiring wiring)
            {
                Tag = ProtocolTag;
                PlacePositions = placeInfos.ConvertAll(v => new PlaceInfoMessagePack(v));
                Wiring = wiring;
            }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SendPlaceBlockProtocolMessagePack() { }
        }
    }
}
