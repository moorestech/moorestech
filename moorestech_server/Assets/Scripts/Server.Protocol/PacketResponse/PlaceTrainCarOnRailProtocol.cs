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
            
            var railComponent = ResolveRailComponent(request.RailSpecifier);
            var inventoryData = _playerInventoryDataStore.GetInventoryData(request.PlayerId);
            if (railComponent == null || inventoryData == null) return null;
            
            var mainInventory = inventoryData.MainOpenableInventory;
            
            // ホットバースロットからアイテムを取得
            // Get item from hotbar slot
            var item = mainInventory.GetItem(request.InventorySlot);
            if (item.Id == ItemMaster.EmptyItemId || item.Count == 0) return null;
            
            // 列車編成データの取得
            // Load train composition definition
            var trainCars = BuildTrainCars(item.Id);
            if (trainCars == null || trainCars.Count == 0)
            {
                return CreateErrorResponse("無効な列車アイテムです");
            }
            
            // 列車ユニット生成
            // Build train unit from composition data
            CreateTrainUnit(railComponent, trainCars);
            
            // アイテムを消費
            // Consume the train item from inventory
            mainInventory.SetItem(request.InventorySlot, item.Id, item.Count - 1);
            
            return null;
            
            #region Internal
            
            TrainUnit CreateTrainUnit(RailComponent railComponent, List<TrainCar> trainCars)
            {
                var trainLength = trainCars.Sum(car => car.Length);
                
                var railNodes = new List<RailNode>
                {
                    railComponent.FrontNode,
                    railComponent.BackNode
                };
                
                var railPosition = new RailPosition(railNodes, trainLength, 0);
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

        [MessagePackObject]
        public class PlaceTrainOnRailResponseMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public bool IsSuccess { get; set; }
            [Key(3)] public string ErrorMessage { get; set; }
            [Key(4)] public string TrainIdStr { get; set; }
            [IgnoreMember] public Guid? TrainId => string.IsNullOrEmpty(TrainIdStr) ? null : Guid.Parse(TrainIdStr);

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public PlaceTrainOnRailResponseMessagePack()
            {
            }

            public PlaceTrainOnRailResponseMessagePack(
                bool isSuccess,
                Guid? trainId,
                string errorMessage)
            {
                // レスポンス情報を格納
                // Populate response information
                Tag = ProtocolTag;
                IsSuccess = isSuccess;
                TrainIdStr = trainId?.ToString();
                ErrorMessage = isSuccess ? null : errorMessage;
            }
        }

        #endregion

        #region Internal

        private RailComponent ResolveRailComponent(RailComponentSpecifier specifier)
        {
            if (specifier == null)
            {
                return null;
            }

            var blockDatastore = ServerContext.WorldBlockDatastore;
            var targetBlock = blockDatastore.GetBlock(specifier.Position.Vector3Int);
            if (targetBlock == null)
            {
                return null;
            }

            return specifier.Mode switch
            {
                RailConnectionEditProtocol.RailComponentSpecifierMode.Rail =>
                    targetBlock.TryGetComponent(out RailComponent railComponent) ? railComponent : null,
                RailConnectionEditProtocol.RailComponentSpecifierMode.Station =>
                    ResolveStationRailComponent(targetBlock, specifier.RailIndex),
                _ => null
            };
        }

        private RailComponent ResolveStationRailComponent(IBlock block, int railIndex)
        {
            if (!block.TryGetComponent(out RailSaverComponent saverComponent))
            {
                return null;
            }

            var rails = saverComponent.RailComponents;
            if (rails == null || railIndex < 0 || railIndex >= rails.Length)
            {
                return null;
            }

            return rails[railIndex];
        }

        private List<TrainCar> BuildTrainCars(ItemId trainItemId)
        {
            var itemElement = MasterHolder.ItemMaster.GetItemMaster(trainItemId);
            var compositionProperty = itemElement.GetType().GetProperty("TrainComposition");
            var composition = compositionProperty?.GetValue(itemElement);
            var carsProperty = composition?.GetType().GetProperty("Cars");
            var carDefinitions = carsProperty?.GetValue(composition) as IEnumerable<object>;

            if (carDefinitions == null)
            {
                return null;
            }

            var cars = new List<TrainCar>();
            foreach (var carDefinition in carDefinitions)
            {
                var type = carDefinition.GetType();
                var traction = Convert.ToInt32(type.GetProperty("TractionForce")?.GetValue(carDefinition) ?? 0);
                var slots = Convert.ToInt32(type.GetProperty("InventorySlots")?.GetValue(carDefinition) ?? 0);
                var length = Convert.ToInt32(type.GetProperty("Length")?.GetValue(carDefinition) ?? 0);
                var facing = Convert.ToBoolean(type.GetProperty("IsFacingForward")?.GetValue(carDefinition) ?? true);

                cars.Add(new TrainCar(traction, slots, length, 0, facing));
            }

            return cars;
        }

        private static PlaceTrainOnRailResponseMessagePack CreateErrorResponse(string message)
        {
            return new PlaceTrainOnRailResponseMessagePack(false, null, message);
        }

        #endregion
    }
}

