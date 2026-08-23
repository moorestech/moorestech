using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.Map.NearestSearch
{
    /// <summary>
    ///     静的な点集合向けの3次元k-d tree。構築時に座標を焼き込み、配列を中央値で再帰分割した暗黙平衡木として持つ
    ///     3D k-d tree for a static point set; positions are baked at build time into an implicit balanced tree over a median-split array
    /// </summary>
    internal sealed class KdTree<T> where T : class, INearestSearchTarget
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

        public KdTree(IReadOnlyList<T> targets)
        {
            _positions = new Vector3[targets.Count];
            _targets = new T[targets.Count];
            for (var i = 0; i < targets.Count; i++)
            {
                _targets[i] = targets[i];
                _positions[i] = targets[i].GetIndexPosition();
            }

            Build(0, _targets.Length, 0);
        }

        /// <summary>
        ///     最寄りとその距離を焼き込み座標で返す。呼び出し側に距離の再計算をさせない
        ///     Returns the nearest target and its distance from the baked positions, so callers never recompute it
        /// </summary>
        public bool TrySearchNearest(Vector3 query, out T target, out float sqrDistance)
        {
            target = null;
            sqrDistance = float.MaxValue;
            if (_targets.Length == 0) return false;

            _query = query;
            _bestIndex = -1;
            _bestSqrDistance = float.MaxValue;
            Search(0, _targets.Length, 0);
            if (_bestIndex < 0) return false;

            target = _targets[_bestIndex];
            sqrDistance = _bestSqrDistance;
            return true;
        }

        private void Build(int lo, int hi, int depth)
        {
            // 1点以下の区間は葉
            // A range of one point or fewer is a leaf
            if (hi - lo <= 1) return;

            // 中央値だけをその場に確定させ、左右を次の軸で再帰する（全整列より軽い O(n log n)）
            // Place only the median in position and recurse both halves on the next axis (O(n log n), lighter than a full sort)
            var axis = depth % AxisCount;
            var mid = (lo + hi) / 2;
            SelectNth(lo, hi, mid, axis);
            Build(lo, mid, depth + 1);
            Build(mid + 1, hi, depth + 1);
        }

        /// <summary>
        ///     Hoare分割による選択。区間[lo,hi)のnth番目を軸座標順の正しい位置へ落とす
        ///     Hoare-partition selection; puts the nth element of [lo,hi) at its correct position in axis order
        /// </summary>
        private void SelectNth(int lo, int hi, int nth, int axis)
        {
            var left = lo;
            var right = hi - 1;
            while (left < right)
            {
                // 区間中央の座標を枢軸に取る。同一座標が連続しても左右が必ず縮む
                // Take the middle element's coordinate as the pivot; both sides shrink even for runs of identical coordinates
                var pivot = _positions[(left + right) / 2][axis];
                var low = left;
                var high = right;
                while (low <= high)
                {
                    while (_positions[low][axis] < pivot) low++;
                    while (pivot < _positions[high][axis]) high--;
                    if (high < low) break;

                    Swap(low, high);
                    low++;
                    high--;
                }

                // nthを含む側だけ残す。両側の間に落ちたなら既に確定している
                // Keep only the side containing nth; if it landed between the halves it is already final
                if (nth <= high) right = high;
                else if (low <= nth) left = low;
                else return;
            }
        }

        private void Swap(int left, int right)
        {
            (_positions[left], _positions[right]) = (_positions[right], _positions[left]);
            (_targets[left], _targets[right]) = (_targets[right], _targets[left]);
        }

        private void Search(int lo, int hi, int depth)
        {
            if (hi <= lo) return;

            var mid = (lo + hi) / 2;
            var axis = depth % AxisCount;
            var nodePosition = _positions[mid];

            // 厳密に近い探索可能な候補だけが最良値を更新する（等距離は走査順で先に訪れた側が残る）
            // Only a strictly closer searchable candidate updates the best (ties keep whichever was visited first)
            var sqrDistance = (nodePosition - _query).sqrMagnitude;
            if (sqrDistance < _bestSqrDistance && _targets[mid].IsSearchable)
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
    }
}
