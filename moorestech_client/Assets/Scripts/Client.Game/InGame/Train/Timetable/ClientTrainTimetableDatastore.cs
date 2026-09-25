using System;
using System.Collections.Generic;
using Game.Train.Unit;
using UniRx;

namespace Client.Game.InGame.Train.Timetable
{
    // 読み取り専用の消費者に公開する口（Apply不可）
    // The surface exposed to read-only consumers (no Apply)
    public interface IClientTrainTimetableLookup
    {
        IObservable<TrainUnitInstanceId> OnTimetableUpdated { get; }
        bool TryGet(TrainUnitInstanceId trainUnitInstanceId, out TrainTimetableSnapshot timetable);
    }

    // 変更を書き込む消費者だけに公開する口
    // The surface exposed only to consumers that write updates
    public interface IClientTrainTimetableMutator
    {
        void Apply(TrainTimetableSnapshot timetable);
    }

    // サーバーから届いた列車ごとの時刻表をUI用に保持する（走行計算は参照しない）
    // Hold per-train timetables from the server for the UI; motion simulation never reads this
    public class ClientTrainTimetableDatastore : IClientTrainTimetableLookup, IClientTrainTimetableMutator
    {
        private readonly Dictionary<TrainUnitInstanceId, TrainTimetableSnapshot> _timetables = new();
        private readonly Subject<TrainUnitInstanceId> _onTimetableUpdated = new();
        public IObservable<TrainUnitInstanceId> OnTimetableUpdated => _onTimetableUpdated;

        public void Apply(TrainTimetableSnapshot timetable)
        {
            _timetables[timetable.TrainUnitInstanceId] = timetable;
            _onTimetableUpdated.OnNext(timetable.TrainUnitInstanceId);
        }

        public bool TryGet(TrainUnitInstanceId trainUnitInstanceId, out TrainTimetableSnapshot timetable)
        {
            return _timetables.TryGetValue(trainUnitInstanceId, out timetable);
        }
    }
}
