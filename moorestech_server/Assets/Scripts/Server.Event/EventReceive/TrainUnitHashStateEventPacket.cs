using System.Collections.Generic;
using Game.Train.Unit;
using Game.Train.Unit;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;

namespace Server.Event.EventReceive
{
    // TrainUnitのハッシュとTickを定期送信する
    // Periodically broadcasts the current TrainUnit hash/tick state
    public sealed class TrainUnitHashStateEventPacket
    {
        public const string EventTag = "va:event:trainUnitHashState";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly TrainUpdateService _trainUpdateService;

        public TrainUnitHashStateEventPacket(EventProtocolProvider eventProtocolProvider, TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _trainUpdateService = trainUpdateService;
            // 1秒間隔でTrainUnitハッシュを通知する
            // Broadcast hash/tick every second
            _trainUpdateService.GetOnHashEvent().Subscribe(BroadcastHashState);
        }

        #region Internal

        private void BroadcastHashState(long tick)
        {
            // TrainUnitスナップショットのハッシュを計算して送信する
            // Compute and broadcast the latest TrainUnit hash state
            var bundles = new List<TrainUnitSnapshotBundle>();
            foreach (var train in _trainUpdateService.GetRegisteredTrains())
            {
                bundles.Add(TrainUnitSnapshotFactory.CreateSnapshot(train));
            }

            var hash = TrainUnitSnapshotHashCalculator.Compute(bundles);
            var payload = MessagePackSerializer.Serialize(new TrainUnitHashStateMessagePack(hash, tick));
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }

        #endregion
    }
}

