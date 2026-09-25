using System;
using System.Collections.Generic;
using Game.Train.Unit;
using Server.Util.MessagePack;
using UniRx;

namespace Client.Game.InGame.Train.Timetable
{
    // サーバーから届いた列車ごとの時刻表をUI用に保持する（走行計算は参照しない）
    // Hold per-train timetables from the server for the UI; motion simulation never reads this
    public class ClientTrainTimetableDatastore
    {
        private readonly Dictionary<TrainUnitInstanceId, TrainTimetableMessagePack> _timetables = new();
        private readonly Subject<TrainUnitInstanceId> _onTimetableUpdated = new();
        public IObservable<TrainUnitInstanceId> OnTimetableUpdated => _onTimetableUpdated;

        public void Apply(TrainTimetableMessagePack message)
        {
            _timetables[message.TrainUnitInstanceId] = message;
            _onTimetableUpdated.OnNext(message.TrainUnitInstanceId);
        }

        public bool TryGet(TrainUnitInstanceId trainUnitInstanceId, out TrainTimetableMessagePack message)
        {
            return _timetables.TryGetValue(trainUnitInstanceId, out message);
        }
    }
}
