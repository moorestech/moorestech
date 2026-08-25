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

        // 追記が1割か64件で再構築する
        // Rebuild a key only once appends reach one tenth of its tree or 64 entries
        private const float RebuildAppendRatio = 0.1f;
        private const int MaxLinearScanAppends = 64;

        private readonly Dictionary<Guid, List<T>> _targetsByKey = new();
        private readonly Dictionary<Guid, KdTree<T>> _treesByKey = new();
        private readonly Dictionary<Guid, List<T>> _appendedTargetsByKey = new();
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

            targets.Add(target);
            if (!_treesByKey.ContainsKey(key))
            {
                _dirtyKeys.Add(key);
                return;
            }

            // 小さな追記は線形走査する
            // Scan small appends linearly instead of rebuilding the tree every frame
            if (!_appendedTargetsByKey.TryGetValue(key, out var appendedTargets))
            {
                appendedTargets = new List<T>();
                _appendedTargetsByKey.Add(key, appendedTargets);
            }

            appendedTargets.Add(target);
            var treeTargetCount = targets.Count - appendedTargets.Count;
            if (MaxLinearScanAppends <= appendedTargets.Count ||
                treeTargetCount * RebuildAppendRatio <= appendedTargets.Count)
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
            _treesByKey[key].TrySearchNearest(position, out target, out sqrDistance);

            // 未再構築の追記分も同じ最近傍競争へ加える
            // Add unreconstructed appends to the same nearest-target race
            if (!_appendedTargetsByKey.TryGetValue(key, out var appendedTargets)) return target != null;
            foreach (var appendedTarget in appendedTargets)
            {
                if (!appendedTarget.IsSearchable) continue;
                var appendedSqrDistance = (appendedTarget.GetIndexPosition() - position).sqrMagnitude;
                if (sqrDistance <= appendedSqrDistance) continue;

                target = appendedTarget;
                sqrDistance = appendedSqrDistance;
            }

            return target != null;
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
            if (_appendedTargetsByKey.TryGetValue(key, out var appendedTargets)) appendedTargets.Clear();
            _treesByKey[key] = new KdTree<T>(_searchableBuffer);
        }
    }
}
