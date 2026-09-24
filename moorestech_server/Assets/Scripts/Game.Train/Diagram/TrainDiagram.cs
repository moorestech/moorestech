using Core.Update;
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
        private bool _isCurrentEntryChanged;

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
            _isCurrentEntryChanged = false;
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
            return TrainDiagramEntryOperations.Add(_entries, ref _currentIndex, node);
        }

        public TrainDiagramEntry AddEntry(IRailNode node, DepartureConditionType departureConditionType, int waitTicks)
        {
            return TrainDiagramEntryOperations.Add(_entries, ref _currentIndex, node, departureConditionType, waitTicks);
        }

        public TrainDiagramEntry InsertEntry(int index, IRailNode node)
        {
            return TrainDiagramEntryOperations.Insert(_entries, ref _currentIndex, index, node);
        }

        // 時刻表を丸ごと置き換え、現在地を先頭へ戻す
        // Replace the whole timetable and reset the cursor to its first stop
        public void ReplaceEntries(IReadOnlyList<IRailNode> stationNodes)
        {
            _entries.Clear();
            _currentIndex = -1;
            foreach (var node in stationNodes)
            {
                AddEntry(node, DepartureConditionType.WaitForTicks, GameUpdater.TicksPerSecond);
            }
            _isCurrentEntryChanged = true;
        }

        // 現在地の変化を一度だけ通知側へ渡す
        // Pass a cursor change to the notifier exactly once
        public bool ConsumeCurrentEntryChanged()
        {
            var changed = _isCurrentEntryChanged;
            _isCurrentEntryChanged = false;
            return changed;
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
            if (_entries.Count == 0) 
            {
                _currentIndex = -1;
                return;
            }

            _currentIndex = (_currentIndex + 1) % _entries.Count;
            _isCurrentEntryChanged = true;
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
                _isCurrentEntryChanged = true;
                if (currentRemoved && _entries.Count > 0)
                {
                    _context?.OnCurrentEntryShiftedByRemoval();
                }
            }
        }


        public TrainDiagramEntry GetCurrentEntry()
        {
            return TryGetActiveEntry(out var entry) ? entry : null;
        }

        private bool TryGetActiveEntry(out TrainDiagramEntry entry)
        {
            entry = null;
            if ((_currentIndex < 0) || (_entries.Count == 0) || (_currentIndex >= _entries.Count))
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
