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
        private readonly ConstructionWalletService _constructionWallet;
        private readonly PlacementTargetCatalog _placementTargetCatalog;

        public PlaceBlockProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _gameUnlockStateDataController = serviceProvider.GetService<IGameUnlockStateDataController>();
            _notificationService = serviceProvider.GetService<NotificationService>();
            _constructionWallet = serviceProvider.GetService<ConstructionWalletService>();
            _placementTargetCatalog = serviceProvider.GetService<PlacementTargetCatalog>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<SendPlaceBlockProtocolMessagePack>(payload);
            var inventoryData = _playerInventoryDataStore.GetInventoryData(data.PlayerId);

            // デバッグ: ブロック設置無料化トグル（設置ごとのファイルIOを避け一度だけ読む）
            // Debug: free block placement toggle (read once to avoid per-cell file IO)
            var isFreePlacement = DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);

            // スキップ理由を集約し末尾で通知
            // Aggregate skip reasons and notify at the end
            var notUnlockedCount = 0;
            var costShortageCount = 0;
            var wireShortageCount = 0;

            foreach (var placeInfo in data.PlacePositions)
            {
                PlaceBlock(placeInfo);
            }

            // ドラッグ設置でセル数分に増幅させないため、財布の変更通知は最後に1通へ集約する
            // Collapse the wallet notifications into one at the very end so a drag never amplifies them per cell
            _constructionWallet.FlushRemainingCountChanges();

            if (0 < notUnlockedCount) _notificationService.Notify(data.PlayerId, NotificationMessagePack.CreateOperationDenied("denied.placeBlockNotUnlocked", Array.Empty<string>()));
            if (0 < costShortageCount) _notificationService.Notify(data.PlayerId, NotificationMessagePack.CreateOperationDenied("denied.placeBlockCostShortage", Array.Empty<string>()));
            if (0 < wireShortageCount) _notificationService.Notify(data.PlayerId, NotificationMessagePack.CreateOperationDenied("denied.placeBlockWireShortage", Array.Empty<string>()));

            return null;

            #region Internal

            void PlaceBlock(PlaceInfoMessagePack placeInfo)
            {
                // すでにブロックがある場合は何もしない
                // Do nothing when a block already exists
                if (ServerContext.WorldBlockDatastore.Exists(placeInfo.Position)) return;

                var placeBlockId = placeInfo.BlockId;
                var createParams = placeInfo.BlockCreateParams.Select(v => new BlockCreateParam(v.Key, v.Value)).ToArray();

                // 無料設置は解放・コスト無視で強制設置
                // Free placement force-places ignoring unlock/cost
                if (isFreePlacement)
                {
                    PlaceForFree(placeBlockId, placeInfo, createParams);
                    return;
                }

                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(placeBlockId);

                // 未解放セルはスキップ。坂ベルトの正規化を含む解放判定はカタログへ集約している
                // Skip locked cells; the unlock rule, belt-slope normalization included, lives in the catalog
                // 無料設置は上の早期returnで完結済みなので、ここへ到達する時点で無料設置ではない
                // Free placement already returned above, so reaching here means placement is never free
                if (!_placementTargetCatalog.IsBlockUnlocked(blockMaster.BlockGuid, _gameUnlockStateDataController, false)) { notUnlockedCount++; return; }

                // 財布に問い合わせ、賄えないセルはスキップ
                // Ask the wallet; skip cells it cannot cover
                var inventory = inventoryData.MainOpenableInventory;
                var placementPlan = _constructionWallet.PlanPlacement(blockMaster, data.PlayerId);
                if (!ConstructionCostService.HasRequiredItems(placementPlan.ItemsToConsume, inventory.InventoryItems)) { costShortageCount++; return; }

                // 電気なら自動接続を事前検証
                // For electric blocks, validate the auto-connect plan before placement; skip when wires are insufficient
                var isElectric = ElectricWireBlockParamResolver.TryGetWireRangeParam(blockMaster.BlockParam, out _, out _, out _);
                var plan = default(ElectricWireAutoConnectPlan);
                if (isElectric)
                {
                    // 建設コストで消費予定の素材を予約として渡し、電線の所持数判定から除外する
                    // Pass construction-cost materials as reservations to exclude them from wire availability
                    plan = ElectricWireAutoConnectService.EvaluateAutoConnect(placeBlockId, placeInfo.Position, placeInfo.Direction, placementPlan.ItemsToConsume, inventory.InventoryItems, false);
                    if (!plan.IsPlaceable) { wireShortageCount++; return; }
                }

                // 設置に失敗した場合はコストを消費しない
                // Do not consume the cost when placement fails
                if (!ServerContext.WorldBlockDatastore.TryAddBlock(placeBlockId, placeInfo.Position, placeInfo.Direction, createParams, out var block)) return;

                _constructionWallet.CommitPlacement(placementPlan, inventory, block.BlockInstanceId);

                // 計画を実行しワイヤー消費
                // Execute the validated plan: add wires and consume wire items
                if (isElectric) ElectricWireAutoConnectService.ExecuteAutoConnect(plan, block, inventory);
            }

            void PlaceForFree(BlockId blockId, PlaceInfoMessagePack placeInfo, BlockCreateParam[] createParams)
            {
                // 通常経路と同じ順序で「設置前に計画→設置→実行」する。予約は無く所持数も見ない
                // Same order as the normal path: plan before placing, place, then execute; no reservation and no held-count check
                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
                var isElectric = ElectricWireBlockParamResolver.TryGetWireRangeParam(blockMaster.BlockParam, out _, out _, out _);
                var inventory = inventoryData.MainOpenableInventory;
                var plan = isElectric
                    ? ElectricWireAutoConnectService.EvaluateAutoConnect(blockId, placeInfo.Position, placeInfo.Direction, Array.Empty<(ItemId itemId, int count)>(), inventory.InventoryItems, true)
                    : default;

                if (!ServerContext.WorldBlockDatastore.TryAddBlock(blockId, placeInfo.Position, placeInfo.Direction, createParams, out var block)) return;

                // 計画がFailureでも設置は行い、接続は計画が成立したときだけ実行する
                // Placement proceeds even on a Failure plan; the connection runs only when the plan is placeable
                if (isElectric && plan.IsPlaceable) ElectricWireAutoConnectService.ExecuteAutoConnect(plan, block, inventory);
            }

            #endregion
        }

        [MessagePackObject]
        public class SendPlaceBlockProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public List<PlaceInfoMessagePack> PlacePositions { get; set; }

            public SendPlaceBlockProtocolMessagePack(int playerId, List<PlaceInfo> placeInfos)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                PlacePositions = placeInfos.ConvertAll(v => new PlaceInfoMessagePack(v));
            }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SendPlaceBlockProtocolMessagePack() { }
        }
    }
}
