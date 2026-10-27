using System;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Game.Train.Unit;
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Timetable
{
    // 開いている車両の所属列車を追い、再配信と再取得をまとめる
    // Track the open car's owning train and gather timetable republish triggers and refetches
    public sealed class OpenTrainTimetableTracker : IInitializable
    {
        private readonly SubInventoryState _subInventoryState;
        private readonly UIStateControl _uiStateControl;
        private readonly TrainUnitClientCache _trainUnitClientCache;
        private readonly IClientTrainTimetableLookup _timetables;
        private readonly TrainTimetableFetcher _fetcher;
        private readonly Subject<UniRx.Unit> _onRepublishRequested = new();
        private TrainUnitInstanceId? _openTrainUnitInstanceId;
        public IObservable<UniRx.Unit> OnRepublishRequested => _onRepublishRequested;

        public OpenTrainTimetableTracker(SubInventoryState subInventoryState, UIStateControl uiStateControl, TrainUnitClientCache trainUnitClientCache, IClientTrainTimetableLookup timetables, TrainTimetableFetcher fetcher)
        {
            _subInventoryState = subInventoryState;
            _uiStateControl = uiStateControl;
            _trainUnitClientCache = trainUnitClientCache;
            _timetables = timetables;
            _fetcher = fetcher;
        }

        public void Initialize()
        {
            // 開閉で取得状態を破棄し再解決
            // Drop fetch state on open/close and re-resolve
            _uiStateControl.OnStateChanged.Subscribe(_ => OnUiStateChanged());
            // 時刻表更新・取得失敗で再配信
            // Republish on timetable updates and fetch failures
            _timetables.OnTimetableUpdated
                .Where(id => _openTrainUnitInstanceId == id)
                .Subscribe(_ => _onRepublishRequested.OnNext(UniRx.Unit.Default));
            _fetcher.OnFetchFailed.Subscribe(_onRepublishRequested.OnNext);
            // 所属列車が変わったら再配信・再取得する
            // Republish and refetch when coupling, splitting, or a late car snapshot changes the owning train
            _trainUnitClientCache.OnUnitApplied.Subscribe(_ => OnTrainUnitApplied());
            _trainUnitClientCache.OnUnitRemoved.Subscribe(_ => OnTrainUnitApplied());

            #region Internal

            void OnUiStateChanged()
            {
                _fetcher.Close();
                _openTrainUnitInstanceId = ResolveOpenTrainUnitInstanceId();
            }

            #endregion
        }

        private void OnTrainUnitApplied()
        {
            var resolved = ResolveOpenTrainUnitInstanceId();
            if (resolved == _openTrainUnitInstanceId) return; // silent-ok: 同じ所属列車なら再取得不要 / 既存の時刻表表示を維持する
            _openTrainUnitInstanceId = resolved;
            if (resolved.HasValue) _fetcher.FollowOpenTrainChange(resolved.Value);
            _onRepublishRequested.OnNext(UniRx.Unit.Default);
        }

        private TrainUnitInstanceId? ResolveOpenTrainUnitInstanceId()
        {
            if (!OpenTrainUnitResolver.TryResolveOpenTrain(_subInventoryState, _trainUnitClientCache, out var trainUnitInstanceId)) return null; // silent-ok: 列車を開いていない状態も正常 / 描画側が列車なしを表示する
            return trainUnitInstanceId;
        }
    }
}
