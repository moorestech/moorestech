using System.Collections.Generic;
using Client.UitkPoc.RecipeGraph.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.RecipeGraph.View
{
    // Painter2Dで辺を描く層。太さは流量、色は資源、流れは破線のオフセットを動かして見せる
    // Painter2D edge layer: width by flow, color by resource, flow shown by moving dash offset
    public class RecipeEdgeLayer : VisualElement
    {
        private ProductionGraph _graph;
        private RecipeEdgeRouter _router;
        private HashSet<int> _highlightedEdges;
        private float _dashOffset;
        private bool _flowAnimation = true;

        public RecipeEdgeLayer()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            generateVisualContent += Draw;
        }

        public void SetGraph(ProductionGraph graph, RecipeEdgeRouter router)
        {
            _graph = graph;
            _router = router;
            MarkDirtyRepaint();
        }

        public void SetHighlightedEdges(HashSet<int> highlightedEdges)
        {
            _highlightedEdges = highlightedEdges;
            MarkDirtyRepaint();
        }

        public void SetFlowAnimation(bool enabled)
        {
            _flowAnimation = enabled;
            MarkDirtyRepaint();
        }

        public void AdvanceFlow(float deltaSeconds)
        {
            if (!_flowAnimation) return;
            _dashOffset -= deltaSeconds * 40f;
            MarkDirtyRepaint();
        }

        public static Color ColorOf(string resourceName, bool isFluid)
        {
            // 名前のハッシュから色相を決め、液体は青系に寄せる
            // Hue from the name hash; fluids lean blue
            var hue = (resourceName.GetHashCode() & 0xffff) / 65535f;
            return isFluid ? Color.HSVToRGB(0.52f + hue * 0.12f, 0.6f, 0.95f) : Color.HSVToRGB(hue, 0.5f, 0.92f);
        }

        private void Draw(MeshGenerationContext context)
        {
            if (_graph == null) return;
            var painter = context.painter2D;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            // 1周目は実線の土台と矢印、2周目は流れの破線を重ねる
            // Pass 1 draws solid bases and arrows, pass 2 overlays flowing dashes
            for (var e = 0; e < _graph.Edges.Count; e++) DrawBase(e);
            if (!_flowAnimation) return;
            painter.SetDashPattern(10f, 14f);
            painter.dashOffset = _dashOffset;
            for (var e = 0; e < _graph.Edges.Count; e++) DrawFlow(e);

            #region Internal

            float Alpha(int e)
            {
                return _highlightedEdges == null || _highlightedEdges.Contains(e) ? 1f : 0.1f;
            }

            float WidthOf(ProductionEdge edge)
            {
                return Mathf.Clamp(2f + Mathf.Sqrt(edge.RatePerMinute) * 0.7f, 2f, 18f);
            }

            void DrawBase(int e)
            {
                var edge = _graph.Edges[e];
                var color = ColorOf(edge.ResourceName, edge.IsFluid);
                painter.strokeColor = new Color(color.r, color.g, color.b, (_flowAnimation ? 0.45f : 0.85f) * Alpha(e));
                painter.lineWidth = WidthOf(edge);
                StrokePath(_router.Paths[e]);

                // 消費側の端に矢印
                // Arrow at the consumer end
                var end = _router.Paths[e][^1];
                painter.fillColor = new Color(color.r, color.g, color.b, Alpha(e));
                painter.BeginPath();
                painter.MoveTo(end);
                painter.LineTo(end + new Vector2(-11f, -6f));
                painter.LineTo(end + new Vector2(-11f, 6f));
                painter.ClosePath();
                painter.Fill();
            }

            void DrawFlow(int e)
            {
                var edge = _graph.Edges[e];
                var color = Color.Lerp(ColorOf(edge.ResourceName, edge.IsFluid), Color.white, 0.35f);
                painter.strokeColor = new Color(color.r, color.g, color.b, Alpha(e));
                painter.lineWidth = Mathf.Max(1.5f, WidthOf(edge) * 0.45f);
                StrokePath(_router.Paths[e]);
            }

            void StrokePath(List<Vector2> path)
            {
                // 各区間を水平接線の3次ベジェで繋ぐ
                // Join each span with a cubic bezier whose tangents are horizontal
                painter.BeginPath();
                painter.MoveTo(path[0]);
                for (var i = 1; i < path.Count; i++)
                {
                    var from = path[i - 1];
                    var to = path[i];
                    var handle = Mathf.Max(30f, (to.x - from.x) * 0.5f);
                    painter.BezierCurveTo(from + new Vector2(handle, 0), to - new Vector2(handle, 0), to);
                }
                painter.Stroke();
            }

            #endregion
        }
    }
}
