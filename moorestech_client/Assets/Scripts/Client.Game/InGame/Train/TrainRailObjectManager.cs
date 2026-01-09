using System.Collections.Generic;
using UnityEngine;
using Game.Train.RailGraph;

namespace Client.Game.InGame.Train
{
    /// <summary>
    ///     キャッシュ更新に追随してレールラインを生成/削除する管理クラス
    ///     Manages runtime line renderers driven directly by rail cache updates
    /// </summary>
    public sealed class TrainRailObjectManager : MonoBehaviour
    {
        public static TrainRailObjectManager Instance { get; private set; }
        [SerializeField] private BezierRailChain _railPrefab;
        private readonly Dictionary<ulong, GameObject> _railObjs = new();
        private RailGraphClientCache _cache;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            foreach (var gobj in _railObjs.Values)
            {
                if (gobj != null)
                {
                    Destroy(gobj);
                }
            }
            _railObjs.Clear();
        }

        internal void OnCacheRebuilt(RailGraphClientCache cache)
        {
            _cache = cache;
            RebuildExistingConnections();
        }

        internal void OnConnectionUpserted(int fromNodeId, int toNodeId, RailGraphClientCache cache)
        {
            _cache = cache;
            TryActivateLine(fromNodeId, toNodeId);
        }

        internal void OnConnectionRemoved(int fromNodeId, int toNodeId, RailGraphClientCache cache)
        {
            _cache = cache;
            RemoveLine(fromNodeId, toNodeId);
        }

        #region Internal

        private void RebuildExistingConnections()
        {
            foreach (var gobj in _railObjs.Values)
            {
                if (gobj != null)
                {
                    Destroy(gobj);
                }
            }
            _railObjs.Clear();

            var adjacency = _cache.ConnectNodes;
            for (var fromId = 0; fromId < adjacency.Count; fromId++)
            {
                var edges = adjacency[fromId];
                if (edges == null || edges.Count == 0)
                {
                    continue;
                }

                foreach (var (targetId, _) in edges)
                {
                    TryActivateLine(fromId, targetId);
                }
            }
        }

        private void TryActivateLine(int fromNodeId, int toNodeId)
        {
            if (!HasPairedConnection(fromNodeId, toNodeId))
                return;

            var (canonicalFrom, canonicalTo) = SelectCanonicalPair(fromNodeId, toNodeId);
            var railObjectId = ComputeRailObjectId(canonicalFrom, canonicalTo);
            if (_railObjs.ContainsKey(railObjectId))
                return;
            if (_cache == null)
                return;
            if (!_cache.TryGetNode(canonicalFrom, out var startNode))
                return;
            if (!_cache.TryGetNode(canonicalTo, out var endNode))
                return;

            var lineObject = SpawnRail($"RailLine_{canonicalFrom}_{canonicalTo}", startNode, endNode);
            lineObject.transform.SetParent(transform, false);
            _railObjs[railObjectId] = lineObject;
        }

        private void RemoveLine(int fromNodeId, int toNodeId)
        {
            var (canonicalFrom, canonicalTo) = SelectCanonicalPair(fromNodeId, toNodeId);
            var railObjectId = ComputeRailObjectId(canonicalFrom, canonicalTo);
            if (!_railObjs.TryGetValue(railObjectId, out var gobj))
            {
                return;
            }

            _railObjs.Remove(railObjectId);
            if (gobj != null)
            {
                Destroy(gobj);
            }
        }

        private bool HasPairedConnection(int fromNodeId, int toNodeId)
        {
            if (_cache == null)
                return false;

            var adjacency = _cache.ConnectNodes;
            if (!IsValidIndex(adjacency, fromNodeId) || !IsValidIndex(adjacency, toNodeId))
                return false;

            var oppositeSource = toNodeId ^ 1;
            var oppositeTarget = fromNodeId ^ 1;
            if (!IsValidIndex(adjacency, oppositeSource))
                return false;

            var edges = adjacency[oppositeSource];
            if (edges == null)
                return false;

            foreach (var (targetId, _) in edges)
            {
                if (targetId == oppositeTarget)
                {
                    return true;
                }
            }
            return false;
        }

        private GameObject SpawnRail(string name, IRailNode startNode, IRailNode endNode)
        {
            var instance = Instantiate(_railPrefab, transform);
            var startControl = startNode.FrontControlPoint.OriginalPosition;
            var control1 = startNode.FrontControlPoint.ControlPointPosition + startControl;
            var endControl = endNode.BackControlPoint.OriginalPosition;
            var control2 = endNode.BackControlPoint.ControlPointPosition + endControl;

            instance.SetControlPoints(startControl, control1, control2, endControl);
            instance.Rebuild();
            instance.name = name;
            return instance.gameObject;
        }

        private static (int canonicalFrom, int canonicalTo) SelectCanonicalPair(int fromNodeId, int toNodeId)
        {
            var alternateFrom = toNodeId ^ 1;
            var alternateTo = fromNodeId ^ 1;
            return fromNodeId <= alternateFrom ? (fromNodeId, toNodeId) : (alternateFrom, alternateTo);
        }

        private static ulong ComputeRailObjectId(int canonicalFrom, int canonicalTo)
        {
            return (ulong)canonicalFrom + ((ulong)canonicalTo << 32);
        }

        private static bool IsValidIndex(IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> adjacency, int index)
        {
            return index >= 0 && index < adjacency.Count;
        }

        #endregion
    }
}
