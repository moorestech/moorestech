using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Map.NearestSearch
{
    /// <summary>
    ///     - key別に独立したk-d treeを持ち、登録・墓標・再構築を索引側で完結させる
    ///     - 探索不能になった対象は木を保ったままスキップし、増えすぎた時だけ組み直す
    ///     - One independent k-d tree per key, owning registration, tombstones and rebuilds
    ///     - Unsearchable targets are skipped in place and the tree is rebuilt only once they pile up
    /// </summary>
    internal sealed class NearestTargetIndex<T> where T : class, INearestSearchTarget
    {
        // 墓標がこの割合を超えた鍵だけ組み直す。1件ごとの再構築（確保とソート）を避けるためのしきい値
        // Rebuild a key only past this tombstone ratio; the threshold is what keeps per-removal rebuilds (allocation and sorting) away
        private const float RebuildTombstoneRatio = 0.5f;

        private readonly Dictionary<Guid, List<T>> _targetsByKey = new();
        private readonly Dictionary<Guid, KdTree<T>> _treesByKey = new();
        private readonly Dictionary<Guid, int> _tombstoneCountByKey = new();
        private readonly HashSet<Guid> _dirtyKeys = new();

        // 再構築時の探索可能対象バッファ。木が配列へ複製するので使い回せる
        // Searchable-target buffer for rebuilds; the tree copies into its own arrays, so this can be reused
        private readonly List<T> _searchableBuffer = new();

        public void Register(Guid key, T target)
        {
            if (!_targetsByKey.TryGetValue(key, out var targets))
            {
                targets = new List<T>();
                _targetsByKey.Add(key, targets);
            }

            // 登録はフレーム分散で来るので、そのたびdirtyにして最初の探索で一括構築する
            // Registration arrives spread across frames, so mark dirty each time and build once on the first search
            targets.Add(target);
            _dirtyKeys.Add(key);
        }

        /// <summary>
        ///     対象が探索対象から外れたことを受ける。木は保ったままで、墓標が増えた時だけ組み直しを予約する
        ///     Accepts that a target left the searchable set; the tree stays as is and only piled-up tombstones schedule a rebuild
        /// </summary>
        public void NotifyTargetUnsearchable(Guid key)
        {
            if (!_targetsByKey.TryGetValue(key, out var targets)) return;

            _tombstoneCountByKey.TryGetValue(key, out var tombstoneCount);
            tombstoneCount++;
            _tombstoneCountByKey[key] = tombstoneCount;

            if (targets.Count * RebuildTombstoneRatio <= tombstoneCount) _dirtyKeys.Add(key);
        }

        public bool TrySearchNearest(Guid key, Vector3 position, out T target, out float sqrDistance)
        {
            target = null;
            sqrDistance = float.MaxValue;
            if (!_targetsByKey.TryGetValue(key, out var targets)) return false;

            if (_dirtyKeys.Remove(key)) Rebuild(key, targets);
            return _treesByKey[key].TrySearchNearest(position, out target, out sqrDistance);
        }

        private void Rebuild(Guid key, List<T> targets)
        {
            // 探索不能な対象は候補からも落とし、木と候補リストの両方を軽くする
            // Drop unsearchable targets from the candidates too, shrinking both the tree and the list
            _searchableBuffer.Clear();
            foreach (var target in targets)
            {
                if (target.IsSearchable) _searchableBuffer.Add(target);
            }

            targets.Clear();
            targets.AddRange(_searchableBuffer);
            _tombstoneCountByKey[key] = 0;
            _treesByKey[key] = new KdTree<T>(_searchableBuffer);
        }
    }
}
