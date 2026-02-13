using Game.Train.Event;
using Game.Train.Unit;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // TrainCar削除をクライアントへ通知するイベントパケット
    // Event packet that broadcasts removed train cars.
    public sealed class TrainCarRemovedEventPacket
    {
        public const string EventTag = "va:event:trainCarRemoved";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly TrainUpdateService _trainUpdateService;

        public TrainCarRemovedEventPacket(EventProtocolProvider eventProtocolProvider, ITrainUpdateEvent trainUpdateEvent, TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _trainUpdateService = trainUpdateService;
            trainUpdateEvent.OnTrainCarRemoved.Subscribe(OnTrainCarRemoved);
        }

        #region Internal

        private void OnTrainCarRemoved(TrainCarInstanceId trainCarInstanceId)
        {
            // 現在の列車tickと削除対象車両IDをまとめて配信する
            // Broadcast removed car id with the current train tick.
            var payload = MessagePackSerializer.Serialize(
                new TrainCarRemovedEventMessagePack(trainCarInstanceId.AsPrimitive(), _trainUpdateService.GetCurrentTick()));
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }

        [MessagePackObject]
        private sealed class TrainCarRemovedEventMessagePack
        {
            [Key(0)] public long TrainCarInstanceId { get; set; }
            [Key(1)] public long ServerTick { get; set; }

            [System.Obsolete("Reserved for MessagePack.")]
            public TrainCarRemovedEventMessagePack()
            {
            }

            public TrainCarRemovedEventMessagePack(long trainCarInstanceId, long serverTick)
            {
                TrainCarInstanceId = trainCarInstanceId;
                ServerTick = serverTick;
            }
        }

        #endregion
    }
}
