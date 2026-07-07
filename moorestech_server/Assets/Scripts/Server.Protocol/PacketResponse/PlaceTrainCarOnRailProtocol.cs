using System;
using System.Collections.Generic;
using Core.Master;
using Game.PlayerInventory.Interface;
using Game.Train.Diagram;
using Game.Train.Event;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.Train.RailGraph;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.TrainModule;
using Server.Protocol.PacketResponse.Util.Construction;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class PlaceTrainCarOnRailProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:placeTrainCar";
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly ITrainUnitMutationDatastore _trainUnitMutationDatastore;
        private readonly TrainRailPositionManager _railPositionManager;
        private readonly TrainDiagramManager _diagramManager;
        private readonly ITrainUnitSnapshotNotifyEvent _trainUnitSnapshotNotifyEvent;
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;

        public PlaceTrainCarOnRailProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _railGraphDatastore = serviceProvider.GetService<IRailGraphDatastore>();
            _trainUnitMutationDatastore = serviceProvider.GetService<ITrainUnitMutationDatastore>();
            _railPositionManager = serviceProvider.GetService<TrainRailPositionManager>();
            _diagramManager = serviceProvider.GetService<TrainDiagramManager>();
            _trainUnitSnapshotNotifyEvent = serviceProvider.GetService<ITrainUnitSnapshotNotifyEvent>();
            _gameUnlockStateDataController = serviceProvider.GetService<IGameUnlockStateDataController>();
        }
        
        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
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
                var inventoryData = _playerInventoryDataStore.GetInventoryData(data.PlayerId);
                var mainInventory = inventoryData.MainOpenableInventory;
                var costItemCounts = ConstructionCostService.ToItemCounts(trainCarMaster.RequiredItems);
                if (!ConstructionCostService.HasRequiredItems(costItemCounts, mainInventory.InventoryItems))
                {
                    return PlaceTrainOnRailResponseMessagePack.CreateFailure(PlaceTrainCarFailureType.InsufficientItems);
                }

                // 列車ユニットを生成して検証する
                // Create and validate the train unit
                if (!TryCreateTrainUnit(trainCarMaster, data.RailPosition, out var createdTrain, out var failureType))
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
                
                bool TryCreateTrainUnit(TrainCarMasterElement trainCarMaster, RailPositionSnapshotMessagePack railPositionSnapshot, out TrainUnit trainUnit, out PlaceTrainCarFailureType failureType)
                {
                    trainUnit = null;
                    failureType = PlaceTrainCarFailureType.InvalidRailPosition;

                    // 期待する列車長とレール位置を検証する
                    // Validate expected train length and rail position
                    var expectedLength = TrainLengthConverter.ToRailUnits(trainCarMaster.Length);
                    if (!TryRestoreRailPosition(railPositionSnapshot, expectedLength, out var railPosition, out failureType))
                    {
                        return false;
                    }
                    
                    // 単一車両の列車編成を生成する(コンテナ装着はTrainCarコンストラクタ内で自動)
                    // Create a single-car train unit (container is attached inside TrainCar constructor).
                    var trainCar = new TrainCar(trainCarMaster, true);
                    trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, _railPositionManager, _diagramManager);
                    return true;
                }
                
                bool TryRestoreRailPosition(RailPositionSnapshotMessagePack snapshot, int expectedLength, out RailPosition position, out PlaceTrainCarFailureType failureType)
                {
                    position = null;
                    failureType = PlaceTrainCarFailureType.InvalidRailPosition;
                    // スナップショットを検証する
                    // Validate snapshot payload
                    if (snapshot == null)
                    {
                        return false;
                    }
                    var saveData = snapshot.ToModel();
                    if (!TryValidateSnapshot(saveData, expectedLength, out var validatedSnapshot, out failureType))
                    {
                        return false;
                    }
                    
                    // RailPositionを復元する
                    // Restore the rail position instance
                    position = RailPositionFactory.Restore(validatedSnapshot, _railGraphDatastore);
                    return position != null;
                }
                
                bool TryValidateSnapshot(RailPositionSaveData snapshot, int expectedTrainLength, out RailPositionSaveData validatedSnapshot, out PlaceTrainCarFailureType failure)
                {
                    validatedSnapshot = null;
                    failure = PlaceTrainCarFailureType.InvalidRailPosition;
                    // 入力と列車長を検証する
                    // Validate inputs and train length
                    if (snapshot == null || snapshot.RailSnapshot == null || snapshot.RailSnapshot.Count < 2)
                    {
                        return false;
                    }
                    if (snapshot.TrainLength != expectedTrainLength)
                    {
                        return false;
                    }
                    if (snapshot.DistanceToNextNode < 0)
                    {
                        return false;
                    }
                    
                    // ノード列を解決する
                    // Resolve node list from destinations
                    var nodes = new List<IRailNode>(snapshot.RailSnapshot.Count);
                    for (var i = 0; i < snapshot.RailSnapshot.Count; i++)
                    {
                        var node = _railGraphDatastore.ResolveRailNode(snapshot.RailSnapshot[i]);
                        if (node == null)
                        {
                            failure = PlaceTrainCarFailureType.RailNotFound;
                            return false;
                        }
                        nodes.Add(node);
                    }
                    
                    // 距離と経路を検証する
                    // Validate distances and path connectivity
                    var totalDistance = 0;
                    for (var i = 0; i < nodes.Count - 1; i++)
                    {
                        var segmentDistance = nodes[i + 1].GetDistanceToNode(nodes[i]);
                        if (segmentDistance <= 0)
                        {
                            return false;
                        }
                        if (i == 0 && snapshot.DistanceToNextNode > segmentDistance)
                        {
                            return false;
                        }
                        totalDistance += segmentDistance;
                    }
                    
                    var requiredDistance = snapshot.TrainLength + snapshot.DistanceToNextNode;
                    if (totalDistance < requiredDistance)
                    {
                        return false;
                    }
                    
                    validatedSnapshot = new RailPositionSaveData
                    {
                        TrainLength = expectedTrainLength,
                        DistanceToNextNode = snapshot.DistanceToNextNode,
                        RailSnapshot = snapshot.RailSnapshot
                    };
                    return true;
                }
            }
            #endregion
        }
        
        #region MessagePack Classes
        
        [MessagePackObject]
        public class PlaceTrainOnRailRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public RailPositionSnapshotMessagePack RailPosition { get; set; }
            [Key(3)] public Guid TrainCarGuid { get; set; }
            [Key(4)] public int PlayerId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public PlaceTrainOnRailRequestMessagePack()
            {
                // タグを既定値に設定
                // Initialize tag with default value
                Tag = ProtocolTag;
            }

            public PlaceTrainOnRailRequestMessagePack(
                RailPositionSnapshotMessagePack railPosition,
                Guid trainCarGuid,
                int playerId)
            {
                // 必須情報を格納
                // Store required request information
                Tag = ProtocolTag;
                RailPosition = railPosition;
                TrainCarGuid = trainCarGuid;
                PlayerId = playerId;
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
