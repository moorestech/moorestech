using System;
using System.Collections.Generic;
using System.Linq;
using Game.Train.RailGraph;
using Game.Train.Unit;
using TrainDiagramType = global::Game.Train.Diagram.TrainDiagram;

namespace Client.Game.InGame.Train.Diagram
{
    // クライアント側のダイアグラム参照と遷移をまとめる。
    // Centralize client-side diagram reads and transitions.
    public sealed class ClientTrainDiagram
    {
        private TrainDiagramSnapshot _snapshot;
        private readonly IRailGraphProvider _railGraphProvider;

        public ClientTrainDiagram(TrainDiagramSnapshot snapshot, IRailGraphProvider railGraphProvider)
        {
            // レールグラフ参照を保持する。
            // Keep the rail graph provider reference.
            _snapshot = snapshot;
            _railGraphProvider = railGraphProvider;
        }

        public TrainDiagramSnapshot Snapshot => _snapshot;
        public int CurrentIndex => _snapshot.CurrentIndex;
        public IReadOnlyList<TrainDiagramEntrySnapshot> Entries => _snapshot.Entries;
        public int EntryCount => _snapshot.Entries?.Count ?? 0;

        public void UpdateSnapshot(TrainDiagramSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public void UpdateIndexByEntryId(Guid entryId)
        {
            var entries = _snapshot.Entries;
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].EntryId == entryId)
                {
                    _snapshot = new TrainDiagramSnapshot(i, entries);
                    return;
                }
            }
        }

        public bool TryGetCurrentEntry(out TrainDiagramEntrySnapshot entry)
        {
            entry = default;
            var entries = _snapshot.Entries;
            if (entries == null || entries.Count == 0)
            {
                return false;
            }

            var index = _snapshot.CurrentIndex;
            if (index < 0 || index >= entries.Count)
            {
                return false;
            }

            entry = entries[index];
            return true;
        }

        public bool TryGetEntry(int index, out TrainDiagramEntrySnapshot entry)
        {
            entry = default;
            var entries = _snapshot.Entries;
            if (entries == null || index < 0 || index >= entries.Count)
            {
                return false;
            }

            entry = entries[index];
            return true;
        }

        public bool TryResolveCurrentDestinationNode(out IRailNode node)
        {
            var entries = _snapshot.Entries;
            if (entries == null || entries.Count == 0)
            {
                node = null;
                return false;
            }

            var index = _snapshot.CurrentIndex;
            if (index < 0 || index >= entries.Count)
            {
                node = null;
                return false;
            }

            node = _railGraphProvider.ResolveRailNode(entries[index].Node);
            return node != null;
        }

        public bool TryFindPathFrom(IRailNode approaching, out List<IRailNode> path)
        {
            path = null;
            var entries = _snapshot.Entries;
            if (entries == null || entries.Count == 0 || approaching == null)
            {
                return false;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                if (!TryResolveCurrentDestinationNode(out var destinationNode))
                {
                    MoveToNextEntry(entries.Count);
                    continue;
                }

                var foundPath = _railGraphProvider.FindShortestPath(approaching, destinationNode);
                var newPath = foundPath?.ToList();
                if (newPath == null || newPath.Count < 2)
                {
                    MoveToNextEntry(entries.Count);
                    continue;
                }

                path = newPath;
                return true;
            }

            return false;
        }

        // ドッキング中の待機tickを進める。
        // Advance docked wait-ticks.
        public void TickDockedDepartureConditions(bool isAutoRun)
        {
            if (!isAutoRun)
            {
                return;
            }
            if (!TryGetCurrentEntry(out var entry))
            {
                return;
            }
            if (!HasWaitForTicksCondition(entry))
            {
                return;
            }

            var remaining = entry.WaitForTicksRemaining ?? entry.WaitForTicksInitial;
            if (!remaining.HasValue || remaining.Value <= 0)
            {
                return;
            }

            ReplaceCurrentEntry(new TrainDiagramEntrySnapshot(
                entry.EntryId,
                entry.Node,
                entry.DepartureConditions,
                entry.WaitForTicksInitial,
                Math.Max(remaining.Value - 1, 0)));
        }

        public bool CanCurrentEntryDepart()
        {
            if (!TryGetCurrentEntry(out var entry))
            {
                return true;
            }

            var conditions = entry.DepartureConditions;
            if (conditions == null || conditions.Count == 0)
            {
                return true;
            }

            for (var i = 0; i < conditions.Count; i++)
            {
                if (conditions[i] == TrainDiagramType.DepartureConditionType.WaitForTicks)
                {
                    var remaining = entry.WaitForTicksRemaining ?? entry.WaitForTicksInitial ?? 0;
                    if (remaining > 0)
                    {
                        return false;
                    }
                    continue;
                }

                return false;
            }

            return true;
        }

        public void ResetCurrentEntryDepartureConditions()
        {
            if (!TryGetCurrentEntry(out var entry))
            {
                return;
            }
            if (!HasWaitForTicksCondition(entry))
            {
                return;
            }
            if (!entry.WaitForTicksInitial.HasValue)
            {
                return;
            }

            ReplaceCurrentEntry(new TrainDiagramEntrySnapshot(
                entry.EntryId,
                entry.Node,
                entry.DepartureConditions,
                entry.WaitForTicksInitial,
                entry.WaitForTicksInitial));
        }

        public void AdvanceToNextEntry()
        {
            MoveToNextEntry(EntryCount);
        }

        #region Internal

        private void MoveToNextEntry(int entriesCount)
        {
            if (entriesCount <= 0)
            {
                var fallbackEntries = _snapshot.Entries ?? Array.Empty<TrainDiagramEntrySnapshot>();
                _snapshot = new TrainDiagramSnapshot(-1, fallbackEntries);
                return;
            }

            var nextIndex = (_snapshot.CurrentIndex + 1) % entriesCount;
            _snapshot = new TrainDiagramSnapshot(nextIndex, _snapshot.Entries);
        }

        private static bool HasWaitForTicksCondition(TrainDiagramEntrySnapshot entry)
        {
            var conditions = entry.DepartureConditions;
            if (conditions == null || conditions.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < conditions.Count; i++)
            {
                if (conditions[i] == TrainDiagramType.DepartureConditionType.WaitForTicks)
                {
                    return true;
                }
            }

            return false;
        }

        private void ReplaceCurrentEntry(TrainDiagramEntrySnapshot entry)
        {
            if (!TryGetCurrentIndex(out var index))
            {
                return;
            }

            var entries = _snapshot.Entries;
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            var copied = new TrainDiagramEntrySnapshot[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                copied[i] = entries[i];
            }

            copied[index] = entry;
            _snapshot = new TrainDiagramSnapshot(index, copied);
        }

        private bool TryGetCurrentIndex(out int index)
        {
            index = -1;
            var entries = _snapshot.Entries;
            if (entries == null || entries.Count == 0)
            {
                return false;
            }

            if (_snapshot.CurrentIndex < 0 || _snapshot.CurrentIndex >= entries.Count)
            {
                return false;
            }

            index = _snapshot.CurrentIndex;
            return true;
        }

        #endregion
    }
}
