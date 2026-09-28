using Server.Protocol.PacketResponse.Util.TrainPlacement;
using System;
using Core.Master;
using Game.Construction;
using Game.PlayerInventory.Interface;
using Game.Train.Event;
using Game.Train.Unit;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.Construction;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class PlaceTrainCarOnRailProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:placeTrainCar";
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly TrainCarRailPlacement _placement;
        private readonly ITrainUnitMutationDatastore _trainUnitMutationDatastore;
        private readonly ITrainUnitSnapshotNotifyEvent _trainUnitSnapshotNotifyEvent;
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;

        public PlaceTrainCarOnRailProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _placement = new TrainCarRailPlacement(serviceProvider);
            _trainUnitMutationDatastore = serviceProvider.GetService<ITrainUnitMutationDatastore>();
            _trainUnitSnapshotNotifyEvent = serviceProvider.GetService<ITrainUnitSnapshotNotifyEvent>();
            _gameUnlockStateDataController = serviceProvider.GetService<IGameUnlockStateDataController>();
        }
        
        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            var request = MessagePackSerializer.Deserialize<PlaceTrainOnRailRequestMessagePack>(payload);
            return ExecuteRequest(request);
            
            #region Internal
            PlaceTrainOnRailResponseMessagePack ExecuteRequest(PlaceTrainOnRailRequestMessagePack data)
            {
                // リクエストとレール位置を検証する
                // Validate request and rail position
                if (data == null)
                {
                    return PlaceTrainOnRailResponseMessagePack.CreateFailure(PlaceTrainCarFailureType.InvalidRequest);
                }
                if (data.RailPosition == null)
                {
                    return PlaceTrainOnRailResponseMessagePack.CreateFailure(PlaceTrainCarFailureType.InvalidRailPosition);
                }
                
                // 車両マスタとアンロック状態を検証する
                // Validate the train car master and its unlock state
                if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(data.TrainCarGuid, out var trainCarMaster))
                {
                    return PlaceTrainOnRailResponseMessagePack.CreateFailure(PlaceTrainCarFailureType.ItemNotFound);
                }
                if (!_gameUnlockStateDataController.TrainCarUnlockStateInfos[data.TrainCarGuid].IsUnlocked)
                {
                    return PlaceTrainOnRailResponseMessagePack.CreateFailure(PlaceTrainCarFailureType.NotUnlocked);
                }

                // 建設コストの充足をインベントリ横断で検証する
                // Validate construction cost across the whole inventory
                var inventoryData = _playerInventoryDataStore.GetInventoryData(requesterPlayerId);
                var mainInventory = inventoryData.MainOpenableInventory;
                var costItemCounts = ConstructionCostItems.ToItemCounts(trainCarMaster.RequiredItems);
                if (!ConstructionCostService.HasRequiredItems(costItemCounts, mainInventory.InventoryItems))
                {
                    return PlaceTrainOnRailResponseMessagePack.CreateFailure(PlaceTrainCarFailureType.InsufficientItems);
                }

                // 列車ユニットを生成して検証する
                // Create and validate the train unit
                if (!_placement.TryCreateTrainUnit(trainCarMaster, data.RailPosition, out var createdTrain, out var failureType))
                {
                    return PlaceTrainOnRailResponseMessagePack.CreateFailure(failureType);
                }

                // 建設コストを消費する
                // Consume the construction cost
                ConstructionCostService.ConsumeRequiredItems(costItemCounts, mainInventory);

                // 新規編成の単機スナップショットを通知する
                // Broadcast a per-unit snapshot for the newly created train.
                _trainUnitMutationDatastore.RegisterTrain(createdTrain);
                _trainUnitSnapshotNotifyEvent.NotifySnapshot(createdTrain);
                
                return PlaceTrainOnRailResponseMessagePack.CreateSuccess();
            }

            #endregion
        }
        
        #region MessagePack Classes
        
        [MessagePackObject]
        public class PlaceTrainOnRailRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public RailPositionSnapshotMessagePack RailPosition { get; set; }
            [Key(3)] public Guid TrainCarGuid { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public PlaceTrainOnRailRequestMessagePack()
            {
                // タグを既定値に設定
                // Initialize tag with default value
                Tag = ProtocolTag;
            }

            public PlaceTrainOnRailRequestMessagePack(
                RailPositionSnapshotMessagePack railPosition,
                Guid trainCarGuid)
            {
                // 必須情報を格納
                // Store required request information
                Tag = ProtocolTag;
                RailPosition = railPosition;
                TrainCarGuid = trainCarGuid;
            }
        }
        
        // 設置レスポンスのペイロード
        // Response payload for train placement
        [MessagePackObject]
        public class PlaceTrainOnRailResponseMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public PlaceTrainCarFailureType FailureType { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public PlaceTrainOnRailResponseMessagePack()
            {
                Tag = ProtocolTag;
            }
            
            public static PlaceTrainOnRailResponseMessagePack CreateSuccess()
            {
                return new PlaceTrainOnRailResponseMessagePack
                {
                    Success = true,
                    FailureType = PlaceTrainCarFailureType.None
                };
            }
            
            public static PlaceTrainOnRailResponseMessagePack CreateFailure(PlaceTrainCarFailureType failureType)
            {
                return new PlaceTrainOnRailResponseMessagePack
                {
                    Success = false,
                    FailureType = failureType
                };
            }
        }
        
        public enum PlaceTrainCarFailureType
        {
            None = 0,
            InvalidRequest = 1,
            RailNotFound = 2,
            ItemNotFound = 3,
            InvalidRailPosition = 4,
            NotUnlocked = 5,
            InsufficientItems = 6,
        }
        
        #endregion
    }
}
