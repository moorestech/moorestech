using Client.Game.InGame.Context;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Timetable
{
    // 時刻表イベントを購読しデータストアへ反映する（tickバッファを通さない）
    // Subscribe to timetable events and apply them to the datastore, bypassing the tick buffer
    public class TrainTimetableEventHandler : IInitializable
    {
        private readonly IClientTrainTimetableMutator _datastore;

        public TrainTimetableEventHandler(IClientTrainTimetableMutator datastore)
        {
            _datastore = datastore;
        }

        public void Initialize()
        {
            ClientContext.VanillaApi.Event.SubscribeEventResponse(TrainTimetableEventPacket.EventTag, payload =>
            {
                _datastore.Apply(MessagePackSerializer.Deserialize<TrainTimetableMessagePack>(payload));
            });
        }
    }
}
