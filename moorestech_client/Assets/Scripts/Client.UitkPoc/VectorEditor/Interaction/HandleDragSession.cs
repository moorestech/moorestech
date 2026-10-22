using System.Collections.Generic;
using Client.UitkPoc.VectorEditor.Model;
using Client.UitkPoc.VectorEditor.View;
using UnityEngine;

namespace Client.UitkPoc.VectorEditor.Interaction
{
    // 1つの図形の取っ手操作。8方向は対辺（対角）を固定した大きさ変更、残る1つは回転
    // Handle drag on one shape: 8 handles resize around the opposite side/corner, the last one rotates
    public class HandleDragSession : IPointerDragSession
    {
        private const float MinSize = 1f;
        private const float RotationStepDegrees = 15f;

        private readonly VectorCanvas _canvas;
        private readonly VectorShape _shape;
        private readonly int _handle;
        private readonly ShapeTransform _before;
        private readonly Vector2 _startWorld;
        private bool _started;

        public HandleDragSession(VectorCanvas canvas, int handle, Vector2 startWorld)
        {
            _canvas = canvas;
            _shape = canvas.Overlay.SingleSelection;
            _handle = handle;
            _before = _shape.Transform;
            _startWorld = startWorld;
        }

        public void Drag(Vector2 world, Vector2 local, bool shift)
        {
            if (!_started)
            {
                _started = true;
                _canvas.BeginInteraction(new[] { _shape });
            }
            _shape.SetTransform(_handle == SelectionOverlayElement.RotateHandle ? Rotated(world, shift) : Resized(world, shift));
            _canvas.RefreshActive();
        }

        public void Release(bool dragged)
        {
            if (!dragged) return;
            var before = new Dictionary<VectorShape, ShapeTransform> { [_shape] = _before };
            var after = new Dictionary<VectorShape, ShapeTransform> { [_shape] = _shape.Transform };
            _canvas.History.Record(new TransformShapesCommand(_canvas.Document, before, after));
            _canvas.EndInteraction();
        }

        private ShapeTransform Rotated(Vector2 world, bool shift)
        {
            var center = _before.Center;
            var delta = Vector2.SignedAngle(_startWorld - center, world - center);
            var angle = _before.RotationDegrees + delta;
            if (shift) angle = Mathf.Round(angle / RotationStepDegrees) * RotationStepDegrees;
            return new ShapeTransform(center, _before.Size, angle);
        }

        private ShapeTransform Resized(Vector2 world, bool shift)
        {
            // 固定点からポインタまでを図形の回転を外した座標で測り、動く軸だけ新しい幅・高さにする
            // Measure fixed point to pointer in unrotated shape space and update only the axes this handle moves
            var handle = SelectionOverlayElement.HandleUnit(_handle);
            var fixedPoint = _before.ToWorld(-handle);
            var reach = ShapeTransform.Rotate(world - fixedPoint, -_before.RotationDegrees);
            var width = handle.x != 0 ? Mathf.Max(MinSize, reach.x * Mathf.Sign(handle.x)) : _before.Size.x;
            var height = handle.y != 0 ? Mathf.Max(MinSize, reach.y * Mathf.Sign(handle.y)) : _before.Size.y;
            if (shift && handle.x != 0 && handle.y != 0)
            {
                var ratio = Mathf.Max(width / _before.Size.x, height / _before.Size.y);
                width = _before.Size.x * ratio;
                height = _before.Size.y * ratio;
            }
            var centerOffset = new Vector2(Mathf.Sign(handle.x) * width * 0.5f, Mathf.Sign(handle.y) * height * 0.5f);
            if (handle.x == 0) centerOffset.x = 0;
            if (handle.y == 0) centerOffset.y = 0;
            return new ShapeTransform(fixedPoint + ShapeTransform.Rotate(centerOffset, _before.RotationDegrees), new Vector2(width, height), _before.RotationDegrees);
        }
    }
}
