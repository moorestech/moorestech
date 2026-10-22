using Client.UitkPoc.VectorEditor.Model;
using Client.UitkPoc.VectorEditor.View;
using UnityEngine;

namespace Client.UitkPoc.VectorEditor.Interaction
{
    // 矩形・楕円ツール。押した点から引いた範囲で作り、クリックだけなら既定の大きさで置く
    // Rectangle/ellipse tool: sized by the dragged span, or a default size on a plain click
    public class DrawShapeSession : IPointerDragSession
    {
        private const float ClickSize = 100f;

        private readonly VectorCanvas _canvas;
        private readonly VectorShape _draft;
        private readonly Vector2 _startWorld;
        private bool _started;

        public DrawShapeSession(VectorCanvas canvas, ShapeKind kind, Vector2 startWorld)
        {
            _canvas = canvas;
            _startWorld = startWorld;
            var transform = new ShapeTransform(startWorld, Vector2.one * ClickSize, 0f);
            _draft = new VectorShape(canvas.Document.AllocateId(), kind, transform, VectorPalette.NextFill(), VectorPalette.Stroke, 2f, null, false);
        }

        public void Drag(Vector2 world, Vector2 local, bool shift)
        {
            if (!_started)
            {
                _started = true;
                _canvas.BeginInteraction(new[] { _draft });
            }

            // Shiftで正方形・正円にする。端点は押した点からポインタ側へ伸ばす
            // Shift forces a square/circle; the far corner extends from the press point toward the pointer
            var span = world - _startWorld;
            var size = new Vector2(Mathf.Abs(span.x), Mathf.Abs(span.y));
            if (shift) size = Vector2.one * Mathf.Max(size.x, size.y);
            var end = _startWorld + new Vector2(Mathf.Sign(span.x) * size.x, Mathf.Sign(span.y) * size.y);
            _draft.SetTransform(new ShapeTransform((_startWorld + end) * 0.5f, Vector2.Max(size, Vector2.one), 0f));
            _canvas.RefreshActive();
        }

        public void Release(bool dragged)
        {
            // 適用してから履歴へ積む（ドラッグ移動と同じ順）。描き直しはEndInteractionの1回だけにする
            // Apply first, then record (same order as drag moves) so EndInteraction redraws exactly once
            _canvas.Document.Shapes.Add(_draft);
            _canvas.History.Record(new AddShapesCommand(_canvas.Document, new System.Collections.Generic.List<VectorShape> { _draft }));
            _canvas.EndInteraction();
            _canvas.SetSelection(new[] { _draft });
            _canvas.SetTool(VectorTool.Select);
        }
    }
}
