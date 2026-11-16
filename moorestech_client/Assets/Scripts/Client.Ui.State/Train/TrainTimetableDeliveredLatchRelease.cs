using System;
using Client.Game.InGame.Train.Timetable;
using Game.Train.Unit;
using UniRx;
using UnityEngine;

namespace Client.Ui.State.Train
{
    // 時刻表が届いたらラッチを下ろし通知する
    // On delivery, lower the latches and notify
    internal sealed class TrainTimetableDeliveredLatchRelease
    {
        private readonly IClientTrainTimetableLookup _timetableLookup;
        private readonly TrainTimetableFetchLatch _latch;
        private readonly Subject<UniRx.Unit> _onReleased = new();
        public IObservable<UniRx.Unit> OnReleased => _onReleased;

        public TrainTimetableDeliveredLatchRelease(IClientTrainTimetableLookup timetableLookup, TrainTimetableFetchLatch latch)
        {
            _timetableLookup = timetableLookup;
            _latch = latch;
        }

        public void OnTimetableDelivered(TrainUnitInstanceId deliveredTrainUnitInstanceId)
        {
            // 削除でも同じ通知が飛ぶため、実体がある時だけ解除する
            // Removal raises the same notification, so only an existing timetable releases the latches
            if (!_timetableLookup.TryGet(deliveredTrainUnitInstanceId, out _)) return; // silent-ok: 削除通知では取得ラッチを解除しない / タブが取得中か取得不可を表示する
            var releasedUnavailable = _latch.TryReleaseUnavailable(deliveredTrainUnitInstanceId);
            if (releasedUnavailable) Debug.Log($"[TrainTimetableFetcher] timetable delivered; releasing unavailable latch: {deliveredTrainUnitInstanceId}");
            var releasedInFlight = _latch.TryReleaseInFlight(deliveredTrainUnitInstanceId);
            if (releasedInFlight) Debug.Log($"[TrainTimetableFetcher] timetable delivered while fetching; releasing in-flight latch: {deliveredTrainUnitInstanceId}");
            // ラッチを下ろしたら購読側に組み直させる
            // A subscriber rebuilds once a latch was lowered
            if (releasedUnavailable || releasedInFlight) _onReleased.OnNext(UniRx.Unit.Default);
        }
    }
}
