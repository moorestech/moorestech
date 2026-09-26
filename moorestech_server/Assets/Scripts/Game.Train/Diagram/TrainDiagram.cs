using Game.Train.RailGraph;
using System;
using System.Collections.Generic;
using Game.Train.Unit;

namespace Game.Train.Diagram
{
    public class TrainDiagram
    {
        private readonly List<TrainDiagramEntry> _entries;
        private int _currentIndex;
        private ITrainDiagramContext _context;
        private readonly IRailGraphProvider _railGraphProvider;
        private readonly TrainDiagramManager _diagramManager;

        public IReadOnlyList<TrainDiagramEntry> Entries => _entries;
        public int CurrentIndex => _currentIndex;

        public enum DepartureConditionType
        {
            TrainInventoryFull,
            TrainInventoryEmpty,
            WaitForTicks
        }

        public TrainDiagram(IRailGraphProvider railGraphProvider, TrainDiagramManager diagramManager)
        {
            // レールグラフプロバイダを保持する
            // Keep the rail graph provider reference
            _railGraphProvider = railGraphProvider;
            _diagramManager = diagramManager;
            _entries = new List<TrainDiagramEntry>();
            _currentIndex = -1;
            _diagramManager.RegisterDiagram(this);
        }
        public void OnDestroy()
        {
            _diagramManager.UnregisterDiagram(this);
            _entries.Clear();
        }

        internal void RestoreState(TrainDiagramSaveData saveData)
        {
            _entries.Clear();
            _currentIndex = -1;
            TrainDiagramSaveDataConverter.Restore(this, saveData, _railGraphProvider);
        }

        internal void SetRestoredEntries(List<TrainDiagramEntry> entries, int currentIndex)
        {
            _entries.AddRange(entries);
            _currentIndex = currentIndex;
        }

        internal void SetContext(ITrainDiagramContext context)
        {
            _context = context;
        }

        public TrainDiagramEntry AddEntry(IRailNode node)
        {
            var entry = TrainDiagramEntryOperations.Add(_entries, ref _currentIndex, node);
            NotifyTimetableChanged();
            return entry;
        }

        public TrainDiagramEntry AddEntry(IRailNode node, DepartureConditionType departureConditionType, int waitTicks)
        {
            var entry = TrainDiagramEntryOperations.Add(_entries, ref _currentIndex, node, departureConditionType, waitTicks);
            NotifyTimetableChanged();
            return entry;
        }

        public TrainDiagramEntry InsertEntry(int index, IRailNode node)
        {
            var entry = TrainDiagramEntryOperations.Insert(_entries, ref _currentIndex, index, node);
            NotifyTimetableChanged();
            return entry;
        }

        // 時刻表を丸ごと置き換え、現在地を先頭へ戻す。公開入口はTrainUnit.ReplaceTimetableだけ
        // Replace the whole timetable and reset the cursor; TrainUnit.ReplaceTimetable is the only public entry
        internal void ReplaceEntries(IReadOnlyList<TrainDiagramStopPlan> stops)
        {
            _entries.Clear();
            _currentIndex = -1;
            foreach (var stop in stops)
            {
                TrainDiagramEntryOperations.Add(_entries, ref _currentIndex, stop.Node, stop.DepartureConditionType, stop.WaitTicks);
            }
            NotifyTimetableChanged();
        }

        public void Update()
        {
            TrainDiagramEntryOperations.Tick(_entries, ref _currentIndex, _context);
        }

        public bool CanCurrentEntryDepart()
        {
            return TrainDiagramEntryOperations.CanDepart(_entries, ref _currentIndex, _context);
        }

        public IRailNode GetCurrentNode()
        {
            return TryGetActiveEntry(out var entry) ? entry.Node : null;
        }
        // 現在のエントリIDを返す
        // Return the current entry ID
        public Guid GetCurrentGuid()
        {
            return TryGetActiveEntry(out var entry) ? entry.entryId : Guid.Empty;
        }

        public void MoveToNextEntry()
        {
            var previousIndex = _currentIndex;
            _currentIndex = _entries.Count == 0 ? -1 : (_currentIndex + 1) % _entries.Count;
            if (previousIndex == _currentIndex)
            {
                return;
            }
            NotifyTimetableChanged();
        }

        // 出発時に現在entryの状態を初期化してから次entryへ移動する。
        // Reset current entry state on departure, then move to the next entry.
        public void NextEntryAndDepartureReset()
        {
            if (!TryGetActiveEntry(out var currentEntry))
            {
                return;
            }
            currentEntry.OnDeparted();
            MoveToNextEntry();
        }

        // ノード削除時に現在地を補正する
        // Adjust the cursor when a rail node is removed
        // 不変条件: ノード削除時に必ず本メソッドが呼ばれるため _entries は常に実在ノードのみを保持する
        // Invariant: this is always called on node removal, so _entries only ever holds live nodes
        public void HandleNodeRemoval(IRailNode removedNode)
        {
            if (removedNode == null)
            {
                UnityEngine.Debug.LogWarning("[TrainDiagram] Cannot remove a null rail node.");
                return;
            }
            _currentIndex = TrainDiagramNodeRemoval.Remove(
                _entries, _currentIndex, removedNode, out var removedAny, out var currentRemoved);
            if (removedAny)
            {
                if (currentRemoved && (0 < _entries.Count))
                {
                    _context?.OnCurrentEntryShiftedByRemoval();
                }
                NotifyTimetableChanged();
            }
        }

        // 時刻表の変化を購読側へその場で押し出す
        // Push a timetable change to the subscriber at the moment it happens
        private void NotifyTimetableChanged()
        {
            _context?.OnTimetableChanged();
        }

        public TrainDiagramEntry GetCurrentEntry()
        {
            return TryGetActiveEntry(out var entry) ? entry : null;
        }

        private bool TryGetActiveEntry(out TrainDiagramEntry entry)
        {
            entry = null;
            if ((_currentIndex < 0) || (_entries.Count == 0) || (_entries.Count <= _currentIndex))
            {
                return false;
            }
            entry = _entries[_currentIndex];
            return true;
        }

        // 到着時に出発条件をリセットする
        // Reset departure conditions on arrival
        public void ResetCurrentEntryDepartureConditions()
        {
            if (TryGetActiveEntry(out var entry))
            {
                entry.OnDeparted();
            }
        }

        public TrainDiagramSaveData CreateTrainDiagramSaveData()
        {
            return TrainDiagramSaveDataConverter.Create(this);
        }
    }
}
