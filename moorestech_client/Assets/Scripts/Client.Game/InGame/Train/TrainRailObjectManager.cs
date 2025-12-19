using System.Collections.Generic;
using Game.Train.RailGraph;
using Game.Train.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace Client.Game.InGame.Train
{
    /// <summary>
    ///     キャッシュ更新に追随してレールラインを生成/削除する管理クラス
    ///     Manages runtime line renderers driven directly by rail cache updates
    /// </summary>
    public sealed class TrainRailObjectManager : MonoBehaviour
    {
        public static TrainRailObjectManager Instance { get; private set; }

        [SerializeField] private float lineWidth = 0.05f;
        [SerializeField] private Color lineColor = Color.cyan;
        [SerializeField] private int curveSegments = 16;

        private readonly Dictionary<ulong, LineRenderer> _railLines = new();
        private RailGraphClientCache _cache;
        private Material _lineMaterial;

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

            foreach (var renderer in _railLines.Values)
            {
                if (renderer != null)
                {
                    Destroy(renderer.gameObject);
                }
            }
            _railLines.Clear();
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
            foreach (var renderer in _railLines.Values)
            {
                if (renderer != null)
                {
                    Destroy(renderer.gameObject);
                }
            }
            _railLines.Clear();

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
            if (_railLines.ContainsKey(railObjectId))
                return;
            if (_cache == null)
                return;
            if (!_cache.TryGetNode(canonicalFrom, out var startNode))
                return;
            if (!_cache.TryGetNode(canonicalTo, out var endNode))
                return;

            var lineObject = new GameObject($"RailLine_{canonicalFrom}_{canonicalTo}");
            lineObject.transform.SetParent(transform, false);
            var renderer = lineObject.AddComponent<LineRenderer>();
            ConfigureRenderer(renderer);
            UpdateLineRenderer(renderer, startNode, endNode);
            _railLines[railObjectId] = renderer;
        }

        private void RemoveLine(int fromNodeId, int toNodeId)
        {
            var (canonicalFrom, canonicalTo) = SelectCanonicalPair(fromNodeId, toNodeId);
            var railObjectId = ComputeRailObjectId(canonicalFrom, canonicalTo);
            if (!_railLines.TryGetValue(railObjectId, out var renderer))
            {
                return;
            }

            _railLines.Remove(railObjectId);
            if (renderer != null)
            {
                Destroy(renderer.gameObject);
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

        private void UpdateLineRenderer(LineRenderer renderer, IRailNode startNode, IRailNode endNode)
        {
            // ベジエ曲線のサンプル数を算出
            // Determine how many segments the curve will use
            int segmentCount = Mathf.Max(1, curveSegments);
            renderer.positionCount = segmentCount + 1;

            var startControl = startNode.FrontControlPoint;
            var endControl = endNode.BackControlPoint;

            // 共有ユーティリティで各頂点を求める
            // Use the shared Bezier utility to compute each vertex
            for (int i = 0; i <= segmentCount; i++)
            {
                float t = (float)i / segmentCount;
                Vector3 point = BezierUtility.GetBezierPoint(startControl, endControl, t);
                renderer.SetPosition(i, point);
            }
        }

        private void ConfigureRenderer(LineRenderer renderer)
        {
            renderer.material = GetLineMaterial();
            renderer.widthMultiplier = Mathf.Max(0.001f, lineWidth);
            renderer.useWorldSpace = true;
            renderer.startColor = lineColor;
            renderer.endColor = lineColor;
            renderer.alignment = LineAlignment.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.textureMode = LineTextureMode.Stretch;
            renderer.numCapVertices = 2;
        }

        private Material GetLineMaterial()
        {
            if (_lineMaterial != null)
                return _lineMaterial;

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            _lineMaterial = new Material(shader)
            {
                color = lineColor
            };
            return _lineMaterial;
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
