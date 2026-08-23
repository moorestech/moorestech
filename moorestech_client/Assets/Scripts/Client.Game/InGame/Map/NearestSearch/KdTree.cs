using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Map.NearestSearch
{
    /// <summary>
    ///     静的な点集合向けの3次元k-d tree。構築時に座標を焼き込み、配列を中央値で再帰分割した暗黙平衡木として持つ
    ///     3D k-d tree for a static point set; positions are baked at build time into an implicit balanced tree over a median-split array
    /// </summary>
    public sealed class KdTree<T> where T : class, INearestSearchTarget
    {
        private const int AxisCount = 3;

        // 中央値分割済みの配列。区間[lo,hi)の中央がノード、左右の部分区間が子
        // Median-split arrays; the middle of [lo,hi) is the node and the two halves are its children
        private readonly Vector3[] _positions;
        private readonly T[] _targets;

        // 探索中の最良値。毎フレーム呼ばれるためアロケーションを避けてフィールドに持つ
        // Best-so-far during a search; kept in fields to avoid allocating on the per-frame path
        private Vector3 _query;
        private int _bestIndex;
        private float _bestSqrDistance;

        public int Count => _targets.Length;

        public KdTree(IReadOnlyList<T> targets)
        {
            _positions = new Vector3[targets.Count];
            _targets = new T[targets.Count];
            for (var i = 0; i < targets.Count; i++)
            {
                _targets[i] = targets[i];
                _positions[i] = targets[i].Position;
            }

            Build(0, _targets.Length, 0);
        }

        public T SearchNearest(Vector3 query)
        {
            if (_targets.Length == 0) return null;

            _query = query;
            _bestIndex = -1;
            _bestSqrDistance = float.MaxValue;
            Search(0, _targets.Length, 0);
            return _targets[_bestIndex];
        }

        private void Build(int lo, int hi, int depth)
        {
            // 1点以下の区間は葉
            // A range of one point or fewer is a leaf
            if (hi - lo <= 1) return;

            // 軸で整列して中央値をノードにし、左右を次の軸で再帰する（同一座標の連続でも区間は必ず縮む）
            // Sort by axis, take the median as the node, recurse both halves on the next axis (ranges shrink even for identical coordinates)
            var axis = depth % AxisCount;
            System.Array.Sort(_positions, _targets, lo, hi - lo, AxisComparer.ForAxis(axis));
            var mid = (lo + hi) / 2;
            Build(lo, mid, depth + 1);
            Build(mid + 1, hi, depth + 1);
        }

        private void Search(int lo, int hi, int depth)
        {
            if (hi <= lo) return;

            var mid = (lo + hi) / 2;
            var axis = depth % AxisCount;
            var nodePosition = _positions[mid];

            // 厳密に近い候補だけが最良値を更新する（等距離は走査順で先に訪れた側が残る）
            // Only a strictly closer candidate updates the best (ties keep whichever was visited first)
            var sqrDistance = (nodePosition - _query).sqrMagnitude;
            if (sqrDistance < _bestSqrDistance)
            {
                _bestSqrDistance = sqrDistance;
                _bestIndex = mid;
            }

            // クエリのある側を先に降り、分割面までの距離が最良値より近い時だけ反対側も見る
            // Descend the query's side first, then the far side only if the splitting plane is closer than the best
            var delta = _query[axis] - nodePosition[axis];
            if (delta < 0f)
            {
                Search(lo, mid, depth + 1);
                if (delta * delta < _bestSqrDistance) Search(mid + 1, hi, depth + 1);
            }
            else
            {
                Search(mid + 1, hi, depth + 1);
                if (delta * delta < _bestSqrDistance) Search(lo, mid, depth + 1);
            }
        }

        /// <summary>
        ///     軸ごとの座標比較。構築時のみ使うため軸数分を静的に共有する
        ///     Per-axis position comparer; build-time only, so one instance per axis is shared statically
        /// </summary>
        private sealed class AxisComparer : IComparer<Vector3>
        {
            private static readonly AxisComparer[] Comparers = { new(0), new(1), new(2) };
            private readonly int _axis;

            private AxisComparer(int axis)
            {
                _axis = axis;
            }

            public static AxisComparer ForAxis(int axis)
            {
                return Comparers[axis];
            }

            public int Compare(Vector3 left, Vector3 right)
            {
                return left[_axis].CompareTo(right[_axis]);
            }
        }
    }
}
