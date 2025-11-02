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

            // リクエスト内容を検証し、列車配置を実行
            // Validate request contents and execute train placement
            
            var railComponent = RailConnectionEditProtocol.ResolveRailComponent(request.RailSpecifier);
            var inventoryData = _playerInventoryDataStore.GetInventoryData(request.PlayerId);
            if (railComponent == null || inventoryData == null) return null;
            
            var mainInventory = inventoryData.MainOpenableInventory;
            
            // ホットバースロットからアイテムを取得
            // Get item from hotbar slot
            var item = mainInventory.GetItem(request.InventorySlot);
            if (item.Id == ItemMaster.EmptyItemId || item.Count == 0) return null;
            
            // 列車ユニット生成
            // Build train unit from composition data
            var trainUnit = CreateTrainUnit(railComponent, item.Id);
            if (trainUnit == null) return null;

            // アイテムを消費
            // Consume the train item from inventory
            mainInventory.SetItem(request.InventorySlot, item.Id, item.Count - 1);
            
            return null;
            
            #region Internal
            
            TrainUnit CreateTrainUnit(RailComponent railComponent, ItemId trainItemId)
            {
                // MasterHolderのTrainから列車データを取得
                // Get train data from MasterHolder.TrainUnitMaster
                var trainMaster = MasterHolder.TrainUnitMaster;
                if (trainMaster?.Train?.TrainUnits == null)
                {
                    return null;
                }

                // アイテムIDに対応する列車ユニット編成を検索
                // Search for train unit composition matching the item ID
                TrainUnitMasterElement trainUnitElement = null;
                foreach (var unit in trainMaster.Train.TrainUnits)
                {
                    if (unit.ItemGuid.HasValue && MasterHolder.ItemMaster.GetItemId(unit.ItemGuid.Value) == trainItemId)
                    {
                        trainUnitElement = unit;
                        break;
                    }
                }

                if (trainUnitElement == null || trainUnitElement.TrainCars == null)
                {
                    return null;
                }

                // TrainCarElementからTrainCarオブジェクトを生成
                // Create TrainCar objects from TrainCarElement data
                var trainCars = new List<TrainCar>();
                foreach (var carElement in trainUnitElement.TrainCars)
                {
                    var car = new TrainCar(
                        tractionForce: carElement.TractionForce,
                        inventorySlots: carElement.InventorySlots,
                        length: carElement.Length,
                        fuelSlots: carElement.FuelSlots,
                        isFacingForward: carElement.IsFacingForward
                    );
                    trainCars.Add(car);
                }

                // 列車全体の長さを計算
                // Calculate total train length
                var trainLength = trainCars.Sum(car => car.Length);

                // レール位置を初期化
                // Initialize rail position
                var railNodes = new List<RailNode>
                {
                    railComponent.FrontNode,
                    railComponent.BackNode
                };

                var railPosition = new RailPosition(railNodes, trainLength, 0);

                // TrainUnitを生成して返す
                // Create and return TrainUnit
                return new TrainUnit(railPosition, trainCars);
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

