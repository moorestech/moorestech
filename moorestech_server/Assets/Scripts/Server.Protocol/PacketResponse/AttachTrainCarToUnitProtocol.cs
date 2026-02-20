using System;
using Core.Item.Interface;
using Core.Master;
using Game.PlayerInventory.Interface;
using Game.Train.Event;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class AttachTrainCarToUnitProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:attachTrainCarToUnit";
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly TrainUpdateService _trainUpdateService;
        private readonly ITrainUnitSnapshotNotifyEvent _trainUnitSnapshotNotifyEvent;

        public AttachTrainCarToUnitProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _railGraphDatastore = serviceProvider.GetService<IRailGraphDatastore>();
            _trainUpdateService = serviceProvider.GetService<TrainUpdateService>();
            _trainUnitSnapshotNotifyEvent = serviceProvider.GetService<ITrainUnitSnapshotNotifyEvent>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            var request = MessagePackSerializer.Deserialize<AttachTrainCarToUnitRequestMessagePack>(payload);
            return ExecuteRequest(request);

            #region Internal

            AttachTrainCarToUnitResponseMessagePack ExecuteRequest(AttachTrainCarToUnitRequestMessagePack data)
            {
                // リクエストを検証する
                // Validate request payload
                if (data == null || data.RailPosition == null || data.TargetTrainInstanceId == TrainInstanceId.Empty)
                {
                    return AttachTrainCarToUnitResponseMessagePack.CreateFailure(AttachTrainCarFailureType.InvalidRequest);
                }

                // 手持ちアイテムを取得して検証する
                // Resolve and validate held item
                var inventoryData = _playerInventoryDataStore.GetInventoryData(data.PlayerId);
                var mainInventory = inventoryData.MainOpenableInventory;
                var item = mainInventory.GetItem(data.InventorySlot);
                if (item == null || item.Count <= 0)
                {
                    return AttachTrainCarToUnitResponseMessagePack.CreateFailure(AttachTrainCarFailureType.ItemNotFound);
                }

                // 連結先編成を解決する
                // Resolve target train unit
                if (!TryResolveTargetTrain(data.TargetTrainInstanceId, out var targetTrain))
                {
                    return AttachTrainCarToUnitResponseMessagePack.CreateFailure(AttachTrainCarFailureType.TrainNotFound);
                }

                // 連結位置と車両マスターを検証して新規車両を生成する
                // Validate attach position/master and create a car
                if (!TryCreateCarAndRailPosition(item.Id, data, out var attachingCar, out var attachingRailPosition, out var failureType))
                {
                    return AttachTrainCarToUnitResponseMessagePack.CreateFailure(failureType);
                }

                // 接続端点を判定してhead/rearへ連結する
                // Detect endpoint and attach to head/rear
                if (!TryAttachToTargetTrain(targetTrain, attachingCar, attachingRailPosition))
                {
                    return AttachTrainCarToUnitResponseMessagePack.CreateFailure(AttachTrainCarFailureType.InvalidRailPosition);
                }

                // 在庫消費と単機スナップショット通知を行う
                // Consume inventory and notify per-unit snapshot
                mainInventory.SetItem(data.InventorySlot, item.Id, item.Count - 1);
                _trainUnitSnapshotNotifyEvent.NotifySnapshot(targetTrain);

                return AttachTrainCarToUnitResponseMessagePack.CreateSuccess();
            }

            bool TryResolveTargetTrain(TrainInstanceId trainInstanceId, out TrainUnit targetTrain)
            {
                // 登録済み編成から一致IDを探索する
                // Find target by id from registered train units
                targetTrain = null;
                foreach (var train in _trainUpdateService.GetRegisteredTrains())
                {
                    if (train == null || train.TrainInstanceId != trainInstanceId)
                    {
                        continue;
                    }
                    targetTrain = train;
                    return true;
                }
                return false;
            }

            bool TryCreateCarAndRailPosition(
                ItemId trainItemId,
                AttachTrainCarToUnitRequestMessagePack request,
                out TrainCar attachingCar,
                out RailPosition attachingRailPosition,
                out AttachTrainCarFailureType failureType)
            {
                attachingCar = null;
                attachingRailPosition = null;
                failureType = AttachTrainCarFailureType.InvalidRailPosition;

                // 車両マスターを検証する
                // Validate train car master
                if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(trainItemId, out var trainCarMaster))
                {
                    failureType = AttachTrainCarFailureType.ItemNotFound;
                    return false;
                }

                // レール位置を復元し長さ整合を検証する
                // Restore rail position and validate expected length
                var expectedLength = TrainLengthConverter.ToRailUnits(trainCarMaster.Length);
                if (!TryRestoreRailPosition(request.RailPosition, expectedLength, out attachingRailPosition, out failureType))
                {
                    return false;
                }

                // 追加車両を作成する
                // Create attaching car
                attachingCar = new TrainCar(trainCarMaster, request.AttachCarFacingForward);
                return true;
            }

            bool TryAttachToTargetTrain(TrainUnit targetTrain, TrainCar car, RailPosition railPosition)
            {
                if (targetTrain == null || car == null || railPosition == null)
                {
                    return false;
                }

                // head連結条件を満たすか判定する
                // Check if head attach condition is satisfied
                if (targetTrain.RailPosition.GetHeadRailPosition().IsSamePositionAllowNodeOverlap(railPosition.GetRearRailPosition()))
                {
                    targetTrain.AttachCarToHead(car, railPosition);
                    return true;
                }

                // rear連結条件を満たすか判定する
                // Check if rear attach condition is satisfied
                if (railPosition.GetHeadRailPosition().IsSamePositionAllowNodeOverlap(targetTrain.RailPosition.GetRearRailPosition()))
                {
                    targetTrain.AttachCarToRear(car, railPosition);
                    return true;
                }

                return false;
            }

            bool TryRestoreRailPosition(
                RailPositionSnapshotMessagePack snapshot,
                int expectedLength,
                out RailPosition railPosition,
                out AttachTrainCarFailureType failureType)
            {
                railPosition = null;
                failureType = AttachTrainCarFailureType.InvalidRailPosition;

                // スナップショットを保存形式へ変換して検証する
                // Convert snapshot to save-data and validate it
                if (snapshot == null)
                {
                    return false;
                }
                var saveData = snapshot.ToModel();
                if (!TryValidateSnapshot(saveData, expectedLength, out var validatedSaveData, out failureType))
                {
                    return false;
                }

                // 検証済みデータからRailPositionを復元する
                // Restore rail position from validated save-data
                railPosition = RailPositionFactory.Restore(validatedSaveData, _railGraphDatastore);
                return railPosition != null;
            }

            bool TryValidateSnapshot(
                RailPositionSaveData snapshot,
                int expectedTrainLength,
                out RailPositionSaveData validatedSnapshot,
                out AttachTrainCarFailureType failureType)
            {
                validatedSnapshot = null;
                failureType = AttachTrainCarFailureType.InvalidRailPosition;

                // 入力値と列車長を検証する
                // Validate input payload and train length
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

                // ノード列を解決して経路整合性を検証する
                // Resolve rail nodes and validate path consistency
                var nodes = new System.Collections.Generic.List<IRailNode>(snapshot.RailSnapshot.Count);
                for (var i = 0; i < snapshot.RailSnapshot.Count; i++)
                {
                    var node = _railGraphDatastore.ResolveRailNode(snapshot.RailSnapshot[i]);
                    if (node == null)
                    {
                        failureType = AttachTrainCarFailureType.RailNotFound;
                        return false;
                    }
                    nodes.Add(node);
                }

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

            #endregion
        }

        #region MessagePack

        [MessagePackObject]
        public class AttachTrainCarToUnitRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public TrainInstanceId TargetTrainInstanceId { get; set; }
            [Key(3)] public RailPositionSnapshotMessagePack RailPosition { get; set; }
            [Key(4)] public int HotBarSlot { get; set; }
            [IgnoreMember] public int InventorySlot => PlayerInventoryConst.HotBarSlotToInventorySlot(HotBarSlot);
            [Key(5)] public int PlayerId { get; set; }
            [Key(6)] public bool AttachCarFacingForward { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public AttachTrainCarToUnitRequestMessagePack()
            {
                Tag = ProtocolTag;
            }

            public AttachTrainCarToUnitRequestMessagePack(
                TrainInstanceId targetTrainInstanceId,
                RailPositionSnapshotMessagePack railPosition,
                int hotBarSlot,
                int playerId,
                bool attachCarFacingForward)
            {
                Tag = ProtocolTag;
                TargetTrainInstanceId = targetTrainInstanceId;
                RailPosition = railPosition;
                HotBarSlot = hotBarSlot;
                PlayerId = playerId;
                AttachCarFacingForward = attachCarFacingForward;
            }
        }

        [MessagePackObject]
        public class AttachTrainCarToUnitResponseMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public AttachTrainCarFailureType FailureType { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public AttachTrainCarToUnitResponseMessagePack()
            {
                Tag = ProtocolTag;
            }

            public static AttachTrainCarToUnitResponseMessagePack CreateSuccess()
            {
                return new AttachTrainCarToUnitResponseMessagePack
                {
                    Success = true,
                    FailureType = AttachTrainCarFailureType.None
                };
            }

            public static AttachTrainCarToUnitResponseMessagePack CreateFailure(AttachTrainCarFailureType failureType)
            {
                return new AttachTrainCarToUnitResponseMessagePack
                {
                    Success = false,
                    FailureType = failureType
                };
            }
        }

        public enum AttachTrainCarFailureType
        {
            None = 0,
            InvalidRequest = 1,
            RailNotFound = 2,
            ItemNotFound = 3,
            InvalidRailPosition = 4,
            TrainNotFound = 5
        }

        #endregion
    }
}
