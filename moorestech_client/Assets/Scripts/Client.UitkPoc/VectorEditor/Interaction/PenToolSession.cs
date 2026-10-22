using System.Collections.Generic;
using Client.UitkPoc.VectorEditor.Geometry;
using Client.UitkPoc.VectorEditor.Model;
using Client.UitkPoc.VectorEditor.View;
using UnityEngine;

namespace Client.UitkPoc.VectorEditor.Interaction
{
    // ペンツール。クリックで角の点、ドラッグで滑らかな点（取っ手は対称）、最初の点を押すと閉じて確定する
    // Pen tool: click adds a corner, drag makes a smooth point (symmetric handles), pressing the first point closes it
    public class PenToolSession : IPointerDragSession
    {
        private const float ClosePickRadius = 8f;

        private readonly VectorCanvas _canvas;
        private readonly List<PathPoint> _points = new();
        private readonly VectorShape _draft;

        public PenToolSession(VectorCanvas canvas)
        {
            _canvas = canvas;
            // 下書きは恒等変換にして、単位座標＝ワールド座標として点を持つ
            // The draft uses an identity transform so its unit coordinates are world coordinates
            var identity = new ShapeTransform(Vector2.zero, Vector2.one, 0f);
            _draft = new VectorShape(canvas.Document.AllocateId(), ShapeKind.Path, identity, Color.clear, VectorPalette.Stroke, 2f, _points, false);
        }

        // 点を足したらtrue。最初の点を押して閉じた場合は確定してfalseを返す
        // Returns true after adding a point; false when pressing the first point closed and committed the path
        public bool Press(Vector2 world, Vector2 local)
        {
            if (_points.Count >= 2 && Vector2.Distance(local, _canvas.ToLocal(_points[0].Anchor)) <= ClosePickRadius)
            {
                Commit(true);
                return false;
            }
            _points.Add(new PathPoint(world, world, world));
            if (_points.Count == 1) _canvas.BeginInteraction(new[] { _draft });
            Refresh();
            return true;
        }

        public void Drag(Vector2 world, Vector2 local, bool shift)
        {
            var anchor = _points[^1].Anchor;
            _points[^1] = new PathPoint(anchor, anchor * 2f - world, world);
            Refresh();
        }

        public void Release(bool dragged)
        {
        }

        public void Commit(bool closed)
        {
            // 曲線を折れ線にして包む矩形を図形の大きさとし、点を単位正方形の座標へ直して確定する（取っ手は枠の外に出てよい）
            // The box around the flattened curve becomes the shape size; points go to unit coordinates (handles may exceed it)
            _canvas.Overlay.SetPenDraft(null);
            if (_points.Count < 2)
            {
                _canvas.EndInteraction();
                return;
            }
            var identity = new ShapeTransform(Vector2.zero, Vector2.one, 0f);
            var outline = ShapeHitTester.Flatten(new VectorShape(0, ShapeKind.Path, identity, Color.clear, Color.clear, 0f, _points, closed));
            var min = outline[0];
            var max = min;
            foreach (var v in outline)
            {
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
            var center = (min + max) * 0.5f;
            var size = Vector2.Max(max - min, Vector2.one);
            var unitPoints = new List<PathPoint>();
            foreach (var p in _points) unitPoints.Add(new PathPoint(ToUnit(p.Anchor), ToUnit(p.InHandle), ToUnit(p.OutHandle)));
            var fill = closed ? VectorPalette.NextFill() : Color.clear;
            var shape = new VectorShape(_canvas.Document.AllocateId(), ShapeKind.Path, new ShapeTransform(center, size, 0f), fill, VectorPalette.Stroke, 2f, unitPoints, closed);
            _canvas.Document.Shapes.Add(shape);
            _canvas.History.Record(new AddShapesCommand(_canvas.Document, new List<VectorShape> { shape }));
            _canvas.EndInteraction();
            _canvas.SetSelection(new[] { shape });

            Vector2 ToUnit(Vector2 world) => new((world.x - center.x) / size.x, (world.y - center.y) / size.y);
        }

        private void Refresh()
        {
            _canvas.Overlay.SetPenDraft(_points);
            _canvas.RefreshActive();
        }
    }
}
