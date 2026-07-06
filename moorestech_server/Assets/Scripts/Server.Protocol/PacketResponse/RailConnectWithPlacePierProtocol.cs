using System;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.RailGraph;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.Construction;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// 橋脚ブロックを自動設置しながらレールを接続するプロトコル。
    /// 解放状態と橋脚コストを設置前に検証し、レール不足や接続失敗時は設置をロールバックして失敗させる。
    /// Protocol to connect rails while automatically placing a pier block.
    /// Unlock state and pier cost are validated before placement; rail shortages and connection failures roll back the placement.
    /// </summary>
    public class RailConnectWithPlacePierProtocol : IPacketResponse
    {
        public const string Tag = "va:railConnectWithPlacePier";
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly RailConnectionCommandHandler _commandHandler;
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;

        public RailConnectWithPlacePierProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _railGraphDatastore = serviceProvider.GetService<IRailGraphDatastore>();
            _commandHandler = serviceProvider.GetService<RailConnectionCommandHandler>();
            _gameUnlockStateDataController = serviceProvider.GetService<IGameUnlockStateDataController>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var request = MessagePackSerializer.Deserialize<RailConnectWithPlacePierRequest>(payload);
            var inventory = _playerInventoryDataStore.GetInventoryData(request.PlayerId).MainOpenableInventory;
            var placePosition = (Vector3Int)request.PierPlaceInfo.Position;

            // fromNodeの解決と設置先の空き確認
            // Resolve the from node and ensure the placement position is free
            if (!_railGraphDatastore.TryGetRailNode(request.FromNodeId, out var fromNode) || fromNode == null || fromNode.Guid != request.FromGuid) return RailConnectWithPlacePierResponse.CreateFailedResponse();
            if (ServerContext.WorldBlockDatastore.Exists(placePosition)) return RailConnectWithPlacePierResponse.CreateFailedResponse();

            // 解放状態を検証する（解放判定は基底ブロック）
            // Validate the unlock state (judged on the base block)
            var baseBlockGuid = MasterHolder.BlockMaster.GetBlockMaster(request.PierBlockId).BlockGuid;
            if (!_gameUnlockStateDataController.BlockUnlockStateInfos[baseBlockGuid].IsUnlocked) return RailConnectWithPlacePierResponse.CreateFailedResponse();

            // 橋脚がレールブロックであることと建設コストを設置前に検証する
            // Validate the pier is a rail block and its construction cost before placement
            var blockId = request.PierBlockId;
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            if (blockMaster.BlockParam is not TrainRailBlockParam) return RailConnectWithPlacePierResponse.CreateFailedResponse();
            var pierItemCounts = ConstructionCostService.ToItemCounts(blockMaster.RequiredItems);
            if (!ConstructionCostService.HasRequiredItems(pierItemCounts, inventory.InventoryItems)) return RailConnectWithPlacePierResponse.CreateFailedResponse();

            // 橋脚を設置する
            // Place the pier block
            var createParams = request.PierPlaceInfo.BlockCreateParams.Select(v => new BlockCreateParam(v.Key, v.Value)).ToArray();
            if (!ServerContext.WorldBlockDatastore.TryAddBlock(blockId, placePosition, request.PierPlaceInfo.Direction, createParams, out var block)) return RailConnectWithPlacePierResponse.CreateFailedResponse();
            var toNode = block.GetComponent<RailComponent>().BackNode;

            // レール長は設置後のtoNodeからのみ確定するため、レール不足時は設置を取り消して失敗させる
            // Rail length is only known from the placed toNode, so roll back the placement when rails run short
            var railLength = RailConnectionEditProtocol.GetRailLength(fromNode, toNode);
            if (!RailConnectionEditProtocol.TryResolveRailItemForPlacement(request.RailTypeGuid, inventory.InventoryItems, railLength, out var railItem, out var requiredCount))
            {
                ServerContext.WorldBlockDatastore.RemoveBlock(placePosition, BlockRemoveReason.ManualRemove);
                return RailConnectWithPlacePierResponse.CreateFailedResponse();
            }

            // レール所要数に橋脚コスト中の同一アイテム分を予約として上乗せして判定する
            // Judge rail sufficiency with the pier cost's same-item amount added as a reservation
            var railItemId = request.RailTypeGuid == Guid.Empty ? ItemMaster.EmptyItemId : MasterHolder.ItemMaster.GetItemId(railItem.ItemGuid);
            if (request.RailTypeGuid != Guid.Empty)
            {
                var reservedRailCount = pierItemCounts.Where(pair => pair.itemId == railItemId).Sum(pair => pair.count);
                var ownedRailCount = inventory.InventoryItems.Where(stack => stack.Id == railItemId).Sum(stack => stack.Count);
                if (ownedRailCount < requiredCount + reservedRailCount)
                {
                    ServerContext.WorldBlockDatastore.RemoveBlock(placePosition, BlockRemoveReason.ManualRemove);
                    return RailConnectWithPlacePierResponse.CreateFailedResponse();
                }
            }

            // 接続失敗時は孤立橋脚とコスト消費を残さないよう設置を取り消して失敗させる
            // On connection failure, roll back the placement so no orphan pier or cost consumption remains
            if (!_commandHandler.TryConnect(fromNode.NodeId, fromNode.Guid, toNode.NodeId, toNode.Guid, request.RailTypeGuid))
            {
                ServerContext.WorldBlockDatastore.RemoveBlock(placePosition, BlockRemoveReason.ManualRemove);
                return RailConnectWithPlacePierResponse.CreateFailedResponse();
            }

            // 橋脚コストと接続に使ったレールを消費する
            // Consume the pier cost and the rails used by the connection
            ConstructionCostService.ConsumeRequiredItems(pierItemCounts, inventory);
            if (request.RailTypeGuid != Guid.Empty)
            {
                ElectricWireSystemUtil.ConsumeItem(inventory, railItemId, requiredCount);
            }

            return RailConnectWithPlacePierResponse.Create(toNode.NodeId, toNode.Guid);
        }

        [MessagePackObject]
        public class RailConnectWithPlacePierResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public int ToNodeId { get; set; }
            [Key(4)] public Guid ToNodeGuid { get; set; }

            public RailConnectWithPlacePierResponse()
            {
                Tag = RailConnectWithPlacePierProtocol.Tag;
            }

            public static RailConnectWithPlacePierResponse CreateFailedResponse()
            {
                return new RailConnectWithPlacePierResponse()
                {
                    Success = false
                };
            }

            public static RailConnectWithPlacePierResponse Create(int toNodeId, Guid toNodeGuid)
            {
                return new RailConnectWithPlacePierResponse()
                {
                    Success = true,
                    ToNodeId = toNodeId,
                    ToNodeGuid = toNodeGuid
                };
            }
        }

        [MessagePackObject]
        public class RailConnectWithPlacePierRequest : ProtocolMessagePackBase
        {
            [Key(2)] public int FromNodeId { get; set; }
            [Key(3)] public Guid FromGuid { get; set; }
            [Key(4)] public PlaceInfoMessagePack PierPlaceInfo { get; set; }
            [Key(5)] public int PlayerId { get; set; }
            [Key(6)] public int PierBlockIdInt { get; set; }
            [Key(7)] public Guid RailTypeGuid { get; set; }

            [IgnoreMember] public BlockId PierBlockId => new(PierBlockIdInt);

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailConnectWithPlacePierRequest()
            {
                Tag = RailConnectWithPlacePierProtocol.Tag;
            }

            public static RailConnectWithPlacePierRequest Create(int playerId, int fromNodeId, Guid fromGuid, BlockId pierBlockId, PlaceInfo placeInfo, Guid railTypeGuid)
            {
                return new RailConnectWithPlacePierRequest
                {
                    PlayerId = playerId,
                    FromNodeId = fromNodeId,
                    FromGuid = fromGuid,
                    PierBlockIdInt = pierBlockId.AsPrimitive(),
                    RailTypeGuid = railTypeGuid,
                    PierPlaceInfo = new PlaceInfoMessagePack(placeInfo),
                };
            }
        }
    }
}
