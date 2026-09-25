using Game.Context;
using Game.Train.Event;
using Game.Train.Unit;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // 時刻表・自動運転の変化をtick番号なしで全員へ配る
    // Broadcast timetable and auto-run changes to everyone without a tick number
    public class TrainTimetableEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:trainTimetable";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly ITrainTimetableNotifyEvent _timetableNotifyEvent;

        public TrainTimetableEventPacket(EventProtocolProvider eventProtocolProvider, ITrainTimetableNotifyEvent timetableNotifyEvent)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _timetableNotifyEvent = timetableNotifyEvent;
        }

        public void Load()
        {
            _timetableNotifyEvent.OnTimetableChanged.Subscribe(trainUnit =>
            {
                var message = new TrainTimetableMessagePack(TrainTimetableSnapshotFactory.Create(trainUnit));
                _eventProtocolProvider.AddBroadcastEvent(EventTag, MessagePackSerializer.Serialize(message));
            });
        }
    }
}
