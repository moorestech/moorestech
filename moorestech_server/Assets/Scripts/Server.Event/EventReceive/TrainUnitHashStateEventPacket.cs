using System.Collections.Generic;
using Game.Train.RailGraph;
using Game.Train.Unit;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // TrainUnit/RailGraphのハッシュとTickを定期送信する
    // Periodically broadcasts train/rail hash state per tick
    public sealed class TrainUnitHashStateEventPacket
    {
        public const string EventTag = "va:event:trainUnitHashState";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly TrainUpdateService _trainUpdateService;

        public TrainUnitHashStateEventPacket(
            EventProtocolProvider eventProtocolProvider,
            IRailGraphDatastore railGraphDatastore,
            TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _railGraphDatastore = railGraphDatastore;
            _trainUpdateService = trainUpdateService;
            // 各tickのハッシュ通知に追従して配信する
            // Broadcast on every hash notification tick
            _trainUpdateService.OnHashEvent.Subscribe(BroadcastHashState);
        }

        #region Internal

        private void BroadcastHashState(uint tick)
        {
            // TrainUnit/RailGraphのハッシュを同一tickで計算して送信する
            // Compute train/rail hashes and broadcast with the same tick
            var bundles = new List<TrainUnitSnapshotBundle>();
            foreach (var train in _trainUpdateService.GetRegisteredTrains())
            {
                bundles.Add(TrainUnitSnapshotFactory.CreateSnapshot(train));
            }

            var unitsHash = TrainUnitSnapshotHashCalculator.Compute(bundles);
            var railGraphHash = _railGraphDatastore.GetConnectNodesHash();
            var tickSequenceId = _trainUpdateService.NextTickSequenceId();
            var payload = MessagePackSerializer.Serialize(new TrainUnitHashStateMessagePack(unitsHash, railGraphHash, tick, tickSequenceId));
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }

        #endregion
    }
}
