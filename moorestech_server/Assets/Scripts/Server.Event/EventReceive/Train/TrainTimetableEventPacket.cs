using Game.Context;
using Game.Train.Event;
using Game.Train.Unit;
using MessagePack;
using Server.Event.EventReceive.Train;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // 時刻表・自動運転の変化を、その列車を開いているプレイヤーだけへtick番号なしで配る
    // Send timetable and auto-run changes, without a tick number, only to players viewing that train
    public class TrainTimetableEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:trainTimetable";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly ITrainTimetableNotifyEvent _timetableNotifyEvent;
        private readonly ITrainTimetableSubscriptionRegistry _subscriptionRegistry;

        public TrainTimetableEventPacket(
            EventProtocolProvider eventProtocolProvider,
            ITrainTimetableNotifyEvent timetableNotifyEvent,
            ITrainTimetableSubscriptionRegistry subscriptionRegistry)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _timetableNotifyEvent = timetableNotifyEvent;
            _subscriptionRegistry = subscriptionRegistry;
        }

        public void Load()
        {
            _timetableNotifyEvent.OnTimetableChanged.Subscribe(trainUnit =>
            {
                var playerIds = _subscriptionRegistry.GetSubscribers(trainUnit);
                // 誰も開いていない列車は配信先が無い。購読の有無による通常の絞り込みなのでログは出さない
                // A train nobody is viewing has no recipient; this is ordinary subscription filtering, not a failure
                if (playerIds.Count == 0) return;
                var payload = MessagePackSerializer.Serialize(new TrainTimetableMessagePack(TrainTimetableSnapshotFactory.Create(trainUnit)));
                foreach (var playerId in playerIds)
                {
                    _eventProtocolProvider.AddEvent(playerId, EventTag, payload);
                }
            });
        }
    }
}
