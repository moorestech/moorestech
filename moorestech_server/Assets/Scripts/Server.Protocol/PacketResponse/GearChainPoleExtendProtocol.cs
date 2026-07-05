using System;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.GearChain;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// 歯車チェーンポールをレール風に自動設置しながら延長するプロトコル。
    /// 全検証を設置前に完了させ、失敗時は一切の状態変更を起こさない。
    /// Protocol to extend gear chain poles rail-style with automatic pole placement.
    /// All validations run before placement so failures leave no state changes.
    /// </summary>
    public class GearChainPoleExtendProtocol : IPacketResponse
    {
        public const string Tag = "va:gearChainPoleExtend";

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public GearChainPoleExtendProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<GearChainPoleExtendRequest>(payload);
            var inventory = _playerInventoryDataStore.GetInventoryData(request.PlayerId).MainOpenableInventory;
            var placePosition = (Vector3Int)request.PolePlaceInfo.Position;

            // 設置先が空いているか確認する
            // Ensure the placement position is free
            if (ServerContext.WorldBlockDatastore.Exists(placePosition)) return GearChainPoleExtendResponse.CreateFailed(GearChainPlacementEvaluator.PositionOccupiedError);

            // スロット番号の妥当性を確認する（不正クライアント対策）
            // Validate the slot index (guards against malformed clients)
            if (request.PoleInventorySlot < 0 || inventory.GetSlotSize() <= request.PoleInventorySlot) return GearChainPoleExtendResponse.CreateFailed(GearChainPlacementEvaluator.NoPoleItemError);

            // ポールアイテムからブロックとパラメータを解決する
            // Resolve block and parameter from the pole item
            var poleItemStack = inventory.GetItem(request.PoleInventorySlot);
            if (!MasterHolder.BlockMaster.IsBlock(poleItemStack.Id)) return GearChainPoleExtendResponse.CreateFailed(GearChainPlacementEvaluator.NoPoleItemError);
            var blockId = MasterHolder.BlockMaster.GetBlockId(poleItemStack.Id).GetVerticalOverrideBlockId(request.PolePlaceInfo.VerticalDirection);
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            if (blockMaster.BlockParam is not GearChainPoleBlockParam poleParam) return GearChainPoleExtendResponse.CreateFailed(GearChainPlacementEvaluator.NoPoleItemError);

            // 起点ありの場合は接続可否を設置前にすべて検証する
            // With a from pole, validate connection viability before placing
            if (request.HasFromPole)
            {
                if (!GearChainSystemUtil.TryGetGearChainPole(request.FromPolePosVector, out var fromPole, out _)) return GearChainPoleExtendResponse.CreateFailed(GearChainPlacementEvaluator.InvalidTargetError);

                // 新規ポール側は接続容量0の場合のみ上限超過として扱う
                // Treat the new pole as full only when its connection capacity is zero
                var anyConnectionFull = fromPole.IsConnectionFull || poleParam.MaxConnectionCount < 1;
                var distance = Vector3Int.Distance(request.FromPolePosVector, placePosition);
                var judgement = GearChainPlacementEvaluator.EvaluatePlacement(distance, fromPole.MaxConnectionDistance, poleParam.MaxConnectionDistance, false, anyConnectionFull, request.ChainItemId, inventory.InventoryItems, poleItemStack.Id);
                if (!judgement.IsPlaceable) return GearChainPoleExtendResponse.CreateFailed(judgement.FailureReason);
            }

            // ブロックを設置する
            // Place the block
            var createParams = request.PolePlaceInfo.BlockCreateParams.Select(v => new BlockCreateParam(v.Key, v.Value)).ToArray();
            if (!ServerContext.WorldBlockDatastore.TryAddBlock(blockId, placePosition, request.PolePlaceInfo.Direction, createParams, out var block)) return GearChainPoleExtendResponse.CreateFailed(GearChainPlacementEvaluator.PositionOccupiedError);

            // 起点ありならチェーン接続とアイテム消費
            // With a from pole, connect the chain and consume chain items
            if (request.HasFromPole && !GearChainSystemUtil.TryConnect(request.FromPolePosVector, placePosition, request.PlayerId, request.ChainItemId, out var connectError))
            {
                // 事前検証済みのため通常到達しないが、孤立ポールを残さないよう設置を取り消す
                // Unreachable after pre-validation; remove the block to avoid leaving an orphan pole
                ServerContext.WorldBlockDatastore.RemoveBlock(placePosition, BlockRemoveReason.ManualRemove);
                return GearChainPoleExtendResponse.CreateFailed(connectError);
            }

            // ポールアイテムを1個消費する（チェーン消費後の最新スタックから減算する）
            // Consume one pole item (subtract from the latest stack after chain consumption)
            var latestPoleItemStack = inventory.GetItem(request.PoleInventorySlot);
            inventory.SetItem(request.PoleInventorySlot, latestPoleItemStack.SubItem(1));

            return GearChainPoleExtendResponse.CreateSuccess(placePosition, block.BlockInstanceId.AsPrimitive());
        }

        [MessagePackObject]
        public class GearChainPoleExtendRequest : ProtocolMessagePackBase
        {
            [Key(2)] public bool HasFromPole { get; set; }
            [Key(3)] public Vector3IntMessagePack FromPolePos { get; set; }
            [Key(4)] public PlaceBlockFromHotBarProtocol.PlaceInfoMessagePack PolePlaceInfo { get; set; }
            [Key(5)] public int PlayerId { get; set; }
            [Key(6)] public int PoleInventorySlot { get; set; }
            [Key(7)] public ItemId ChainItemId { get; set; }

            [IgnoreMember] public Vector3Int FromPolePosVector => FromPolePos;

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public GearChainPoleExtendRequest()
            {
                Tag = GearChainPoleExtendProtocol.Tag;
            }

            public static GearChainPoleExtendRequest CreateExtendRequest(int playerId, Vector3Int fromPolePos, int poleInventorySlot, PlaceInfo polePlaceInfo, ItemId chainItemId)
            {
                return new GearChainPoleExtendRequest
                {
                    HasFromPole = true,
                    FromPolePos = new Vector3IntMessagePack(fromPolePos),
                    PolePlaceInfo = new PlaceBlockFromHotBarProtocol.PlaceInfoMessagePack(polePlaceInfo),
                    PlayerId = playerId,
                    PoleInventorySlot = poleInventorySlot,
                    ChainItemId = chainItemId,
                };
            }

            public static GearChainPoleExtendRequest CreateIsolatedPlaceRequest(int playerId, int poleInventorySlot, PlaceInfo polePlaceInfo)
            {
                return new GearChainPoleExtendRequest
                {
                    HasFromPole = false,
                    FromPolePos = new Vector3IntMessagePack(Vector3Int.zero),
                    PolePlaceInfo = new PlaceBlockFromHotBarProtocol.PlaceInfoMessagePack(polePlaceInfo),
                    PlayerId = playerId,
                    PoleInventorySlot = poleInventorySlot,
                    ChainItemId = ItemMaster.EmptyItemId,
                };
            }
        }

        [MessagePackObject]
        public class GearChainPoleExtendResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool IsSuccess { get; set; }
            [Key(3)] public string Error { get; set; }
            [Key(4)] public Vector3IntMessagePack PlacedPolePos { get; set; }
            [Key(5)] public int PlacedBlockInstanceId { get; set; }

            public GearChainPoleExtendResponse()
            {
                Tag = GearChainPoleExtendProtocol.Tag;
            }

            public static GearChainPoleExtendResponse CreateFailed(string error)
            {
                return new GearChainPoleExtendResponse
                {
                    IsSuccess = false,
                    Error = error,
                    PlacedPolePos = new Vector3IntMessagePack(Vector3Int.zero),
                };
            }

            public static GearChainPoleExtendResponse CreateSuccess(Vector3Int placedPolePos, int placedBlockInstanceId)
            {
                return new GearChainPoleExtendResponse
                {
                    IsSuccess = true,
                    Error = string.Empty,
                    PlacedPolePos = new Vector3IntMessagePack(placedPolePos),
                    PlacedBlockInstanceId = placedBlockInstanceId,
                };
            }
        }
    }
}
