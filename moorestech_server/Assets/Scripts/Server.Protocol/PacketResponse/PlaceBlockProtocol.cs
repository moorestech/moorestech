using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.Construction;
using Server.Protocol.PacketResponse.Util.ElectricWire;

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

        public PlaceBlockProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _gameUnlockStateDataController = serviceProvider.GetService<IGameUnlockStateDataController>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<SendPlaceBlockProtocolMessagePack>(payload);
            var inventoryData = _playerInventoryDataStore.GetInventoryData(data.PlayerId);

            // 未解放ブロックは全セルの設置を拒否する
            // Reject all cells when the block is locked
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(data.BlockId).BlockGuid;
            if (!_gameUnlockStateDataController.BlockUnlockStateInfos[blockGuid].IsUnlocked) return null;

            foreach (var placeInfo in data.PlacePositions)
            {
                PlaceBlock(placeInfo);
            }

            return null;

            #region Internal

            void PlaceBlock(PlaceInfoMessagePack placeInfo)
            {
                // すでにブロックがある場合は何もしない
                // Do nothing when a block already exists
                if (ServerContext.WorldBlockDatastore.Exists(placeInfo.Position)) return;

                var placeBlockId = data.BlockId.GetVerticalOverrideBlockId(placeInfo.VerticalDirection);
                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(placeBlockId);
                var createParams = placeInfo.BlockCreateParams.Select(v => new BlockCreateParam(v.Key, v.Value)).ToArray();

                // コスト不足セルはスキップ
                // Skip cells whose construction cost cannot be covered (place only what is affordable)
                var inventory = inventoryData.MainOpenableInventory;
                if (!ConstructionCostService.HasRequiredItems(blockMaster.RequiredItems, inventory.InventoryItems)) return;

                // 電気なら自動接続を事前検証
                // For electric blocks, validate the auto-connect plan before placement; skip when wires are insufficient
                var isElectric = ElectricWireBlockParamResolver.TryGetWireParam(blockMaster.BlockParam, out _, out _);
                var plan = default(ElectricWireAutoConnectPlan);
                if (isElectric)
                {
                    // 建設コストで消費予定の素材を予約として渡し、電線の所持数判定から除外する
                    // Pass construction-cost materials as reservations to exclude them from wire availability
                    var reservedItems = blockMaster.RequiredItems == null
                        ? Array.Empty<(ItemId, int)>()
                        : blockMaster.RequiredItems.Select(v => (MasterHolder.ItemMaster.GetItemId(v.ItemGuid), v.Count)).ToArray();
                    plan = ElectricWireAutoConnectService.EvaluateAutoConnect(placeBlockId, placeInfo.Position, placeInfo.Direction, reservedItems, inventory.InventoryItems);
                    if (!plan.IsPlaceable) return;
                }

                // 設置に失敗した場合はコストを消費しない
                // Do not consume the cost when placement fails
                if (!ServerContext.WorldBlockDatastore.TryAddBlock(placeBlockId, placeInfo.Position, placeInfo.Direction, createParams, out var block)) return;

                ConstructionCostService.ConsumeRequiredItems(blockMaster.RequiredItems, inventory);

                // 計画を実行しワイヤー消費
                // Execute the validated plan: add wires and consume wire items
                if (isElectric) ElectricWireAutoConnectService.ExecuteAutoConnect(plan, block, inventory);
            }

            #endregion
        }

        [MessagePackObject]
        public class SendPlaceBlockProtocolMessagePack : ProtocolMessagePackBase
        {
            [IgnoreMember] public BlockId BlockId => new(BlockIdInt);

            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public int BlockIdInt { get; set; }
            [Key(4)] public List<PlaceInfoMessagePack> PlacePositions { get; set; }

            public SendPlaceBlockProtocolMessagePack(int playerId, BlockId blockId, List<PlaceInfo> placeInfos)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                BlockIdInt = blockId.AsPrimitive();
                PlacePositions = placeInfos.ConvertAll(v => new PlaceInfoMessagePack(v));
            }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SendPlaceBlockProtocolMessagePack() { }
        }
    }
}
