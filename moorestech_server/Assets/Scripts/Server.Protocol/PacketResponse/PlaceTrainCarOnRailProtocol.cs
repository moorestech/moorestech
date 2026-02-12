using System;
using System.Collections.Generic;
using Core.Master;
using Game.PlayerInventory.Interface;
using Game.Train.Diagram;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.Train.RailGraph;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class PlaceTrainCarOnRailProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:placeTrainCar";
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly TrainUpdateService _trainUpdateService;
        private readonly TrainRailPositionManager _railPositionManager;
        private readonly TrainDiagramManager _diagramManager;
        
        public PlaceTrainCarOnRailProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _railGraphDatastore = serviceProvider.GetService<IRailGraphDatastore>();
            _trainUpdateService = serviceProvider.GetService<TrainUpdateService>();
            _railPositionManager = serviceProvider.GetService<TrainRailPositionManager>();
            _diagramManager = serviceProvider.GetService<TrainDiagramManager>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<PlaceTrainOnRailRequestMessagePack>(payload.ToArray());
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
                
                // 手持ちアイテムを取得し検証する
                // Resolve and validate inventory item
                var inventoryData = _playerInventoryDataStore.GetInventoryData(data.PlayerId);
                var mainInventory = inventoryData.MainOpenableInventory;
                var item = mainInventory.GetItem(data.InventorySlot);
                if (item == null || item.Count <= 0)
                {
                    return PlaceTrainOnRailResponseMessagePack.CreateFailure(PlaceTrainCarFailureType.ItemNotFound);
                }
                
                // 列車ユニットを生成して検証する
                // Create and validate the train unit
                if (!TryCreateTrainUnit(item.Id, data.RailPosition, out var createdTrain, out var failureType))
                {
                    return PlaceTrainOnRailResponseMessagePack.CreateFailure(failureType);
                }
                
                // アイテムを消費する
                // Consume the train item from inventory
                mainInventory.SetItem(data.InventorySlot, item.Id, item.Count - 1);
                
                return PlaceTrainOnRailResponseMessagePack.CreateSuccess();
                
                bool TryCreateTrainUnit(ItemId trainItemId, RailPositionSnapshotMessagePack railPositionSnapshot, out TrainUnit trainUnit, out PlaceTrainCarFailureType failureType)
                {
                    trainUnit = null;
                    failureType = PlaceTrainCarFailureType.InvalidRailPosition;
                    // アイテムIDに対応する車両マスターを検索する
                    // Resolve train car master by item id
                    if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(trainItemId, out var trainCarMaster))
                    {
                        return false;
                    }
                    
                    // 期待する列車長とレール位置を検証する
                    // Validate expected train length and rail position
                    var expectedLength = TrainLengthConverter.ToRailUnits(trainCarMaster.Length);
                    if (!TryRestoreRailPosition(railPositionSnapshot, expectedLength, out var railPosition, out failureType))
                    {
                        return false;
                    }
                    
                    // 単一車両の列車編成を生成する
                    // Create a single-car train unit
                    var trainCar = new TrainCar(trainCarMaster, true);
                    trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, _trainUpdateService, _railPositionManager, _diagramManager);
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
            [Key(3)] public int HotBarSlot { get; set; }
            [IgnoreMember] public int InventorySlot => PlayerInventoryConst.HotBarSlotToInventorySlot(HotBarSlot);
            [Key(4)] public int PlayerId { get; set; }
            
            [Obsolete("This constructor is for deserialization. Do not use directly.")]
            public PlaceTrainOnRailRequestMessagePack()
            {
                // タグを既定値に設定
                // Initialize tag with default value
                Tag = ProtocolTag;
            }
            
            public PlaceTrainOnRailRequestMessagePack(
                RailPositionSnapshotMessagePack railPosition,
                int hotBarSlot,
                int playerId)
            {
                // 必須情報を格納
                // Store required request information
                Tag = ProtocolTag;
                RailPosition = railPosition;
                HotBarSlot = hotBarSlot;
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
            
            [Obsolete("This constructor is for deserialization. Do not use directly.")]
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
        }
        
        #endregion
    }
}
