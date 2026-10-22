using System.Collections.Generic;
using System.Linq;
using Client.UitkPoc.VectorEditor.Geometry;
using Client.UitkPoc.VectorEditor.Model;
using Client.UitkPoc.VectorEditor.View;
using UnityEngine;

namespace Client.UitkPoc.VectorEditor.Interaction
{
    // 選択中の図形をまとめて動かす。表示範囲内の他の図形の辺・中心へ吸着させる
    // Moves the selection together, snapping to edges and centers of other on-screen shapes
    public class MoveDragSession : IPointerDragSession
    {
        private const float GuideThresholdPixels = 5f;

        private readonly VectorCanvas _canvas;
        private readonly Vector2 _startWorld;
        private readonly Dictionary<VectorShape, ShapeTransform> _before;
        private readonly Rect _startBounds;
        private readonly VectorShape _pressedShape;
        private readonly bool _additive;
        private List<Rect> _others;

        public MoveDragSession(VectorCanvas canvas, Vector2 startWorld, VectorShape pressedShape, bool additive)
        {
            _canvas = canvas;
            _startWorld = startWorld;
            _pressedShape = pressedShape;
            _additive = additive;
            _before = canvas.Selected.ToDictionary(s => s, s => s.Transform);
            _startBounds = _before.Keys.Select(s => s.WorldBounds()).Aggregate(Union);
        }

        public void Drag(Vector2 world, Vector2 local, bool shift)
        {
            // 最初のドラッグで動かす図形を操作層へ移し、吸着候補を表示範囲内に絞って集める
            // On the first drag, move shapes to the active layer and gather on-screen snap candidates
            if (_others == null)
            {
                var view = _canvas.ViewportWorld;
                _others = _canvas.Document.Shapes.Where(s => !_before.ContainsKey(s)).Select(s => s.WorldBounds()).Where(b => b.Overlaps(view)).ToList();
                _canvas.BeginInteraction(_before.Keys);
            }
            var delta = world - _startWorld;
            var moved = new Rect(_startBounds.position + delta, _startBounds.size);
            var snap = SmartGuides.Snap(moved, _others, GuideThresholdPixels / _canvas.Zoom);
            delta += snap.Offset;
            foreach (var (shape, transform) in _before) shape.SetTransform(new ShapeTransform(transform.Center + delta, transform.Size, transform.RotationDegrees));
            _canvas.Overlay.SetGuides(snap.GuideX, snap.GuideY);
            _canvas.RefreshActive();
        }

        public void Release(bool dragged)
        {
            // 動かさずに離したら、押した図形だけの選択に絞る（複数選択の中の1つをクリックした場合）
            // Releasing without a drag narrows the selection to the pressed shape (a click inside a multi-selection)
            if (!dragged)
            {
                if (!_additive) _canvas.SetSelection(new[] { _pressedShape });
                return;
            }
            _canvas.Overlay.SetGuides(null, null);
            var after = _before.Keys.ToDictionary(s => s, s => s.Transform);
            _canvas.History.Record(new TransformShapesCommand(_canvas.Document, _before, after));
            _canvas.EndInteraction();
        }

        private static Rect Union(Rect a, Rect b)
        {
            return Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        }
    }
}
