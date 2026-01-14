using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.PlayerInventory.Interface;
using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using Game.Train.Utility;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using RailComponentSpecifier = Server.Protocol.PacketResponse.RailConnectionEditProtocol.RailComponentSpecifier;

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
                // リクエストとレールを検証する
                // Validate request and rail component
                if (data == null)
                {
                    return PlaceTrainOnRailResponseMessagePack.CreateFailure(PlaceTrainCarFailureType.InvalidRequest);
                }
                var railComponent = RailConnectionEditProtocol.ResolveRailComponent(data.RailSpecifier);
                if (railComponent == null)
                {
                    return PlaceTrainOnRailResponseMessagePack.CreateFailure(PlaceTrainCarFailureType.RailNotFound);
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
                if (!TryCreateTrainUnit(railComponent, item.Id, data.RailPosition, out var createdTrain))
                {
                    return PlaceTrainOnRailResponseMessagePack.CreateFailure(PlaceTrainCarFailureType.InvalidRailPosition);
                }
                
                // アイテムを消費する
                // Consume the train item from inventory
                mainInventory.SetItem(data.InventorySlot, item.Id, item.Count - 1);
                
                return PlaceTrainOnRailResponseMessagePack.CreateSuccess();
                
                bool TryCreateTrainUnit(RailComponent railComponent, ItemId trainItemId, RailPositionSnapshotMessagePack railPositionSnapshot, out TrainUnit trainUnit)
                {
                    trainUnit = null;
                    // アイテムIDに対応する列車マスターを検索する
                    // Resolve train master by item id
                    if (!MasterHolder.TrainUnitMaster.TryGetTrainUnit(trainItemId, out var trainCarMaster))
                    {
                        return false;
                    }
                    
                    // 期待する列車長とレール位置を検証する
                    // Validate expected train length and rail position
                    var expectedLength = TrainLengthConverter.ToRailUnits(trainCarMaster.Length);
                    if (!TryRestoreRailPosition(railComponent, railPositionSnapshot, expectedLength, out var railPosition))
                    {
                        return false;
                    }
                    
                    // 列車ユニットを生成する
                    // Create the train unit
                    var trainCar = new TrainCar(trainCarMaster, true);
                    trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, _trainUpdateService, _railPositionManager, _diagramManager);
                    return true;
                }
                
                bool TryRestoreRailPosition(RailComponent railComponent, RailPositionSnapshotMessagePack snapshot, int expectedLength, out RailPosition position)
                {
                    position = null;
                    // スナップショットを検証する
                    // Validate snapshot payload
                    if (snapshot == null)
                    {
                        return false;
                    }
                    var saveData = snapshot.ToModel();
                    if (saveData == null || saveData.RailSnapshot == null || saveData.RailSnapshot.Count == 0)
                    {
                        return false;
                    }
                    if (saveData.TrainLength != expectedLength)
                    {
                        return false;
                    }
                    // サーバー側の経路から期待スナップショットを構築する
                    // Build the expected snapshot from the server rail graph
                    if (!TryBuildExpectedSnapshot(railComponent, saveData.RailSnapshot[0], expectedLength, out var expectedSnapshot))
                    {
                        return false;
                    }
                    if (!IsSnapshotMatch(saveData, expectedSnapshot))
                    {
                        return false;
                    }
                    
                    // RailPositionを復元する
                    // Restore the rail position instance
                    position = RailPositionFactory.Restore(expectedSnapshot, railComponent.FrontNode.GraphProvider);
                    return position != null;
                }
                
                bool TryBuildExpectedSnapshot(RailComponent railComponent, ConnectionDestination headDestination, int trainLength, out RailPositionSaveData expected)
                {
                    expected = null;
                    // レールグラフから位置を再構築する
                    // Rebuild placement snapshot from the rail graph
                    var headNode = railComponent.FrontNode.GraphProvider.ResolveRailNode(headDestination);
                    if (headNode == null)
                    {
                        return false;
                    }
                    var graphSnapshot = _railGraphDatastore.CaptureSnapshot(_trainUpdateService.GetCurrentTick());
                    var railSnapshot = new List<ConnectionDestination> { headNode.ConnectionDestination };
                    var remaining = trainLength;
                    var currentNodeId = headNode.NodeId;
                    var guard = 0;
                    while (remaining > 0)
                    {
                        if (!TryGetIncomingEdge(graphSnapshot.Connections, currentNodeId, out var previousNodeId, out var distance))
                        {
                            return false;
                        }
                        if (distance <= 0)
                        {
                            return false;
                        }
                        if (!_railGraphDatastore.TryGetRailNode(previousNodeId, out var previousNode))
                        {
                            return false;
                        }
                        railSnapshot.Add(previousNode.ConnectionDestination);
                        remaining -= distance;
                        currentNodeId = previousNodeId;
                        guard++;
                        if (guard > graphSnapshot.Connections.Count + 1)
                        {
                            return false;
                        }
                    }
                    expected = new RailPositionSaveData
                    {
                        TrainLength = trainLength,
                        DistanceToNextNode = 0,
                        RailSnapshot = railSnapshot
                    };
                    return true;
                }
                
                bool TryGetIncomingEdge(IReadOnlyList<RailGraphConnectionSnapshot> connections, int nodeId, out int sourceNodeId, out int distance)
                {
                    sourceNodeId = -1;
                    distance = 0;
                    // 分岐時は最小NodeIdの入力辺を選択する
                    // Choose the incoming edge with the smallest node id
                    var found = false;
                    for (var i = 0; i < connections.Count; i++)
                    {
                        var connection = connections[i];
                        if (connection.ToNodeId != nodeId)
                        {
                            continue;
                        }
                        if (!found || connection.FromNodeId < sourceNodeId)
                        {
                            sourceNodeId = connection.FromNodeId;
                            distance = connection.Distance;
                            found = true;
                        }
                    }
                    return found;
                }
                
                bool IsSnapshotMatch(RailPositionSaveData clientSnapshot, RailPositionSaveData expectedSnapshot)
                {
                    // クライアント提案と一致するか確認する
                    // Check if the client snapshot matches the expected snapshot
                    if (clientSnapshot == null || expectedSnapshot == null)
                    {
                        return false;
                    }
                    if (clientSnapshot.TrainLength != expectedSnapshot.TrainLength)
                    {
                        return false;
                    }
                    if (clientSnapshot.DistanceToNextNode != expectedSnapshot.DistanceToNextNode)
                    {
                        return false;
                    }
                    var clientNodes = clientSnapshot.RailSnapshot;
                    var expectedNodes = expectedSnapshot.RailSnapshot;
                    if (clientNodes == null || expectedNodes == null || clientNodes.Count != expectedNodes.Count)
                    {
                        return false;
                    }
                    for (var i = 0; i < clientNodes.Count; i++)
                    {
                        if (!clientNodes[i].Equals(expectedNodes[i]))
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }
            #endregion
        }
        
        #region MessagePack Classes
        
        [MessagePackObject]
        public class PlaceTrainOnRailRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public RailComponentSpecifier RailSpecifier { get; set; }
            [Key(3)] public RailPositionSnapshotMessagePack RailPosition { get; set; }
            [Key(4)] public int HotBarSlot { get; set; }
            [IgnoreMember] public int InventorySlot => PlayerInventoryConst.HotBarSlotToInventorySlot(HotBarSlot);
            [Key(5)] public int PlayerId { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public PlaceTrainOnRailRequestMessagePack()
            {
                // タグを既定値に設定
                // Initialize tag with default value
                Tag = ProtocolTag;
            }
            
            public PlaceTrainOnRailRequestMessagePack(
                RailComponentSpecifier railSpecifier,
                RailPositionSnapshotMessagePack railPosition,
                int hotBarSlot,
                int playerId)
            {
                // 必須情報を格納
                // Store required request information
                Tag = ProtocolTag;
                RailSpecifier = railSpecifier;
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
        }
        
        #endregion
    }
}
