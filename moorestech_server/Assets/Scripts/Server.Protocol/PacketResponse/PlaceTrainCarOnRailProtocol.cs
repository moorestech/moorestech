using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.TrainModule;
using Server.Util.MessagePack;
using RailComponentSpecifier = Server.Protocol.PacketResponse.RailConnectionEditProtocol.RailComponentSpecifier;
using Game.Block.Interface.Extension;

namespace Server.Protocol.PacketResponse
{
    public class PlaceTrainCarOnRailProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:placeTrainCar";

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public PlaceTrainCarOnRailProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<PlaceTrainOnRailRequestMessagePack>(payload.ToArray());

            // レールがあるかチェック
            // Check if rail component exists
            var railComponent = RailConnectionEditProtocol.ResolveRailComponent(request.RailSpecifier);
            if (railComponent == null) return null;
            
            // 手持ちのアイテム取得
            // Get the item from player's inventory
            var mainInventory = _playerInventoryDataStore.GetInventoryData(request.PlayerId).MainOpenableInventory;
            var item = mainInventory.GetItem(request.InventorySlot);

            // 列車ユニット生成
            // Build train unit from composition data
            var trainUnit = CreateTrainUnit(railComponent, item.Id);
            if (trainUnit == null) return null;

            // アイテムを消費
            // Consume the train item from inventory
            mainInventory.SetItem(request.InventorySlot, item.Id, item.Count - 1);

            return null;

            #region Internal

            TrainUnit CreateTrainUnit(RailComponent rail, ItemId trainItemId)
            {
                // アイテムIDに対応する列車ユニット編成を検索
                // Search for train unit composition matching the item ID
                if (!MasterHolder.TrainUnitMaster.TryGetTrainUnit(trainItemId, out var trainUnitElement))
                {
                    return null;
                }

                // TrainCarElementからTrainCarオブジェクトを生成
                // Create TrainCar objects from TrainCarElement data
                var trainCars = new TrainCar(
                    tractionForce: trainUnitElement.TractionForce,
                    inventorySlots: trainUnitElement.InventorySlots,
                    length: trainUnitElement.Length,
                    fuelSlots: trainUnitElement.FuelSlots,
                    isFacingForward: trainUnitElement.IsFacingForward
                );
                
                // レール位置を初期化 - 接続されたノードの経路を構築
                // Initialize rail position - build path from connected nodes
                var railNodes = BuildConnectedNodePath(rail);
                if (railNodes == null || railNodes.Count == 0)
                {
                    return null;
                }

                var railPosition = new RailPosition(railNodes, trainUnitElement.Length, 0);

                // TrainUnitを生成して返す
                // Create and return TrainUnit
                return new TrainUnit(railPosition, new List<TrainCar> { trainCars });
            }

            List<RailNode> BuildConnectedNodePath(RailComponent startRail)
            {
                // 開始ノードを決定（FrontNodeから接続を探す）
                // Determine starting node (search for connections from FrontNode)
                var startNode = startRail.FrontNode;
                var connectedNodes = startNode.ConnectedNodes.ToList();

                // FrontNodeに接続がない場合はBackNodeを試す
                // If FrontNode has no connections, try BackNode
                if (connectedNodes.Count == 0)
                {
                    startNode = startRail.BackNode;
                    connectedNodes = startNode.ConnectedNodes.ToList();
                }

                // 接続ノードがある場合は接続先ノードと開始ノードのパスを作成
                // RailPositionのリストは進行方向の逆順（目的地が先頭）なので、接続先を先に配置
                // If there are connected nodes, create path with connected node first then start node
                // RailPosition list is in reverse order of travel (destination first), so connected node comes first
                if (connectedNodes.Count > 0)
                {
                    var path = new List<RailNode> { connectedNodes[0], startNode };
                    return path;
                }

                // 接続がない場合は開始ノードのみでパスを作成
                // If no connections, create path with just the start node
                return new List<RailNode> { startNode };
            }

            #endregion
        }

        #region MessagePack Classes

        [MessagePackObject]
        public class PlaceTrainOnRailRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public RailComponentSpecifier RailSpecifier { get; set; }
            [Key(3)] public int HotBarSlot { get; set; }
            [IgnoreMember] public int InventorySlot => PlayerInventoryConst.HotBarSlotToInventorySlot(HotBarSlot);
            [Key(4)] public int PlayerId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public PlaceTrainOnRailRequestMessagePack()
            {
                // タグを既定値に設定
                // Initialize tag with default value
                Tag = ProtocolTag;
            }

            public PlaceTrainOnRailRequestMessagePack(
                RailComponentSpecifier railSpecifier,
                int hotBarSlot,
                int playerId)
            {
                // 必須情報を格納
                // Store required request information
                Tag = ProtocolTag;
                RailSpecifier = railSpecifier;
                HotBarSlot = hotBarSlot;
                PlayerId = playerId;
            }
        }

        #endregion
    }
}
