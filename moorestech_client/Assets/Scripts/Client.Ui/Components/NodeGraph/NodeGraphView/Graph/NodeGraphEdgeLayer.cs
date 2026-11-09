using System;
using System.Collections.Generic;
using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.NodeGraph
{
    /// <summary>
    /// ノードの間の線を Painter2D で描く層（キャンバスを覆い、ノードの下に置く）。web: TreeView.module.css の .line
    /// Layer that strokes the lines between nodes with Painter2D (covers the canvas, sits below the nodes); web: .line in TreeView.module.css
    /// </summary>
    internal sealed class NodeGraphEdgeLayer : VisualElement
    {
        private const string EdgeColorProperty = "--node-graph-edge-color";
        private const string EdgeWidthProperty = "--node-graph-edge-width";

        private IReadOnlyList<NodeGraphLine> _lines = Array.Empty<NodeGraphLine>();
        private Color _edgeColor;
        private float _edgeWidth;
        private bool _hasStroke;

        public NodeGraphEdgeLayer()
        {
            AddToClassList("node-graph__edges");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawEdges;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        public void SetLines(IReadOnlyList<NodeGraphLine> lines)
        {
            _lines = lines;
            MarkDirtyRepaint();
        }

        public static Vector2 StrokeStart(NodeGraphLine line, float width)
        {
            // Web の線は高さ w の帯を左端の中央（transform-origin: 0 50%）で回すので、中心線は点から w/2 下を通る（A-117）
            // The Web line is a w-tall band rotated about its left-middle (transform-origin: 0 50%), so the center line runs w/2 below the point (A-117)
            return line.Origin + new Vector2(0f, width / 2f);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            // 読めなくなったときも描き直し、前回の線を残さない。読めない名前は UssCustomStyleReader が LogError で出す
            // Repaint even when unreadable so stale lines never linger; UssCustomStyleReader logs any unresolved name
            var hasColor = UssCustomStyleReader.TryReadColor(customStyle, EdgeColorProperty, out _edgeColor);
            var hasWidth = UssCustomStyleReader.TryReadPixels(customStyle, EdgeWidthProperty, out _edgeWidth);
            _hasStroke = hasColor && hasWidth;
            MarkDirtyRepaint();
        }

        private void DrawEdges(MeshGenerationContext context)
        {
            // silent-ok: 色と太さの解決前は描かない / 解決できなければ UssCustomStyleReader が LogError を出す
            if (!_hasStroke) return;

            // 端は平ら（Web の帯の端と同じ）で、全部の線を 1 本のパスにまとめて描く
            // Butt ends (like the Web band ends); all lines go into one path
            var painter = context.painter2D;
            painter.strokeColor = _edgeColor;
            painter.lineWidth = _edgeWidth;
            painter.lineCap = LineCap.Butt;
            painter.BeginPath();
            foreach (var line in _lines)
            {
                var start = StrokeStart(line, _edgeWidth);
                var radians = line.AngleDegrees * Mathf.Deg2Rad;
                painter.MoveTo(start);
                painter.LineTo(start + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * line.Length);
            }
            painter.Stroke();
        }
    }
}
