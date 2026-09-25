using System;
using Client.Game.InGame.Train.Timetable;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Game.Train.Unit;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    // 開いている車両の所属列車を追い、再配信と再取得をまとめる
    // Track the open car's owning train and gather timetable republish triggers and refetches
    public class OpenTrainTimetableTracker : IDisposable
    {
        private readonly SubInventoryState _subInventoryState;
        private readonly UIStateControl _uiStateControl;
        private readonly TrainUnitClientCache _trainUnitClientCache;
        private readonly TrainTimetableFetcher _fetcher;
        private readonly Subject<Unit> _onRepublishRequested = new();
        private readonly CompositeDisposable _subscriptions = new();
        private TrainUnitInstanceId? _openTrainUnitInstanceId;
        public IObservable<Unit> OnRepublishRequested => _onRepublishRequested;

        public OpenTrainTimetableTracker(SubInventoryState subInventoryState, UIStateControl uiStateControl, TrainUnitClientCache trainUnitClientCache, IClientTrainTimetableLookup timetables, TrainTimetableFetcher fetcher)
        {
            _subInventoryState = subInventoryState;
            _uiStateControl = uiStateControl;
            _trainUnitClientCache = trainUnitClientCache;
            _fetcher = fetcher;
            // 開閉で取得状態を忘れ、開いている列車を解決し直す
            // Forget the fetch state on open/close (UI-state transitions) and re-resolve the open train
            _uiStateControl.OnStateChanged += OnUiStateChanged;
            // 開いている列車の時刻表更新と取得失敗で再配信する
            // Republish on the open train's timetable updates and on fetch failures
            timetables.OnTimetableUpdated
                .Where(id => _openTrainUnitInstanceId == id)
                .Subscribe(_ => _onRepublishRequested.OnNext(Unit.Default))
                .AddTo(_subscriptions);
            _fetcher.OnFetchFailed.Subscribe(_onRepublishRequested.OnNext).AddTo(_subscriptions);
            // 所属列車が変わったら再配信・再取得する
            // Republish and refetch when coupling, splitting, or a late car snapshot changes the owning train
            _trainUnitClientCache.OnUnitApplied.Subscribe(_ => OnTrainUnitApplied()).AddTo(_subscriptions);
            _trainUnitClientCache.OnUnitRemoved.Subscribe(_ => OnTrainUnitApplied()).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _uiStateControl.OnStateChanged -= OnUiStateChanged;
            _subscriptions.Dispose();
        }

        private void OnUiStateChanged(UIStateEnum state)
        {
            _fetcher.Close();
            _openTrainUnitInstanceId = ResolveOpenTrainUnitInstanceId();
        }

        private void OnTrainUnitApplied()
        {
            var resolved = ResolveOpenTrainUnitInstanceId();
            if (resolved == _openTrainUnitInstanceId) return;
            _openTrainUnitInstanceId = resolved;
            if (resolved.HasValue) _fetcher.FollowOpenTrainChange(resolved.Value);
            _onRepublishRequested.OnNext(Unit.Default);
        }

        private TrainUnitInstanceId? ResolveOpenTrainUnitInstanceId()
        {
            if (!OpenTrainUnitResolver.TryResolveOpenTrain(_subInventoryState, _trainUnitClientCache, out var trainUnitInstanceId)) return null;
            return trainUnitInstanceId;
        }
    }
}
