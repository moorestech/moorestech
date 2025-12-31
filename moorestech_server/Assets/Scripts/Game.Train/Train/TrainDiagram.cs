using Game.Train.RailGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Train.Common;

namespace Game.Train.Train
{
    public class TrainDiagram
    {
        private readonly List<TrainDiagramEntry> _entries;
        private int _currentIndex;
        private ITrainDiagramContext _context;

        public IReadOnlyList<TrainDiagramEntry> Entries => _entries;
        public int CurrentIndex => _currentIndex;

        public enum DepartureConditionType
        {
            TrainInventoryFull,
            TrainInventoryEmpty,
            WaitForTicks
        }

        public TrainDiagram()
        {
            _entries = new List<TrainDiagramEntry>();
            _currentIndex = -1;
            TrainDiagramManager.Instance.RegisterDiagram(this);
        }
        public void OnDestroy()
        {
            TrainDiagramManager.Instance.UnregisterDiagram(this);
            _entries.Clear();
        }

        internal void RestoreState(TrainDiagramSaveData saveData)
        {
            _entries.Clear();
            _currentIndex = -1;

            if (saveData == null || saveData.Entries == null)
            {
                return;
            }

            foreach (var entryData in saveData.Entries)
            {
                if (entryData == null)
                {
                    continue;
                }
                
                var node = RailGraphProvider.Current.ResolveRailNode(entryData.Node);
                if (node == null)
                {
                    continue;
                }

                var entry = TrainDiagramEntry.CreateFromSaveData(
                    node,
                    entryData.EntryId,
                    entryData.DepartureConditions,
                    entryData.WaitForTicksInitial,
                    entryData.WaitForTicksRemaining);

                _entries.Add(entry);
            }

            if (_entries.Count == 0)
            {
                _currentIndex = -1;
                return;
            }

            var restoredIndex = saveData.CurrentIndex;
            if (restoredIndex < -1)
            {
                restoredIndex = -1;
            }
            else if (restoredIndex >= _entries.Count)
            {
                restoredIndex = _entries.Count - 1;
            }

            _currentIndex = restoredIndex;
        }

        internal void SetContext(ITrainDiagramContext context)
        {
            _context = context;
        }

        //最後に追加
        public TrainDiagramEntry AddEntry(IRailNode node)
        {
            if (_currentIndex < 0)
                _currentIndex = 0;
            var entry = new TrainDiagramEntry(node);
            _entries.Add(entry);
            return entry;
        }
        //最後に追加のcondition付き
        public TrainDiagramEntry AddEntry(IRailNode node, DepartureConditionType departureConditionType, int waitTicks = 0)
        {
            var entry = AddEntry(node);
            if (departureConditionType == DepartureConditionType.WaitForTicks)
            {
                entry.SetDepartureWaitTicks(waitTicks);
            }
            else 
            {
                entry.SetDepartureCondition(departureConditionType);
            }
            return entry;
        }
        //index指定して追加
        public TrainDiagramEntry InsertEntry(int index, IRailNode node)
        {
            if (_currentIndex < 0)
                _currentIndex = 0;
            if (index < 0)
            {
                index = 0;
            }
            else if (index > _entries.Count)
            {
                index = _entries.Count;
            }
            var entry = new TrainDiagramEntry(node);
            _entries.Insert(index, entry);
            return entry;
        }

        public void Update()
        {
            if (_currentIndex < 0)
            {
                return;
            }

            if (!TryGetActiveEntry(out var currentEntry))
            {
                _currentIndex = -1;
                return;
            }

            currentEntry.Tick(_context);
        }

        public bool CanCurrentEntryDepart()
        {
            if (_currentIndex < 0)
            {
                return true;
            }

            if (!TryGetActiveEntry(out var currentEntry))
            {
                _currentIndex = -1;
                return true;
            }

            return currentEntry.CanDepart(_context);
        }

        public IRailNode GetCurrentNode()
        {
            return TryGetActiveEntry(out var entry) ? entry.Node : null;
        }
        //getNextのguid版
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
        }

        //node削除時かならず呼ばれます->entriesの中身は常に実在するnodeのみ
        //currentIndexも削除対象なら暗黙的に次のnodeに移動します
        public void HandleNodeRemoval(IRailNode removedNode)
        {
            if (removedNode == null)
                return;
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (!_entries[i].MatchesNode(removedNode))
                {
                    continue;
                }

                if (_currentIndex >= 0 && i < _currentIndex)
                {
                    _currentIndex--;
                }
                _entries.RemoveAt(i);
            }

            if (_entries.Count == 0)
            {
                _currentIndex = -1;
            }
            else 
            {
                _currentIndex %= _entries.Count;
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

        //到着時発車条件リセット
        public void ResetCurrentEntryDepartureConditions()
        {
            if (TryGetActiveEntry(out var entry))
            {
                entry.OnDeparted();
            }
        }

        internal void NotifyDocked()
        {
            var entry = GetCurrentEntry();
            if (_context == null || entry?.Node == null)
            {
                return;
            }
            TrainDiagramManager.Instance.NotifyDocked(_context, entry, TrainUpdateService.CurrentTick);
        }

        internal void NotifyDeparted()
        {
            var entry = GetCurrentEntry();
            if (_context == null || entry?.Node == null)
            {
                return;
            }
            TrainDiagramManager.Instance.NotifyDeparted(_context, entry, TrainUpdateService.CurrentTick);
        }

        public TrainDiagramSaveData CreateTrainDiagramSaveData()
        {
            var entries = new List<TrainDiagramEntrySaveData>();
            foreach (var entry in this.Entries)
            {
                entries.Add(new TrainDiagramEntrySaveData
                {
                    EntryId = entry.entryId,
                    Node = entry.Node.ConnectionDestination,
                    DepartureConditions = entry.DepartureConditionTypes?.ToList() ?? new List<DepartureConditionType>(),
                    WaitForTicksInitial = entry.GetWaitForTicksInitialTicks(),
                    WaitForTicksRemaining = entry.GetWaitForTicksRemainingTicks()
                });
            }

            return new TrainDiagramSaveData
            {
                CurrentIndex = this.CurrentIndex,
                Entries = entries
            };
        }
    }
}
