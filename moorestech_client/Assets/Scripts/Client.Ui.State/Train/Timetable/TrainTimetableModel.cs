using Client.Game.InGame.Block;
using Client.Game.InGame.Train.Timetable;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// 開いている貨車の時刻表を組む（駅一覧と用意済みの組み立ては State の口を通す）
    /// Builds the open car's timetable (the station list and ready data go through the State calls)
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
            // 再計算の契機: 画面の開閉（閉じると使えないへ倒れ、編集は閉じる）・中身の更新・時刻表の到着
            // Recompute triggers: screen open/close (closing falls to unavailable and closes the edit), contents updates, timetable arrival
            // UIStateEnum は契機だけで、判定は SubInventory の中身の源（列車）なので UiScreen を通さない（A-248）
            // UIStateEnum is only the trigger; the decision is the SubInventory content source (a train), so UiScreen is not consulted (A-248)
            _uiStateControl.OnStateChanged.Subscribe(_ => Refresh());
            _subInventoryState.OnSubInventoryUpdated.Subscribe(_ => Refresh());
            _timetables.OnTimetableUpdated.Subscribe(_ => Refresh());
            _tracker.OnRepublishRequested.Subscribe(_ => Refresh());
            // 取得の開始を拾い、依頼の直後に読込中を出す
            // Pick up fetch start to show Loading right after the request
            _fetcher.OnFetchStarted.Subscribe(_ => Refresh());
            // ラッチが下りた後の通知を拾い、取得不可・取得中の古い値で固まらないようにする
            // Pick up the latch release so the model does not stay on a stale unavailable or in-flight value
            _fetcher.OnFetchLatchReleased.Subscribe(_ => Refresh());
            Refresh();
        }

        private void Refresh()
        {
            var next = _subInventoryState.CurrentSubInventorySource is TrainSubInventorySource source
                ? _readyDataFactory.Build(source.TrainCarInstanceId, _cache, _timetables, _fetcher, _blocks)
                : NoTrainOpen();
            // silent-ok: 中身が同じ状態は配り直さない / 表示が変わらないので見ている人に差は無く、行の作り直しも起きない（R-5）
            if (TrainTimetableViewState.IsSameState(_current.Value, next)) return;
            _current.Value = next;

            #region Internal

            TrainTimetableViewState NoTrainOpen()
            {
                // 貨車が無い間は旗を戻し再警告に備える
                // Clear flag while no car so it warns again
                _readyDataFactory.ClearMissingCarReport();
                return TrainTimetableViewState.Unavailable;
            }

            #endregion
        }
    }
}
