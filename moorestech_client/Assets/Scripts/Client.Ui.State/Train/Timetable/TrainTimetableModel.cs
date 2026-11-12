using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Train.Timetable;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Ui.State.Diagnostics;
using Client.Ui.State.Train.Logic;
using Game.Block.Blocks.TrainRail;
using UniRx;
using UnityEngine;
using VContainer.Unity;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Ui.State.Train.Timetable
{
    /// <summary>
    /// 開いている貨車の時刻表を Web ホスト TrainTimetableDtoBuilder.Build と同じ優先で組む（D-2）
    /// Builds the open car's timetable with the same priority as the Web host TrainTimetableDtoBuilder.Build (D-2)
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
        private readonly AnomalyReportGate _missingCarGate = new();
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
                : TrainTimetableViewState.Unavailable;
            // silent-ok: 中身が同じ状態は配り直さない / 表示が変わらないので見ている人に差は無く、行の作り直しも起きない（R-5）
            if (TrainTimetableViewState.IsSameState(_current.Value, next)) return;
            _current.Value = next;
        }

        private TrainTimetableViewState Build(long trainCarInstanceId)
        {
            // 優先順: 列車不明>不可>取得中>無>用意済
            // Priority: no train, unavailable, in flight, no cache, ready
            if (!OpenTrainUnitResolver.TryResolveOwningTrain(trainCarInstanceId, _cache, out var trainUnitInstanceId))
            {
                // 同じ貨車が欠けている間は 1 回だけ警告する
                // Warn once while the same car stays missing
                var missingCar = $"[TrainTimetableModel] Missing car snapshot: {trainCarInstanceId}";
                if (_missingCarGate.ShouldReport(missingCar)) Debug.LogWarning(missingCar);
                return TrainTimetableViewState.Unavailable;
            }
            _missingCarGate.ShouldReport(null);
            if (_fetcher.IsUnavailable(trainUnitInstanceId)) return TrainTimetableViewState.Unavailable;
            if (_fetcher.IsFetchInFlight(trainUnitInstanceId)) return TrainTimetableViewState.Loading;
            if (!_timetables.TryGet(trainUnitInstanceId, out var timetable)) return TrainTimetableViewState.Loading;
            return _readyDataFactory.TryCreate(timetable, ReadStations(), out var data)
                ? TrainTimetableViewState.Ready(data)
                : TrainTimetableViewState.Unavailable;
        }

        // 駅ブロックの位置と駅名（サーバーの StateDetail。届く前は空名で並べる。Web ホストと同じ）
        // Station block positions and names (a server StateDetail; empty until it arrives, same as the Web host)
        private List<TrainTimetableStation> ReadStations()
        {
            var stations = new List<TrainTimetableStation>();
            foreach (var block in _blocks.GetBlocksByBlockType(BlockTypeConst.TrainStation))
            {
                var name = block.GetStateDetail<TrainStationNameStateDetail>(TrainStationNameStateDetail.BlockStateDetailKey)?.StationName;
                // silent-ok: 駅名の StateDetail が届く前は空名で並べ、無名の駅の文言で出す（Web ホストと同じ）/ 届かないままなら駅一覧が無名のままで、録画 train/train-timetable-via-ui の「1駅目は北駅」の主張が落ちて開発者が気づく
                stations.Add(new TrainTimetableStation(block.BlockPosInfo.OriginalPos, name ?? string.Empty));
            }
            return TrainTimetableStationCatalog.Sorted(stations);
        }
    }
}
