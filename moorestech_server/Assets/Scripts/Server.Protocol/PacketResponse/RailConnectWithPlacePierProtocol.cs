using System;
using System.Collections.Generic;
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
using Server.Protocol.PacketResponse.Util.ConnectTool;
using Server.Protocol.PacketResponse.Util.Construction;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
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

            // 未解放のconnectToolによる接続要求は設置前に拒否する（Empty=無コストは許可）
            // Reject a connection request using a connectTool that is not unlocked before placement (Empty is costless and allowed)
            if (request.ConnectToolGuid != Guid.Empty && !ConnectToolSelector.IsUnlocked(request.ConnectToolGuid)) return RailConnectWithPlacePierResponse.CreateFailedResponse();

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

            // レール長は設置後のtoNodeからのみ確定するため、レール素材算出は設置後に行う
            // Rail length is only known from the placed toNode, so compute the material cost after placement
            var railLength = RailConnectionEditProtocol.GetRailLength(fromNode, toNode);
            IReadOnlyList<ConnectToolMaterialCost> railMaterials = null;
            if (request.ConnectToolGuid != Guid.Empty)
            {
                // connectToolマスタから複数素材の必要数を算出し、橋脚コスト中の同一アイテム分を予約として上乗せして判定する
                // Compute multi-material requirement from the connectTool master and judge with the pier cost's same-item amount reserved
                if (!ConnectToolCostCalculator.TryCalculate(request.ConnectToolGuid, railLength, out railMaterials))
                {
                    ServerContext.WorldBlockDatastore.RemoveBlock(placePosition, BlockRemoveReason.ManualRemove);
                    return RailConnectWithPlacePierResponse.CreateFailedResponse();
                }

                foreach (var material in railMaterials)
                {
                    var reserved = pierItemCounts.Where(pair => pair.itemId == material.ItemId).Sum(pair => pair.count);
                    var owned = inventory.InventoryItems.Where(stack => stack.Id == material.ItemId).Sum(stack => stack.Count);
                    if (owned < material.Count + reserved)
                    {
                        ServerContext.WorldBlockDatastore.RemoveBlock(placePosition, BlockRemoveReason.ManualRemove);
                        return RailConnectWithPlacePierResponse.CreateFailedResponse();
                    }
                }
            }

            // 接続失敗時は孤立橋脚とコスト消費を残さないよう設置を取り消して失敗させる。RailTypeGuidにはconnectToolGuidを格納する
            // On connection failure, roll back the placement so no orphan pier or cost consumption remains; store the connectToolGuid in the RailTypeGuid slot
            if (!_commandHandler.TryConnect(fromNode.NodeId, fromNode.Guid, toNode.NodeId, toNode.Guid, request.ConnectToolGuid))
            {
                ServerContext.WorldBlockDatastore.RemoveBlock(placePosition, BlockRemoveReason.ManualRemove);
                return RailConnectWithPlacePierResponse.CreateFailedResponse();
            }

            // 橋脚コストと接続に使ったレール素材を消費する
            // Consume the pier cost and the rail materials used by the connection
            ConstructionCostService.ConsumeRequiredItems(pierItemCounts, inventory);
            if (railMaterials != null)
            {
                ConnectToolMaterialConsumer.Consume(railMaterials, inventory);
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
            [Key(7)] public Guid ConnectToolGuid { get; set; }

            [IgnoreMember] public BlockId PierBlockId => new(PierBlockIdInt);

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailConnectWithPlacePierRequest()
            {
                Tag = RailConnectWithPlacePierProtocol.Tag;
            }

            public static RailConnectWithPlacePierRequest Create(int playerId, int fromNodeId, Guid fromGuid, BlockId pierBlockId, PlaceInfo placeInfo, Guid connectToolGuid)
            {
                return new RailConnectWithPlacePierRequest
                {
                    PlayerId = playerId,
                    FromNodeId = fromNodeId,
                    FromGuid = fromGuid,
                    PierBlockIdInt = pierBlockId.AsPrimitive(),
                    ConnectToolGuid = connectToolGuid,
                    PierPlaceInfo = new PlaceInfoMessagePack(placeInfo),
                };
            }
        }
    }
}
