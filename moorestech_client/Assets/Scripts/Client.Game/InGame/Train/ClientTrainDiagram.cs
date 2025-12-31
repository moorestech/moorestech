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
        public int EntryCount => _snapshot.Entries?.Count ?? 0;

        public void UpdateSnapshot(TrainDiagramSnapshot snapshot)
        {
            // スナップショットを置き換えて参照を更新する
            // Replace the snapshot to refresh reads
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

        public bool TryGetCurrentEntry(out TrainDiagramEntrySnapshot entry)
        {
            // 現在のエントリーを取得してUI表示に使う
            // Get the current entry for UI rendering
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
            // 任意インデックスのエントリー取得を提供する
            // Expose entry access by index
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
            // 現在の目的地ノードを解決し、存在しなければ失敗扱いにする
            // Resolve the current destination node and fail if missing
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
            // 現在のエントリーから順に経路を探索し、見つかれば返す
            // Walk entries to find a reachable path
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
