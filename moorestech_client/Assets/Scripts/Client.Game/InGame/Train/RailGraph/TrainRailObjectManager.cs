using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Train.RailGraph;
using Game.Train.RailCalc;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    /// <summary>
    ///     レールキャッシュ更新に追従するランタイム描画を管理するクラス
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
            ApplyRailObjectId(lineObject, railObjectId);
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
                UniTask.Create(async () =>
                {
                    var bezierRailChain = gobj.GetComponent<BezierRailChain>();
                    await bezierRailChain.RemoveAnimation();
                    Destroy(gobj);
                }).Forget();
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
            // 描画用の制御点を生成
            // Build render control points
            BezierUtility.BuildRenderControlPoints(startNode.FrontControlPoint, endNode.BackControlPoint, out var p0, out var p1, out var p2, out var p3);
            instance.SetControlPoints(p0, p1, p2, p3);
            instance.SetRailGraphCache(_cache);
            instance.Rebuild();
            instance.PlaceAnimation().Forget();
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

        private static void ApplyRailObjectId(GameObject lineObject, ulong railObjectId)
        {
            if (lineObject == null)
                return;

            // レール用コライダーにIDを埋め込む
            // Embed the rail object id into colliders for raycast lookup
            var colliders = lineObject.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null)
                    continue;

                var carrier = collider.GetComponent<RailObjectIdCarrier>();
                if (carrier == null)
                    carrier = collider.gameObject.AddComponent<RailObjectIdCarrier>();
                carrier.SetRailObjectId(railObjectId);
            }
        }

        #endregion
    }
}
