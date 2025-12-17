using System;
using System.Collections.Generic;

namespace Game.Train.RailGraph
{
    /// <summary>
    /// 肥大化するRailGraphDatastore から切り出したダイクストラ法による経路探索クラス
    /// ・ノード ID（int）ベースで探索
    /// ・距離配列／前ノード配列は内部で再利用（stamp 方式で初期化コスト削減）
    /// ・同じ距離の場合は nodeId の小さい方を優先（決定的な経路）
    /// MassiveAutoRunScenarioProducesIdenticalStateWithAndWithoutSaveLoad (6.017s)→(5.884s)に改善
    /// </summary>
    public class RailGraphPathFinder
    {
        private int[] _distances;
        private int[] _previous;
        private int[] _stamp;
        private int _currentStamp = 1;

        private readonly DijkstraPriorityQueue _priorityQueue;

        public RailGraphPathFinder()
        {
            _priorityQueue = new DijkstraPriorityQueue(this);
        }

        /// <summary>
        /// 内部 ID（startId, targetId）を用いて最短経路を探索し、RailNodeId の列として返す。
        /// </summary>
        public List<int> FindShortestPath(
            List<List<(int targetId, int distance)>> connectNodes,
            int startId,
            int targetId)
        {
            int nodeCount = connectNodes.Count;
            if (startId < 0 || startId >= nodeCount ||
                targetId < 0 || targetId >= nodeCount)
            {
                return new List<int>();
            }
            EnsureWorkspace(nodeCount);
            BeginSearch();

            // 開始ノードの距離・前ノードを設定
            SetDistance(startId, 0, -1);

            _priorityQueue.Clear();
            _priorityQueue.Enqueue(startId);

            while (_priorityQueue.Count > 0)
            {
                int current = _priorityQueue.Dequeue();
                if (current == targetId)
                    break;

                int currentDistance = GetDistance(current);
                if (currentDistance == int.MaxValue)
                    continue; // 到達不可扱い

                var edges = connectNodes[current];
                for (int i = 0; i < edges.Count; i++)
                {
                    var edge = edges[i];
                    int neighbor = edge.Item1;
                    int weight = edge.Item2;

                    int newDistance = currentDistance + weight;
                    if (newDistance < 0)
                        continue; // オーバーフロー保険

                    int oldDistance = GetDistance(neighbor);
                    if (newDistance < oldDistance)
                    {
                        SetDistance(neighbor, newDistance, current);
                        _priorityQueue.Enqueue(neighbor);
                    }
                }
            }

            // target に到達できていない
            if (GetDistance(targetId) == int.MaxValue)
            {
                return new List<int>();
            }

            // 経路復元（targetId から backward にたどり、最後に reverse）
            var pathIds = new List<int>();
            int currentId = targetId;
            while (currentId != -1)
            {
                pathIds.Add(currentId);
                currentId = _previous[currentId];
            }
            pathIds.Reverse();

            #region Internal
            // ==========================
            // 内部ヘルパ
            // ==========================

            void EnsureWorkspace(int nodeCount)
            {
                // 既存配列で足りていれば再利用
                if (_distances != null && _distances.Length >= nodeCount)
                    return;

                // 必要数まで拡張（倍々 or ちょうど）
                int size = _distances == null
                    ? nodeCount
                    : Math.Max(_distances.Length * 2, nodeCount);

                _distances = new int[size];
                _previous = new int[size];
                _stamp = new int[size];
                _currentStamp = 1;
            }

            void BeginSearch()
            {
                _currentStamp++;
                if (_currentStamp == int.MaxValue)
                {
                    // stamp が溢れそうな場合のみ全クリア
                    Array.Clear(_stamp, 0, _stamp.Length);
                    _currentStamp = 1;
                }
            }

            /// <summary>
            /// 現在の探索における nodeId の距離と前ノードを設定。
            /// </summary>
            void SetDistance(int nodeId, int distance, int previousId)
            {
                _stamp[nodeId] = _currentStamp;
                _distances[nodeId] = distance;
                _previous[nodeId] = previousId;
            }
            #endregion
            return pathIds;
        }


        /// <summary>
        /// 現在の探索における nodeId の距離を取得。未設定なら int.MaxValue を返す。
        /// </summary>
        int GetDistance(int nodeId)
        {
            return _stamp[nodeId] == _currentStamp
                ? _distances[nodeId]
                : int.MaxValue;
        }
        // ==================================================
        // 内部専用 ヒープ実装（nodeId のみ持つ最小ヒープ）
        //   比較は owner.GetDistance(node) → nodeId
        // ==================================================

        private sealed class DijkstraPriorityQueue
        {
            private readonly RailGraphPathFinder _owner;
            private readonly List<int> _heap = new List<int>();

            public int Count => _heap.Count;

            public DijkstraPriorityQueue(RailGraphPathFinder owner)
            {
                _owner = owner;
            }

            public void Clear()
            {
                _heap.Clear();
            }

            public void Enqueue(int nodeId)
            {
                _heap.Add(nodeId);
                HeapifyUp(_heap.Count - 1);
            }

            public int Dequeue()
            {
                if (_heap.Count == 0)
                    throw new InvalidOperationException("The priority queue is empty.");

                int result = _heap[0];

                int lastIndex = _heap.Count - 1;
                _heap[0] = _heap[lastIndex];
                _heap.RemoveAt(lastIndex);

                if (_heap.Count > 0)
                {
                    HeapifyDown(0);
                }

                return result;
            }

            // ----------------- ヒープ操作 -----------------

            private void HeapifyUp(int index)
            {
                while (index > 0)
                {
                    int parentIndex = (index - 1) / 2;
                    if (!Less(index, parentIndex))
                        break;

                    Swap(index, parentIndex);
                    index = parentIndex;
                }
            }

            private void HeapifyDown(int index)
            {
                int count = _heap.Count;
                while (true)
                {
                    int left = index * 2 + 1;
                    int right = index * 2 + 2;
                    int smallest = index;

                    if (left < count && Less(left, smallest))
                        smallest = left;
                    if (right < count && Less(right, smallest))
                        smallest = right;

                    if (smallest == index)
                        break;

                    Swap(index, smallest);
                    index = smallest;
                }
            }

            /// <summary>
            /// 「距離が小さい方」→ 同距離なら「nodeId が小さい方」を優先する比較
            /// </summary>
            private bool Less(int indexA, int indexB)
            {
                int nodeA = _heap[indexA];
                int nodeB = _heap[indexB];

                int distA = _owner.GetDistance(nodeA);
                int distB = _owner.GetDistance(nodeB);

                if (distA != distB)
                    return distA < distB;

                // 距離が同じ場合は nodeId が小さい方を優先
                return nodeA < nodeB;
            }

            private void Swap(int a, int b)
            {
                int tmp = _heap[a];
                _heap[a] = _heap[b];
                _heap[b] = tmp;
            }
        }
    }
}
