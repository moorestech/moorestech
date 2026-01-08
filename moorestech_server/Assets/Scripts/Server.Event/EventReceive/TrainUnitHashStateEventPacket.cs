using System;
using System.Collections.Generic;
using Game.Train.Common;
using Game.Train.Train;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // TrainUnitのハッシュとTickを定期送信する
    // Periodically broadcasts the current TrainUnit hash/tick state
    public sealed class TrainUnitHashStateEventPacket : IDisposable
    {
        public const string EventTag = "va:event:trainUnitHashState";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly CompositeDisposable _disposables = new();

        public TrainUnitHashStateEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;

            // 1秒間隔でTrainUnitハッシュを通知する
            // Broadcast hash/tick every second
            TrainUpdateService.OnHashEvent.Subscribe(BroadcastHashState).AddTo(_disposables);
            /*
            Observable.Interval(TimeSpan.FromSeconds(TrainUpdateService.HashBroadcastIntervalSeconds))
                .Subscribe(_ => BroadcastHashState())
                .AddTo(_disposables);
                */
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }

        #region Internal

        private void BroadcastHashState(long tick)
        {
            // TrainUnitスナップショットのハッシュを計算して送信する
            // Compute and broadcast the latest TrainUnit hash state
            var bundles = new List<TrainUnitSnapshotBundle>();
            foreach (var train in TrainUpdateService.Instance.GetRegisteredTrains())
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
