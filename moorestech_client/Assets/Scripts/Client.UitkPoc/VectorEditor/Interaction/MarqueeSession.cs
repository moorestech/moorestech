using System.Collections.Generic;
using System.Linq;
using Client.UitkPoc.VectorEditor.Model;
using Client.UitkPoc.VectorEditor.View;
using UnityEngine;

namespace Client.UitkPoc.VectorEditor.Interaction
{
    // 何も無い所からのドラッグで、枠に掛かった図形を選ぶ。Shift押下時は元の選択に足す
    // Dragging from empty space selects shapes touching the frame; with Shift it adds to the prior selection
    public class MarqueeSession : IPointerDragSession
    {
        private readonly VectorCanvas _canvas;
        private readonly Vector2 _startLocal;
        private readonly bool _additive;
        private readonly HashSet<VectorShape> _initial;

        public MarqueeSession(VectorCanvas canvas, Vector2 startLocal, bool additive)
        {
            _canvas = canvas;
            _startLocal = startLocal;
            _additive = additive;
            _initial = additive ? new HashSet<VectorShape>(canvas.Selected) : new HashSet<VectorShape>();
        }

        public void Drag(Vector2 world, Vector2 local, bool shift)
        {
            var frame = Rect.MinMaxRect(Mathf.Min(local.x, _startLocal.x), Mathf.Min(local.y, _startLocal.y), Mathf.Max(local.x, _startLocal.x), Mathf.Max(local.y, _startLocal.y));
            _canvas.Overlay.SetMarquee(true, frame);
            var worldFrame = new Rect(_canvas.ToWorld(frame.min), frame.size / _canvas.Zoom);
            _canvas.SetSelection(_initial.Concat(_canvas.Document.Shapes.Where(s => worldFrame.Overlaps(s.WorldBounds()))));
        }

        public void Release(bool dragged)
        {
            _canvas.Overlay.SetMarquee(false, Rect.zero);
            if (!dragged && !_additive) _canvas.SetSelection(Enumerable.Empty<VectorShape>());
        }
    }
}
