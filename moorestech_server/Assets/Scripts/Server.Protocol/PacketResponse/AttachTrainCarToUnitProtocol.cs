using Server.Protocol.PacketResponse.Util.TrainPlacement;
using System;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Game.PlayerInventory.Interface;
using Game.Train.Event;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.TrainModule;
using Server.Protocol.PacketResponse.Util.Construction;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class AttachTrainCarToUnitProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:attachTrainCarToUnit";
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly TrainCarAttachmentPlacement _placement;
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        private readonly ITrainUnitMutationDatastore _trainUnitMutationDatastore;
        private readonly ITrainUnitSnapshotNotifyEvent _trainUnitSnapshotNotifyEvent;
        private readonly IGameUnlockStateDataController _gameUnlockStateDataController;

        public AttachTrainCarToUnitProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _placement = new TrainCarAttachmentPlacement(serviceProvider.GetService<IRailGraphDatastore>());
            _trainUnitLookupDatastore = serviceProvider.GetService<ITrainUnitLookupDatastore>();
            _trainUnitSnapshotNotifyEvent = serviceProvider.GetService<ITrainUnitSnapshotNotifyEvent>();
            _trainUnitMutationDatastore = serviceProvider.GetService<ITrainUnitMutationDatastore>();
            _gameUnlockStateDataController = serviceProvider.GetService<IGameUnlockStateDataController>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            var request = MessagePackSerializer.Deserialize<AttachTrainCarToUnitRequestMessagePack>(payload);
            return ExecuteRequest(request);

            #region Internal

            AttachTrainCarToUnitResponseMessagePack ExecuteRequest(AttachTrainCarToUnitRequestMessagePack data)
            {
                // リクエストを検証する
                // Validate request payload
                if (data == null || data.RailPosition == null || data.TargetTrainUnitInstanceId == TrainUnitInstanceId.Empty)
                {
                    return AttachTrainCarToUnitResponseMessagePack.CreateFailure(AttachTrainCarFailureType.InvalidRequest);
                }

                // 車両マスタとアンロック状態を検証する
                // Validate the train car master and its unlock state
                if (!MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(data.TrainCarGuid, out var trainCarMaster))
                {
                    return AttachTrainCarToUnitResponseMessagePack.CreateFailure(AttachTrainCarFailureType.ItemNotFound);
                }
                if (!_gameUnlockStateDataController.TrainCarUnlockStateInfos[data.TrainCarGuid].IsUnlocked)
                {
                    return AttachTrainCarToUnitResponseMessagePack.CreateFailure(AttachTrainCarFailureType.NotUnlocked);
                }

                // 建設コストの充足をインベントリ横断で検証する
                // Validate construction cost across the whole inventory
                var inventoryData = _playerInventoryDataStore.GetInventoryData(requesterPlayerId);
                var mainInventory = inventoryData.MainOpenableInventory;
                var costItemCounts = ConstructionCostItems.ToItemCounts(trainCarMaster.RequiredItems);
                if (!ConstructionCostService.HasRequiredItems(costItemCounts, mainInventory.InventoryItems))
                {
                    return AttachTrainCarToUnitResponseMessagePack.CreateFailure(AttachTrainCarFailureType.InsufficientItems);
                }

                // 連結先編成を解決する
                // Resolve target train unit
                if (!_trainUnitLookupDatastore.TryGetTrainUnit(data.TargetTrainUnitInstanceId, out var targetTrain))
                {
                    return AttachTrainCarToUnitResponseMessagePack.CreateFailure(AttachTrainCarFailureType.TrainNotFound);
                }

                // 連結位置と車両マスターを検証して新規車両を生成する
                // Validate attach position/master and create a car
                if (!_placement.TryCreateCarAndRailPosition(trainCarMaster, data, out var attachingCar, out var attachingRailPosition, out var failureType))
                {
                    return AttachTrainCarToUnitResponseMessagePack.CreateFailure(failureType);
                }

                // クライアント指定の接続端点(head/rear)で連結する
                // Attach using client-specified target endpoint (head/rear)
                if (!_placement.TryAttachToTargetTrain(targetTrain, attachingCar, attachingRailPosition, data.AttachToTargetTrainHead))
                {
                    return AttachTrainCarToUnitResponseMessagePack.CreateFailure(AttachTrainCarFailureType.InvalidRailPosition);
                }

                // 建設コスト消費と単機スナップショット通知を行う、サーバー側datastoreも更新
                // Consume construction cost and notify per-unit snapshot
                ConstructionCostService.ConsumeRequiredItems(costItemCounts, mainInventory);
                _trainUnitMutationDatastore.RegisterTrain(targetTrain);
                _trainUnitSnapshotNotifyEvent.NotifySnapshot(targetTrain);
                return AttachTrainCarToUnitResponseMessagePack.CreateSuccess();
            }

            #endregion
        }

        #region MessagePack

        [MessagePackObject]
        public class AttachTrainCarToUnitRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public TrainUnitInstanceId TargetTrainUnitInstanceId { get; set; }
            [Key(3)] public RailPositionSnapshotMessagePack RailPosition { get; set; }
            [Key(4)] public Guid TrainCarGuid { get; set; }
            [Key(5)] public bool AttachCarFacingForward { get; set; }
            [Key(6)] public bool AttachToTargetTrainHead { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public AttachTrainCarToUnitRequestMessagePack()
            {
                Tag = ProtocolTag;
            }

            public AttachTrainCarToUnitRequestMessagePack(
                TrainUnitInstanceId targetTrainUnitInstanceId,
                RailPositionSnapshotMessagePack railPosition,
                Guid trainCarGuid,
                bool attachCarFacingForward,
                bool attachToTargetTrainHead)
            {
                Tag = ProtocolTag;
                TargetTrainUnitInstanceId = targetTrainUnitInstanceId;
                RailPosition = railPosition;
                TrainCarGuid = trainCarGuid;

                AttachCarFacingForward = attachCarFacingForward;
                AttachToTargetTrainHead = attachToTargetTrainHead;
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
            TrainNotFound = 5,
            NotUnlocked = 6,
            InsufficientItems = 7,
        }

        #endregion
    }
}
