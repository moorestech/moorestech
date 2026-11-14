using Client.Game.InGame.Block;
using Client.Game.InGame.Train.Timetable;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Ui.State.Train.Logic;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// 開いている貨車の時刻表を組む（駅一覧と用意済みの組み立ては Web ホスト TrainTimetableDtoBuilder.Build と同じ State の口）
    /// Builds the open car's timetable (the station list and ready data come from the same State calls as the Web host TrainTimetableDtoBuilder.Build)
    /// </summary>
    public sealed class TrainTimetableModel : ITrainTimetableSource, IInitializable
    {
        private readonly SubInventoryState _subInventoryState;
        private readonly UIStateControl _uiStateControl;
        private readonly TrainUnitClientCache _cache;
        private readonly IClientTrainTimetableLookup _timetables;
        private readonly TrainTimetableFetcher _fetcher;
        private readonly BlockGameObjectDataStore _blocks;
        private readonly OpenTrainTimetableTracker _tracker;
        private readonly TrainTimetableReadyDataFactory _readyDataFactory = new();
        private readonly TrainTimetableMissingCarReport _missingCarReport = new();
        private readonly ReactiveProperty<TrainTimetableViewState> _current = new(TrainTimetableViewState.Unavailable);

        public IReadOnlyReactiveProperty<TrainTimetableViewState> Current => _current;

        public TrainTimetableModel(SubInventoryState subInventoryState, UIStateControl uiStateControl, TrainUnitClientCache cache, IClientTrainTimetableLookup timetables, TrainTimetableFetcher fetcher, BlockGameObjectDataStore blocks, OpenTrainTimetableTracker tracker)
        {
            _subInventoryState = subInventoryState;
            _uiStateControl = uiStateControl;
            _cache = cache;
            _timetables = timetables;
            _fetcher = fetcher;
            _blocks = blocks;
            _tracker = tracker;
        }

        public void Initialize()
        {
            // Web ホスト BlockInventoryTopic と同じ契機: 画面の開閉（閉じると使えないへ倒れ、編集は閉じる）・中身の更新・時刻表の到着
            // Same triggers as the Web host BlockInventoryTopic: screen open/close (closing falls to unavailable and closes the edit), contents updates, timetable arrival
            // UIStateEnum は契機だけで、判定は SubInventory の中身の源（列車）なので UiScreen を通さない（A-248）
            // UIStateEnum is only the trigger; the decision is the SubInventory content source (a train), so UiScreen is not consulted (A-248)
            _uiStateControl.OnStateChanged.Subscribe(_ => Refresh());
            _subInventoryState.OnSubInventoryUpdated.Subscribe(_ => Refresh());
            _timetables.OnTimetableUpdated.Subscribe(_ => Refresh());
            _tracker.OnRepublishRequested.Subscribe(_ => Refresh());
            // 取得の開始は UITK だけが拾い、依頼の直後に読込中を出す（Web ホストの配信は変えない。D-20）
            // Only UITK picks up fetch start to show Loading right after the request (the Web host publishing is unchanged; D-20)
            _fetcher.OnFetchStarted.Subscribe(_ => Refresh());
            // ラッチが下りた後の通知を拾い、取得不可・取得中の古い値で固まらないようにする
            // Pick up the latch release so the model does not stay on a stale unavailable or in-flight value
            _fetcher.OnFetchLatchReleased.Subscribe(_ => Refresh());
            Refresh();
        }

        private void Refresh()
        {
            var next = _subInventoryState.CurrentSubInventorySource is TrainSubInventorySource source
                ? Build(source.TrainCarInstanceId)
                : NoTrainOpen();
            // silent-ok: 中身が同じ状態は配り直さない / 表示が変わらないので見ている人に差は無く、行の作り直しも起きない（R-5）
            if (TrainTimetableViewState.IsSameState(_current.Value, next)) return;
            _current.Value = next;

            #region Internal

            TrainTimetableViewState Build(long trainCarInstanceId)
            {
                // 優先順: 列車不明>不可>取得中>無>用意済
                // Priority: no train, unavailable, in flight, no cache, ready
                if (!OpenTrainUnitResolver.TryResolveOwningTrain(trainCarInstanceId, _cache, out var trainUnitInstanceId))
                {
                    _missingCarReport.ReportMissing(trainCarInstanceId);
                    return TrainTimetableViewState.Unavailable;
                }
                _missingCarReport.Clear();
                if (_fetcher.IsUnavailable(trainUnitInstanceId)) return TrainTimetableViewState.Unavailable;
                if (_fetcher.IsFetchInFlight(trainUnitInstanceId)) return TrainTimetableViewState.Loading;
                if (!_timetables.TryGet(trainUnitInstanceId, out var timetable)) return TrainTimetableViewState.Loading;
                return _readyDataFactory.TryCreate(timetable, TrainTimetableStationCatalog.ReadSorted(_blocks), out var data)
                    ? TrainTimetableViewState.Ready(data)
                    : TrainTimetableViewState.Unavailable;
            }

            #endregion
        }

        private TrainTimetableViewState NoTrainOpen()
        {
            // 貨車が無い間は旗を戻し再警告に備える
            // Clear flag while no car so it warns again
            _missingCarReport.Clear();
            return TrainTimetableViewState.Unavailable;
        }
    }
}
