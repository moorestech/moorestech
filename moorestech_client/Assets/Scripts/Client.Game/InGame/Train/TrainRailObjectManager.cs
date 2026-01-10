using System.Collections.Generic;
using UnityEngine;
using Game.Train.RailGraph;
using InGame.Train.Rail;

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
            // 重複チェック：既にこのペアのレールが存在する場合はスキップ
            // Duplicate check: skip if rail for this pair already exists
            var railObjectId = ComputeRailObjectId(fromNodeId, toNodeId);
            if (_railObjs.ContainsKey(railObjectId))
                return;

            // Opposite側のIDでも同じレールとして扱う
            // Treat opposite side IDs as the same rail
            var oppositeRailObjectId = ComputeRailObjectId(toNodeId ^ 1, fromNodeId ^ 1);
            if (_railObjs.ContainsKey(oppositeRailObjectId))
                return;

            // ノード取得（最初に来たイベントでレールを生成する）
            // Get nodes (generate rail on first event received)
            if (_cache == null)
                return;
            if (!_cache.TryGetNode(fromNodeId, out var startNode))
                return;
            if (!_cache.TryGetNode(toNodeId, out var endNode))
                return;

            var lineObject = SpawnRail($"RailLine_{fromNodeId}_{toNodeId}", startNode, endNode);
            lineObject.transform.SetParent(transform, false);
            _railObjs[railObjectId] = lineObject;
        }

        private void RemoveLine(int fromNodeId, int toNodeId)
        {
            // 元のIDとOpposite側のID両方をチェック
            // Check both original ID and opposite side ID
            var railObjectId = ComputeRailObjectId(fromNodeId, toNodeId);
            var oppositeRailObjectId = ComputeRailObjectId(toNodeId ^ 1, fromNodeId ^ 1);

            var targetId = _railObjs.ContainsKey(railObjectId) ? railObjectId : oppositeRailObjectId;
            if (!_railObjs.TryGetValue(targetId, out var gobj))
            {
                return;
            }

            _railObjs.Remove(targetId);
            if (gobj != null)
            {
                Destroy(gobj);
            }
        }

        private GameObject SpawnRail(string name, IRailNode startNode, IRailNode endNode)
        {
            var instance = Instantiate(_railPrefab, transform);

            // 両方とも外向き（FrontControlPoint）を使用
            // Use outward control points (FrontControlPoint) for both nodes
            var startControl = startNode.FrontControlPoint.OriginalPosition;
            var control1 = startNode.FrontControlPoint.ControlPointPosition + startControl;
            var endControl = endNode.FrontControlPoint.OriginalPosition;
            var control2 = endNode.FrontControlPoint.ControlPointPosition + endControl;

            instance.SetControlPoints(startControl, control1, control2, endControl);
            instance.Rebuild();
            instance.name = name;
            return instance.gameObject;
        }

        private static ulong ComputeRailObjectId(int canonicalFrom, int canonicalTo)
        {
            return (ulong)canonicalFrom + ((ulong)canonicalTo << 32);
        }

        #endregion
    }
}
