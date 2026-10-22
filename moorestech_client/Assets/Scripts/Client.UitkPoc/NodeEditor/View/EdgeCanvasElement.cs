using System.Collections.Generic;
using Client.UitkPoc.NodeEditor.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.NodeEditor.View
{
    // 全ての辺と、接続中の仮の線を1枚のPainter2Dで描く。座標は内容座標（パン・ズーム前）
    // Draws every edge plus the in-progress connection with one Painter2D, in content coordinates
    public class EdgeCanvasElement : VisualElement
    {
        private const int HitSamples = 20;
        private static readonly Color EdgeColor = new(0.55f, 0.6f, 0.68f);
        private static readonly Color SelectedColor = new(0.95f, 0.65f, 0.25f);

        private readonly NodeGraphModel _model;
        private readonly HashSet<int> _movingNodes;
        private readonly bool _drawsMovingEdges;
        private int _selectedEdgeId = -1;
        private bool _previewActive;
        private Vector2 _previewFrom;
        private Vector2 _previewTo;

        // movingNodesに触れる辺を描くのがdrawsMovingEdges=trueの層、触れない辺を描くのがfalseの層
        // The drawsMovingEdges=true layer draws edges touching movingNodes; the false layer draws the rest
        public EdgeCanvasElement(NodeGraphModel model, HashSet<int> movingNodes, bool drawsMovingEdges)
        {
            _model = model;
            _movingNodes = movingNodes;
            _drawsMovingEdges = drawsMovingEdges;
            pickingMode = PickingMode.Ignore;
            AddToClassList("ne-edges");
            generateVisualContent += Draw;
        }

        public void SetSelectedEdge(int edgeId)
        {
            _selectedEdgeId = edgeId;
            MarkDirtyRepaint();
        }

        public void SetPreview(bool active, Vector2 from, Vector2 to)
        {
            _previewActive = active;
            _previewFrom = from;
            _previewTo = to;
            MarkDirtyRepaint();
        }

        public int HitTest(Vector2 contentPoint, float tolerance)
        {
            // 曲線を折れ線に分けて点との距離を測る。Painter2Dには当たり判定が無いため自前で持つ
            // Flatten each curve and measure point distance; Painter2D has no hit testing of its own
            foreach (var edge in _model.Edges.Values)
            {
                var (from, to) = Endpoints(edge);
                var previous = from;
                for (var i = 1; i <= HitSamples; i++)
                {
                    var current = Bezier(from, to, i / (float)HitSamples);
                    if (DistanceToSegment(contentPoint, previous, current) <= tolerance) return edge.Id;
                    previous = current;
                }
            }
            return -1;
        }

        private void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            painter.lineWidth = 2.5f;
            painter.lineCap = LineCap.Round;
            foreach (var edge in _model.Edges.Values)
            {
                var moving = _movingNodes.Contains(edge.FromNode) || _movingNodes.Contains(edge.ToNode);
                if (moving != _drawsMovingEdges) continue;
                var (from, to) = Endpoints(edge);
                painter.strokeColor = edge.Id == _selectedEdgeId ? SelectedColor : EdgeColor;
                Stroke(painter, from, to);
            }
            if (!_previewActive) return;
            painter.strokeColor = SelectedColor;
            painter.SetDashPattern(6f, 5f);
            Stroke(painter, _previewFrom, _previewTo);
        }

        private (Vector2 from, Vector2 to) Endpoints(EditorEdge edge)
        {
            return (EditorNodeElement.PortPosition(_model.Nodes[edge.FromNode], false, edge.FromPort),
                EditorNodeElement.PortPosition(_model.Nodes[edge.ToNode], true, edge.ToPort));
        }

        private static void Stroke(Painter2D painter, Vector2 from, Vector2 to)
        {
            var (c1, c2) = Controls(from, to);
            painter.BeginPath();
            painter.MoveTo(from);
            painter.BezierCurveTo(c1, c2, to);
            painter.Stroke();
        }

        private static (Vector2, Vector2) Controls(Vector2 from, Vector2 to)
        {
            var reach = Mathf.Max(40f, Mathf.Abs(to.x - from.x) * 0.5f);
            return (from + new Vector2(reach, 0), to - new Vector2(reach, 0));
        }

        private static Vector2 Bezier(Vector2 from, Vector2 to, float t)
        {
            var (c1, c2) = Controls(from, to);
            var u = 1f - t;
            return u * u * u * from + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * to;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            return Vector2.Distance(point, a + ab * t);
        }
    }
}
