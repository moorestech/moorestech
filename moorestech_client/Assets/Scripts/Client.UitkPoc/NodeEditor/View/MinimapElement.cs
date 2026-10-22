using System;
using System.Collections.Generic;
using Client.UitkPoc.NodeEditor.Model;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.NodeEditor.View
{
    // 全ノードと今の表示範囲を縮小表示し、押した場所へ表示範囲を移す
    // Shrunk view of all nodes and the current viewport; pressing moves the viewport there
    public class MinimapElement : VisualElement
    {
        private const float Padding = 8f;
        private static readonly Color NodeColor = new(0.45f, 0.5f, 0.58f);
        private static readonly Color SelectedColor = new(0.95f, 0.65f, 0.25f);
        private static readonly Color ViewportColor = new(1f, 1f, 1f, 0.8f);

        private readonly NodeGraphModel _model;
        private readonly HashSet<int> _selectedNodes;
        private readonly Subject<Vector2> _centerRequested = new();
        private Rect _viewportContent;
        private Rect _bounds;
        private float _fitScale = 1f;

        public IObservable<Vector2> CenterRequested => _centerRequested;

        public MinimapElement(NodeGraphModel model, HashSet<int> selectedNodes)
        {
            _model = model;
            _selectedNodes = selectedNodes;
            AddToClassList("ne-minimap");
            generateVisualContent += Draw;
            RegisterCallback<PointerDownEvent>(evt => { this.CapturePointer(evt.pointerId); RequestCenter(evt.localPosition); evt.StopPropagation(); });
            RegisterCallback<PointerMoveEvent>(evt => { if (this.HasPointerCapture(evt.pointerId)) RequestCenter(evt.localPosition); evt.StopPropagation(); });
            RegisterCallback<PointerUpEvent>(evt => { this.ReleasePointer(evt.pointerId); evt.StopPropagation(); });
        }

        public void SetViewport(Rect viewportInContent)
        {
            _viewportContent = viewportInContent;
            MarkDirtyRepaint();
        }

        private void RequestCenter(Vector2 local)
        {
            _centerRequested.OnNext(_bounds.min + (local - Vector2.one * Padding) / _fitScale);
        }

        private void Draw(MeshGenerationContext context)
        {
            // 全ノードと表示範囲を包む矩形を求め、余白を残して枠に収める
            // Fit the box enclosing all nodes and the viewport into the frame with padding
            _bounds = _viewportContent;
            foreach (var node in _model.Nodes.Values) _bounds = Union(_bounds, new Rect(node.Position, node.Size));
            var frame = contentRect.size - Vector2.one * Padding * 2f;
            _fitScale = Mathf.Min(frame.x / Mathf.Max(_bounds.width, 1f), frame.y / Mathf.Max(_bounds.height, 1f));

            var painter = context.painter2D;
            foreach (var node in _model.Nodes.Values)
            {
                painter.fillColor = _selectedNodes.Contains(node.Id) ? SelectedColor : NodeColor;
                var rect = ToMinimap(new Rect(node.Position, node.Size));
                painter.BeginPath();
                painter.MoveTo(rect.min);
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(rect.max);
                painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                painter.ClosePath();
                painter.Fill();
            }
            var view = ToMinimap(_viewportContent);
            painter.strokeColor = ViewportColor;
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            painter.MoveTo(view.min);
            painter.LineTo(new Vector2(view.xMax, view.yMin));
            painter.LineTo(view.max);
            painter.LineTo(new Vector2(view.xMin, view.yMax));
            painter.ClosePath();
            painter.Stroke();
        }

        private Rect ToMinimap(Rect content)
        {
            return new Rect(Vector2.one * Padding + (content.min - _bounds.min) * _fitScale, content.size * _fitScale);
        }

        private static Rect Union(Rect a, Rect b)
        {
            return Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        }
    }
}
