using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.NodeGraph
{
    /// <summary>
    /// キャンバスの中身（線の層とノードの包み）を組み、押下の対象からノードのキーを引く。web: TreeView.tsx の renderedLines・renderedNodes と useTreePanGesture.ts の nodeIdAt
    /// Builds the canvas content (edge layer and node wrappers) and resolves a node key from a press target; web: renderedLines/renderedNodes in TreeView.tsx and nodeIdAt in useTreePanGesture.ts
    /// </summary>
    internal sealed class NodeGraphScene
    {
        private readonly VisualElement _canvas;
        private readonly NodeGraphEdgeLayer _edgeLayer = new();
        private readonly Dictionary<VisualElement, string> _keyByWrapper = new();
        private NodeGraphCanvasBounds _bounds;

        public NodeGraphScene(VisualElement canvas)
        {
            _canvas = canvas;
            _canvas.Add(_edgeLayer);
            ApplyBounds(NodeGraphGeometry.ComputeCanvasBounds(Array.Empty<Vector2>(), NodeGraphGeometry.CanvasPadding));
        }

        public bool HasNodes => 0 < _keyByWrapper.Count;

        public void Rebuild(IReadOnlyList<NodeGraphNode> nodes)
        {
            // 前回の包みを外し、重なったキーを除いた列で境界を決める
            // Remove the previous wrappers and size the canvas from the nodes without duplicate keys
            foreach (var wrapper in _keyByWrapper.Keys) wrapper.RemoveFromHierarchy();
            _keyByWrapper.Clear();
            var placedNodes = new List<NodeGraphNode>();
            var nodeByKey = IndexByKey();
            var positions = new List<Vector2>();
            foreach (var node in placedNodes) positions.Add(node.TreePosition);
            ApplyBounds(NodeGraphGeometry.ComputeCanvasBounds(positions, NodeGraphGeometry.CanvasPadding));

            // 線を先（下）、ノードを後（上）に入力の順で置く。z-index は使わず子の順で決める
            // Lines first (below), then nodes (above) in input order; child order decides, never z-index
            _edgeLayer.SetLines(BuildLines());
            foreach (var node in placedNodes) AddWrapper(node);

            #region Internal

            Dictionary<string, NodeGraphNode> IndexByKey()
            {
                // 重なったキーは後のノードを置かず開発者へ知らせる（Web は黙って上書きする。A-117）
                // A duplicate key drops the later node and tells the developer (the Web overwrites silently; A-117)
                var index = new Dictionary<string, NodeGraphNode>();
                foreach (var node in nodes)
                {
                    if (index.TryAdd(node.Key, node))
                    {
                        placedNodes.Add(node);
                        continue;
                    }
                    Debug.LogError($"[NodeGraphView] duplicate node key '{node.Key}' is not placed");
                }
                return index;
            }

            List<NodeGraphLine> BuildLines()
            {
                // 線は子の点を始点に親の点へ向かう。グラフに無い親は描かずに知らせる（Web は黙って飛ばす。A-117）
                // A line runs from the child point to the parent point; a parent missing from the graph is not drawn and is reported (the Web skips silently; A-117)
                var lines = new List<NodeGraphLine>();
                foreach (var node in placedNodes)
                {
                    foreach (var previousKey in node.PreviousKeys)
                    {
                        if (!nodeByKey.TryGetValue(previousKey, out var previous))
                        {
                            Debug.LogError($"[NodeGraphView] node '{node.Key}' refers to missing previous key '{previousKey}'; the line is not drawn");
                            continue;
                        }
                        lines.Add(NodeGraphGeometry.LineBetween(ToCanvasPoint(node.TreePosition), ToCanvasPoint(previous.TreePosition)));
                    }
                }
                return lines;
            }

            void AddWrapper(NodeGraphNode node)
            {
                // 包みは点に置く幅 0・高さは中身なりの要素。拾わず、中のカードが押下を受ける
                // The wrapper is a zero-width, content-height element at the point; it does not pick, and the card inside receives presses
                var point = ToCanvasPoint(node.TreePosition);
                var wrapper = new VisualElement { pickingMode = PickingMode.Ignore };
                wrapper.AddToClassList("node-graph__node");
                wrapper.style.left = point.x;
                wrapper.style.top = point.y;
                wrapper.Add(node.Card);
                _canvas.Add(wrapper);
                _keyByWrapper.Add(wrapper, node.Key);
            }

            #endregion
        }

        public Vector2 ToCanvasPoint(Vector2 treePoint)
        {
            return NodeGraphGeometry.ToCanvasPoint(treePoint, _bounds);
        }

        public bool ContainsKey(string key)
        {
            return _keyByWrapper.ContainsValue(key);
        }

        public bool TryFindNodeKey(VisualElement pressTarget, out string key)
        {
            // 押下の対象から親を辿り、最初に見つかった包みのキーを返す（Web の closest）
            // Walk up from the press target and return the first wrapper's key (the Web closest)
            for (var current = pressTarget; current != null && current != _canvas; current = current.parent)
            {
                if (_keyByWrapper.TryGetValue(current, out key)) return true;
            }
            key = null;
            return false;
        }

        private void ApplyBounds(NodeGraphCanvasBounds bounds)
        {
            _bounds = bounds;
            _canvas.style.width = bounds.Width;
            _canvas.style.height = bounds.Height;
        }
    }
}
