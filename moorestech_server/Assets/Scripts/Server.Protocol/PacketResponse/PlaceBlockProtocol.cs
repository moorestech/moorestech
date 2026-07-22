using System;
using System.Collections.Generic;
using System.Linq;
using Common.Debug;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
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
        private readonly BlockReplaceService _blockReplaceService;
        private readonly NotificationService _notificationService;

        public PlaceBlockProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _gameUnlockStateDataController = serviceProvider.GetService<IGameUnlockStateDataController>();
            _blockReplaceService = new BlockReplaceService(_gameUnlockStateDataController);
            _notificationService = serviceProvider.GetService<NotificationService>();
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

            if (0 < notUnlockedCount) _notificationService.Notify(data.PlayerId, NotificationMessagePack.CreateOperationDenied("denied.placeBlockNotUnlocked", Array.Empty<string>()));
            if (0 < costShortageCount) _notificationService.Notify(data.PlayerId, NotificationMessagePack.CreateOperationDenied("denied.placeBlockCostShortage", Array.Empty<string>()));
            if (0 < wireShortageCount) _notificationService.Notify(data.PlayerId, NotificationMessagePack.CreateOperationDenied("denied.placeBlockWireShortage", Array.Empty<string>()));

            return null;

            #region Internal

            void PlaceBlock(PlaceInfoMessagePack placeInfo)
            {
                // リプレース指定セルはリプレースサービスへ委譲、それ以外は既存ブロックがあれば何もしない
                // Delegate replace-flagged cells to the replace service; otherwise skip occupied cells
                if (placeInfo.IsReplace)
                {
                    _blockReplaceService.TryReplaceBlock(placeInfo, inventoryData.MainOpenableInventory, isFreePlacement);
                    return;
                }
                if (ServerContext.WorldBlockDatastore.Exists(placeInfo.Position)) return;

                var placeBlockId = placeInfo.BlockId;
                var createParams = placeInfo.BlockCreateParams.Select(v => new BlockCreateParam(v.Key, v.Value)).ToArray();

                // 無料設置デバッグ: 解放・コスト・電線を一切見ず強制設置して即return
                // Free placement debug: force-place ignoring unlock/cost/wire entirely, then return
                if (isFreePlacement)
                {
                    ServerContext.WorldBlockDatastore.TryAddBlock(placeBlockId, placeInfo.Position, placeInfo.Direction, createParams, out _);
                    return;
                }

                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(placeBlockId);

                // 未解放セルはスキップし、ベルトの坂はファミリー直線の状態で判定する
                // Skip locked cells and resolve belt slopes through their family straight block
                if (!IsUnlocked(placeBlockId, blockMaster.BlockGuid)) { notUnlockedCount++; return; }

                // コスト不足セルはスキップ
                // Skip cells whose construction cost cannot be covered
                var inventory = inventoryData.MainOpenableInventory;
                var costItemCounts = ConstructionCostService.ToItemCounts(blockMaster.RequiredItems);
                if (!ConstructionCostService.HasRequiredItems(costItemCounts, inventory.InventoryItems)) { costShortageCount++; return; }

                // 電気なら自動接続を事前検証
                // For electric blocks, validate the auto-connect plan before placement; skip when wires are insufficient
                var isElectric = ElectricWireBlockParamResolver.TryGetWireParam(blockMaster.BlockParam, out _, out _);
                var plan = default(ElectricWireAutoConnectPlan);
                if (isElectric)
                {
                    // 建設コストで消費予定の素材を予約として渡し、電線の所持数判定から除外する
                    // Pass construction-cost materials as reservations to exclude them from wire availability
                    plan = ElectricWireAutoConnectService.EvaluateAutoConnect(placeBlockId, placeInfo.Position, placeInfo.Direction, costItemCounts, inventory.InventoryItems);
                    if (!plan.IsPlaceable) { wireShortageCount++; return; }
                }

                // 設置に失敗した場合はコストを消費しない
                // Do not consume the cost when placement fails
                if (!ServerContext.WorldBlockDatastore.TryAddBlock(placeBlockId, placeInfo.Position, placeInfo.Direction, createParams, out var block)) return;

                ConstructionCostService.ConsumeRequiredItems(costItemCounts, inventory);

                // 計画を実行しワイヤー消費
                // Execute the validated plan: add wires and consume wire items
                if (isElectric) ElectricWireAutoConnectService.ExecuteAutoConnect(plan, block, inventory);
            }

            bool IsUnlocked(BlockId blockId, Guid blockGuid)
            {
                // unlock判定はBlockReplaceServiceと共通化（ベルトファミリーは直線ブロック参照）
                // Unlock judgement shared with BlockReplaceService (belt families resolve through their straight block)
                return BlockReplaceService.IsUnlocked(blockId, blockGuid, _gameUnlockStateDataController);
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
