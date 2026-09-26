using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Timetable
{
    // 時刻表イベントをデータストアへ反映する（tickバッファ外）
    // Apply timetable events to the datastore, outside the tick buffer
    public class TrainTimetableEventHandler : IInitializable
    {
        private readonly IClientTrainTimetableMutator _datastore;
        private readonly TrainUnitClientCache _trainUnitClientCache;

        public TrainTimetableEventHandler(IClientTrainTimetableMutator datastore, TrainUnitClientCache trainUnitClientCache)
        {
            _datastore = datastore;
            _trainUnitClientCache = trainUnitClientCache;
        }

        public void Initialize()
        {
            ClientContext.VanillaApi.Event.SubscribeEventResponse(TrainTimetableEventPacket.EventTag, payload =>
            {
                _datastore.Apply(MessagePackSerializer.Deserialize<TrainTimetableMessagePack>(payload).ToModel());
            });

            // 列車が消えたら時刻表も捨てる。残すと古い内容がreadyのまま出続ける
            // Drop the timetable when its train is gone; keeping it shows stale data as ready
            _trainUnitClientCache.OnUnitRemoved.Subscribe(trainUnitInstanceId =>
            {
                Debug.Log($"[TrainTimetable] dropping the timetable of a removed train: {trainUnitInstanceId}");
                _datastore.Remove(trainUnitInstanceId);
            });
        }
    }
}
