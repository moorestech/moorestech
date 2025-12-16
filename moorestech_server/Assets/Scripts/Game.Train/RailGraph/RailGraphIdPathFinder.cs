using System;
using System.Collections.Generic;

namespace Game.Train.RailGraph
{
    // IDベースのダイクストラ探索を提供するユーティリティ
    // Utility that runs ID-based Dijkstra searches for rail graphs
    public sealed class RailGraphIdPathFinder
    {
        private readonly List<int> _pathBuffer = new List<int>();
        private int[] _distances;
        private int[] _previous;
        private int[] _stamp;
        private int _currentStamp = 1;
        private readonly DijkstraPriorityQueue _priorityQueue;

        public RailGraphIdPathFinder()
        {
            _priorityQueue = new DijkstraPriorityQueue(this);
        }

        public RailPathResult FindShortestPath(
            IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> adjacency,
            int startId,
            int targetId)
        {
            // 無効な入力なら距離なしの結果を返す
            // Return empty result for invalid indices
            var nodeCount = adjacency.Count;
            if (!IsValidIndex(startId, nodeCount) || !IsValidIndex(targetId, nodeCount))
                return RailPathResult.Empty;

            EnsureWorkspace(nodeCount);
            BeginSearch();
            SetDistance(startId, 0, -1);

            _priorityQueue.Clear();
            _priorityQueue.Enqueue(startId);

            while (_priorityQueue.Count > 0)
            {
                var current = _priorityQueue.Dequeue();
                if (current == targetId)
                    break;

                var currentDistance = ReadDistance(current);
                if (currentDistance == int.MaxValue)
                    continue;

                var edges = adjacency[current];
                if (edges == null)
                    continue;

                for (var i = 0; i < edges.Count; i++)
                {
                    var edge = edges[i];
                    var neighbor = edge.targetId;
                    var weight = edge.distance;
                    var newDistance = currentDistance + weight;
                    if (newDistance < 0)
                        continue;

                    var oldDistance = ReadDistance(neighbor);
                    if (newDistance < oldDistance)
                    {
                        SetDistance(neighbor, newDistance, current);
                        _priorityQueue.Enqueue(neighbor);
                    }
                }
            }

            var finalDistance = ReadDistance(targetId);
            if (finalDistance == int.MaxValue)
                return RailPathResult.Empty;

            _pathBuffer.Clear();
            var pathCurrent = targetId;
            while (pathCurrent != -1)
            {
                _pathBuffer.Add(pathCurrent);
                pathCurrent = _previous[pathCurrent];
            }
            _pathBuffer.Reverse();

            var path = new List<int>(_pathBuffer);
            return new RailPathResult(path, finalDistance);

            #region Internal

            bool IsValidIndex(int index, int count)
            {
                return index >= 0 && index < count;
            }

            void EnsureWorkspace(int nodeCount)
            {
                if (_distances != null && _distances.Length >= nodeCount)
                    return;

                var size = _distances == null ? nodeCount : Math.Max(_distances.Length * 2, nodeCount);
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
                    Array.Clear(_stamp, 0, _stamp.Length);
                    _currentStamp = 1;
                }
            }

            void SetDistance(int nodeId, int distance, int previousId)
            {
                _stamp[nodeId] = _currentStamp;
                _distances[nodeId] = distance;
                _previous[nodeId] = previousId;
            }

            #endregion
        }

        internal int ReadDistance(int nodeId)
        {
            return _stamp[nodeId] == _currentStamp ? _distances[nodeId] : int.MaxValue;
        }

        private sealed class DijkstraPriorityQueue
        {
            private readonly RailGraphIdPathFinder _owner;
            private readonly List<int> _heap = new List<int>();

            public int Count => _heap.Count;

            public DijkstraPriorityQueue(RailGraphIdPathFinder owner)
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

                var result = _heap[0];
                var lastIndex = _heap.Count - 1;
                _heap[0] = _heap[lastIndex];
                _heap.RemoveAt(lastIndex);

                if (_heap.Count > 0)
                    HeapifyDown(0);

                return result;
            }

            private void HeapifyUp(int index)
            {
                while (index > 0)
                {
                    var parentIndex = (index - 1) / 2;
                    if (!Less(index, parentIndex))
                        break;

                    Swap(index, parentIndex);
                    index = parentIndex;
                }
            }

            private void HeapifyDown(int index)
            {
                var count = _heap.Count;
                while (true)
                {
                    var left = index * 2 + 1;
                    var right = index * 2 + 2;
                    var smallest = index;

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

            private bool Less(int indexA, int indexB)
            {
                var nodeA = _heap[indexA];
                var nodeB = _heap[indexB];
                var distA = _owner.ReadDistance(nodeA);
                var distB = _owner.ReadDistance(nodeB);
                if (distA != distB)
                    return distA < distB;
                return nodeA < nodeB;
            }

            private void Swap(int a, int b)
            {
                var tmp = _heap[a];
                _heap[a] = _heap[b];
                _heap[b] = tmp;
            }
        }
    }
}
