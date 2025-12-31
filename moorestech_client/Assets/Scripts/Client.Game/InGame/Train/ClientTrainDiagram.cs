using System;
using System.Collections.Generic;
using System.Linq;
using Game.Train.RailGraph;
using Game.Train.Train;

namespace Client.Game.InGame.Train
{
    // クライアント側のダイアグラム参照と遷移をまとめる
    // Centralize client-side diagram reads and transitions
    public sealed class ClientTrainDiagram
    {
        private TrainDiagramSnapshot _snapshot;

        public ClientTrainDiagram(TrainDiagramSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public TrainDiagramSnapshot Snapshot => _snapshot;
        public int CurrentIndex => _snapshot.CurrentIndex;
        public IReadOnlyList<TrainDiagramEntrySnapshot> Entries => _snapshot.Entries;

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

            // 対象のエントリーを探してカレントを更新する
            // Update current index by locating the matching entry
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].EntryId == entryId)
                {
                    _snapshot = new TrainDiagramSnapshot(i, entries);
                    return;
                }
            }
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

            node = RailGraphProvider.Current.ResolveRailNode(entries[index].Node);
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

            // 現在のエントリーから順に到達可能な経路を探索する
            // Walk entries in order to find a reachable path
            for (var i = 0; i < entries.Count; i++)
            {
                if (!TryResolveCurrentDestinationNode(out var destinationNode))
                {
                    MoveToNextEntry(entries.Count);
                    continue;
                }

                var foundPath = RailGraphProvider.Current.FindShortestPath(approaching, destinationNode);
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

        #region Internal

        private void MoveToNextEntry(int entriesCount)
        {
            // エントリー数に応じて次のインデックスへ進める
            // Move to the next index based on current entry count
            if (entriesCount <= 0)
            {
                var fallbackEntries = _snapshot.Entries ?? Array.Empty<TrainDiagramEntrySnapshot>();
                _snapshot = new TrainDiagramSnapshot(-1, fallbackEntries);
                return;
            }

            var nextIndex = (_snapshot.CurrentIndex + 1) % entriesCount;
            _snapshot = new TrainDiagramSnapshot(nextIndex, _snapshot.Entries);
        }

        #endregion
    }
}
