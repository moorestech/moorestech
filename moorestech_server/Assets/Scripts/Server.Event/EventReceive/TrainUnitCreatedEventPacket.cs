using System;
using Game.Entity.Interface;
using Game.Train.Unit;
using Game.Train.Unit;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;

namespace Server.Event.EventReceive
{
    /// <summary>
    ///     TrainUnit生成イベントをブロードキャストするパケット
    ///     Event packet that broadcasts newly created train units
    /// </summary>
    public sealed class TrainUnitCreatedEventPacket
    {
        public const string EventTag = "va:event:trainUnitCreated";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly TrainUpdateService _trainUpdateService;

        public TrainUnitCreatedEventPacket(EventProtocolProvider eventProtocolProvider, TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _trainUpdateService = trainUpdateService;
            // 列車生成イベントを購読する
            // Subscribe to train unit creation events
            _trainUpdateService.GetTrainUnitCreatedEvent().Subscribe(OnTrainUnitCreated);
        }

        private void OnTrainUnitCreated(TrainUnitInitializationNotifier.TrainUnitCreatedData data)
        {
            // 列車生成差分をブロードキャストする
            // Broadcast the train unit creation diff
            var trainUnit = data.TrainUnit;
            var snapshot = TrainUnitSnapshotFactory.CreateSnapshot(trainUnit);
            var snapshotPack = new TrainUnitSnapshotBundleMessagePack(snapshot);
            var entities = BuildTrainEntities(trainUnit);
            var message = new TrainUnitCreatedEventMessagePack(snapshotPack, entities, _trainUpdateService.GetCurrentTick());
            var payload = MessagePackSerializer.Serialize(message);
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }

        private EntityMessagePack[] BuildTrainEntities(TrainUnit trainUnit)
        {
            // 列車車両ごとのエンティティ情報を作成する
            // Build entity messages for each train car
            var cars = trainUnit.Cars;
            if (cars.Count == 0)
            {
                return Array.Empty<EntityMessagePack>();
            }
            var entities = new EntityMessagePack[cars.Count];
            for (var i = 0; i < cars.Count; i++)
            {
                var car = cars[i];
                var entityId = CreateTrainEntityInstanceId(car.CarId);
                var state = new TrainEntityStateMessagePack(car.CarId, car.TrainCarMasterElement.TrainCarGuid);
                entities[i] = new EntityMessagePack
                {
                    InstanceId = entityId,
                    Type = VanillaEntityType.VanillaTrain,
                    Position = new Vector3MessagePack(Vector3.zero),
                    EntityData = MessagePackSerializer.Serialize(state)
                };
            }
            return entities;
        }

        // 車両Guidから安定したInstanceIdを生成する
        // Generate a stable instance id from the train car Guid
        private static long CreateTrainEntityInstanceId(Guid trainCarId)
        {
            var bytes = trainCarId.ToByteArray();
            var low = BitConverter.ToInt64(bytes, 0);
            var high = BitConverter.ToInt64(bytes, 8);
            return low ^ high;
        }
    }
}
