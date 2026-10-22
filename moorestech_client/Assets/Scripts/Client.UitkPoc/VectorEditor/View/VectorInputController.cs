using System.Linq;
using Client.UitkPoc.VectorEditor.Geometry;
using Client.UitkPoc.VectorEditor.Interaction;
using Client.UitkPoc.VectorEditor.Model;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.VectorEditor.View
{
    // ポインタとキーの振り分け。押した時点のツールと当たり先で操作の種類を決め、以降はその操作に任せる
    // Routes pointer and key input; the tool and hit target at press time pick the gesture, which then owns the drag
    public class VectorInputController
    {
        private const float DragThreshold = 3f;
        private const float HitTolerancePixels = 4f;

        private readonly VectorCanvas _canvas;
        private IPointerDragSession _session;
        private PenToolSession _pen;
        private bool _panning;
        private Vector2 _pressLocal;
        private bool _dragged;

        public VectorInputController(VectorCanvas canvas)
        {
            _canvas = canvas;
            canvas.RegisterCallback<PointerDownEvent>(OnPointerDown);
            canvas.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            canvas.RegisterCallback<PointerUpEvent>(OnPointerUp);
            canvas.RegisterCallback<WheelEvent>(OnWheel);
            canvas.RegisterCallback<KeyDownEvent>(OnKeyDown);
            canvas.ToolChanged.Where(tool => tool != VectorTool.Pen).Subscribe(_ => CommitPen(false));
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            _canvas.Focus();
            var local = _canvas.WorldToLocal(evt.position);
            _pressLocal = local;
            _dragged = false;
            _panning = evt.button != 0;
            _session = _panning ? null : CreateSession(local, _canvas.ToWorld(local), evt.shiftKey);
            if (!_panning && _session == null) return;
            _canvas.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private IPointerDragSession CreateSession(Vector2 local, Vector2 world, bool shift)
        {
            var tool = _canvas.Tool;
            if (tool == VectorTool.Rectangle) return new DrawShapeSession(_canvas, ShapeKind.Rectangle, world);
            if (tool == VectorTool.Ellipse) return new DrawShapeSession(_canvas, ShapeKind.Ellipse, world);
            if (tool == VectorTool.Pen)
            {
                _pen ??= new PenToolSession(_canvas);
                if (_pen.Press(world, local)) return _pen;
                _pen = null;
                _canvas.SetTool(VectorTool.Select);
                return null;
            }

            // 選択ツール: 取っ手 → 図形 → 何も無い所（範囲選択）の順に判定する
            // Select tool: test handles, then shapes, then empty space (marquee)
            var handle = _canvas.Overlay.HandleAt(local);
            if (handle >= 0) return new HandleDragSession(_canvas, handle, world);
            var hit = ShapeHitTester.TopmostAt(_canvas.Document.Shapes, world, HitTolerancePixels / _canvas.Zoom);
            if (hit == null) return new MarqueeSession(_canvas, local, shift);
            if (shift) _canvas.SetSelection(_canvas.Selected.Contains(hit) ? _canvas.Selected.Where(s => s != hit).ToList() : _canvas.Selected.Append(hit).ToList());
            else if (!_canvas.Selected.Contains(hit)) _canvas.SetSelection(new[] { hit });
            return _canvas.Selected.Contains(hit) ? new MoveDragSession(_canvas, world, hit, shift) : null;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_canvas.HasPointerCapture(evt.pointerId)) return;
            var local = _canvas.WorldToLocal(evt.position);
            _dragged |= Vector2.Distance(local, _pressLocal) > DragThreshold;
            if (_panning) _canvas.SetView(_canvas.Translation + (Vector2)evt.deltaPosition, _canvas.Zoom);
            else if (_dragged) _session.Drag(_canvas.ToWorld(local), local, evt.shiftKey);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_canvas.HasPointerCapture(evt.pointerId)) return;
            _canvas.ReleasePointer(evt.pointerId);
            if (!_panning) _session.Release(_dragged);
            _session = null;
            evt.StopPropagation();
        }

        private void OnWheel(WheelEvent evt)
        {
            _canvas.ZoomAt(_canvas.WorldToLocal(evt.mousePosition), _canvas.Zoom * Mathf.Exp(-evt.delta.y * 0.08f));
            evt.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            var history = _canvas.History;
            if (evt.actionKey && evt.keyCode == KeyCode.Z && !evt.shiftKey) history.Undo();
            else if (evt.actionKey && (evt.keyCode == KeyCode.Y || (evt.keyCode == KeyCode.Z && evt.shiftKey))) history.Redo();
            else if (evt.actionKey && evt.keyCode == KeyCode.A) _canvas.SetSelection(_canvas.Document.Shapes);
            else if (evt.actionKey) return;
            else if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter or KeyCode.Escape) _canvas.SetTool(VectorTool.Select);
            else if (evt.keyCode is KeyCode.Delete or KeyCode.Backspace && _canvas.Selected.Count > 0)
                history.Execute(new DeleteShapesCommand(_canvas.Document, _canvas.Selected.Select(s => s.Id).ToHashSet()));
            else if (evt.keyCode == KeyCode.V) _canvas.SetTool(VectorTool.Select);
            else if (evt.keyCode == KeyCode.R) _canvas.SetTool(VectorTool.Rectangle);
            else if (evt.keyCode == KeyCode.O) _canvas.SetTool(VectorTool.Ellipse);
            else if (evt.keyCode == KeyCode.P) _canvas.SetTool(VectorTool.Pen);
        }

        private void CommitPen(bool closed)
        {
            if (_pen == null) return;
            _pen.Commit(closed);
            _pen = null;
        }
    }
}
