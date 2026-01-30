using System.Collections.Generic;
using Game.Train.RailGraph;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    /// <summary>
    ///     キャッシュ更新に追随してレールラインを生成/削除する管理クラス
    ///     Manages runtime line renderers driven directly by rail cache updates
    /// </summary>
    public sealed class TrainRailObjectManager : MonoBehaviour
    {
        public static TrainRailObjectManager Instance { get; private set; }
        [SerializeField] private BezierRailChain _railPrefab;
        private readonly Dictionary<RailSegmentId, GameObject> _railObjs = new();
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
            var segmentId = RailSegmentId.CreateCanonical(fromNodeId, toNodeId);
            var minNodeId = segmentId.GetMinNodeId();
            var maxNodeId = segmentId.GetMaxNodeId();
            if (_railObjs.ContainsKey(segmentId))
                return;
            if (_cache == null)
                return;
            if (!_cache.TryGetNode(minNodeId, out var startNode))
                return;
            if (!_cache.TryGetNode(maxNodeId, out var endNode))
                return;
            if (!_cache.TryGetRailSegment(segmentId, out var railSegment))
                return;
            if (!railSegment.HasAnyDirection())
                return;

            var lineObject = SpawnRail($"RailLine_{minNodeId}_{maxNodeId}", startNode, endNode);
            ApplyRailSegment(lineObject, railSegment);
            lineObject.transform.SetParent(transform, false);
            _railObjs[segmentId] = lineObject;
        }

        private void RemoveLine(int fromNodeId, int toNodeId)
        {
            var segmentId = RailSegmentId.CreateCanonical(fromNodeId, toNodeId);
            if (!_railObjs.TryGetValue(segmentId, out var gobj))
            {
                return;
            }

            _railObjs.Remove(segmentId);
            if (gobj != null)
            {
                Destroy(gobj);
            }
        }

        private GameObject SpawnRail(string name, IRailNode startNode, IRailNode endNode)
        {
            var instance = Instantiate(_railPrefab, transform);
            var startControl = startNode.FrontControlPoint.OriginalPosition;
            var control1 = startNode.FrontControlPoint.ControlPointPosition + startControl;
            var endControl = endNode.BackControlPoint.OriginalPosition;
            var control2 = endNode.BackControlPoint.ControlPointPosition + endControl;

            instance.SetControlPoints(startControl, control1, control2, endControl);
            instance.SetRailGraphCache(_cache);
            instance.Rebuild();
            instance.name = name;
            return instance.gameObject;
        }

        private static bool IsValidIndex(IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> adjacency, int index)
        {
            return index >= 0 && index < adjacency.Count;
        }

        private static void ApplyRailSegment(GameObject lineObject, RailSegment railSegment)
        {
            if (lineObject == null)
                return;

            // レール用コライダーに区間情報を埋め込む
            // Embed the rail segment into colliders for raycast lookup
            var colliders = lineObject.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null)
                    continue;

                var carrier = collider.GetComponent<RailSegmentCarrier>();
                if (carrier == null)
                    carrier = collider.gameObject.AddComponent<RailSegmentCarrier>();
                carrier.SetRailSegment(railSegment);
            }
        }

        #endregion
    }
}
