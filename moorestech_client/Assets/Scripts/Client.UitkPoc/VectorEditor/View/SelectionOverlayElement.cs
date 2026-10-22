using System.Collections.Generic;
using System.Linq;
using Client.UitkPoc.VectorEditor.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.VectorEditor.View
{
    // 選択枠・取っ手・スマートガイド・範囲選択・描きかけのペンを、ビュー座標で毎回描く最前面の層
    // Topmost layer drawing selection boxes, handles, smart guides, marquee and the pen draft in viewport space
    public class SelectionOverlayElement : VisualElement
    {
        public const int RotateHandle = 8;
        private const float HandleHalf = 4.5f;
        private const float HandlePickRadius = 8f;
        private const float RotateOffset = 26f;
        private const int MaxOutlines = 300;
        private static readonly Color Accent = new(0.25f, 0.6f, 1f);
        private static readonly Color GuideColor = new(1f, 0.25f, 0.65f);
        private static readonly Vector2[] HandleUnits =
        {
            new(-0.5f, -0.5f), new(0f, -0.5f), new(0.5f, -0.5f), new(0.5f, 0f),
            new(0.5f, 0.5f), new(0f, 0.5f), new(-0.5f, 0.5f), new(-0.5f, 0f),
        };

        private readonly VectorCanvas _canvas;
        private float? _guideX;
        private float? _guideY;
        private bool _marqueeVisible;
        private Rect _marquee;
        private List<PathPoint> _penDraft;

        public SelectionOverlayElement(VectorCanvas canvas)
        {
            _canvas = canvas;
            pickingMode = PickingMode.Ignore;
            AddToClassList("ve-overlay");
            generateVisualContent += Draw;
        }

        public VectorShape SingleSelection => _canvas.Selected.Count == 1 ? _canvas.Selected.First() : null;

        public static Vector2 HandleUnit(int index) => HandleUnits[index];

        public void SetGuides(float? worldX, float? worldY)
        {
            _guideX = worldX;
            _guideY = worldY;
            MarkDirtyRepaint();
        }

        public void SetMarquee(bool visible, Rect localRect)
        {
            _marqueeVisible = visible;
            _marquee = localRect;
            MarkDirtyRepaint();
        }

        public void SetPenDraft(List<PathPoint> worldPoints)
        {
            _penDraft = worldPoints;
            MarkDirtyRepaint();
        }

        public int HandleAt(Vector2 local)
        {
            var shape = SingleSelection;
            if (shape == null || _canvas.Tool != VectorTool.Select) return -1;
            if (Vector2.Distance(local, RotateHandlePosition(shape)) <= HandlePickRadius) return RotateHandle;
            for (var i = 0; i < HandleUnits.Length; i++) if (Vector2.Distance(local, Local(shape, HandleUnits[i])) <= HandlePickRadius) return i;
            return -1;
        }

        private void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            painter.lineWidth = 1f;
            painter.strokeColor = Accent;
            foreach (var shape in _canvas.Selected.Take(MaxOutlines)) StrokeQuad(painter, shape);
            var single = SingleSelection;
            if (single != null && _canvas.Tool == VectorTool.Select) DrawHandles(painter, single);
            DrawGuides(painter);
            if (_marqueeVisible)
            {
                painter.fillColor = new Color(Accent.r, Accent.g, Accent.b, 0.12f);
                RectPath(painter, _marquee.center, _marquee.size * 0.5f);
                painter.Fill();
                painter.Stroke();
            }
            if (_penDraft != null) DrawPenDraft(painter);
        }

        private void DrawHandles(Painter2D painter, VectorShape shape)
        {
            // 回転の取っ手は上辺の中央から図形の「上」方向へ伸ばす
            // The rotate handle extends from the top-center toward the shape's local "up"
            var topCenter = Local(shape, HandleUnits[1]);
            var rotate = RotateHandlePosition(shape);
            painter.BeginPath();
            painter.MoveTo(topCenter);
            painter.LineTo(rotate);
            painter.Stroke();
            painter.fillColor = Color.white;
            painter.BeginPath();
            painter.Arc(rotate, HandleHalf + 1f, 0f, 360f);
            painter.Fill();
            painter.Stroke();
            foreach (var unit in HandleUnits)
            {
                RectPath(painter, Local(shape, unit), Vector2.one * HandleHalf);
                painter.Fill();
                painter.Stroke();
            }
        }

        private void DrawGuides(Painter2D painter)
        {
            painter.strokeColor = GuideColor;
            var size = contentRect.size;
            if (_guideX.HasValue) Line(painter, new Vector2(_canvas.ToLocal(new Vector2(_guideX.Value, 0)).x, 0), new Vector2(_canvas.ToLocal(new Vector2(_guideX.Value, 0)).x, size.y));
            if (_guideY.HasValue) Line(painter, new Vector2(0, _canvas.ToLocal(new Vector2(0, _guideY.Value)).y), new Vector2(size.x, _canvas.ToLocal(new Vector2(0, _guideY.Value)).y));
            painter.strokeColor = Accent;
        }

        private void DrawPenDraft(Painter2D painter)
        {
            // 取っ手の線と丸、アンカーの四角。最初の点は閉じる目印として大きめに描く
            // Handle lines and dots plus anchor squares; the first anchor is larger as the close target
            painter.fillColor = Color.white;
            for (var i = 0; i < _penDraft.Count; i++)
            {
                var point = _penDraft[i];
                var anchor = _canvas.ToLocal(point.Anchor);
                foreach (var handle in new[] { point.InHandle, point.OutHandle })
                {
                    if ((handle - point.Anchor).sqrMagnitude < 0.0001f) continue;
                    Line(painter, anchor, _canvas.ToLocal(handle));
                    painter.BeginPath();
                    painter.Arc(_canvas.ToLocal(handle), 3f, 0f, 360f);
                    painter.Fill();
                }
                RectPath(painter, anchor, Vector2.one * (i == 0 ? HandleHalf + 1.5f : HandleHalf));
                painter.Fill();
                painter.Stroke();
            }
        }

        private void StrokeQuad(Painter2D painter, VectorShape shape)
        {
            painter.BeginPath();
            painter.MoveTo(Local(shape, HandleUnits[0]));
            painter.LineTo(Local(shape, HandleUnits[2]));
            painter.LineTo(Local(shape, HandleUnits[4]));
            painter.LineTo(Local(shape, HandleUnits[6]));
            painter.ClosePath();
            painter.Stroke();
        }

        private Vector2 RotateHandlePosition(VectorShape shape)
        {
            return Local(shape, HandleUnits[1]) + ShapeTransform.Rotate(Vector2.down, shape.Transform.RotationDegrees) * RotateOffset;
        }

        private Vector2 Local(VectorShape shape, Vector2 unit) => _canvas.ToLocal(shape.Transform.ToWorld(unit));

        private static void Line(Painter2D painter, Vector2 from, Vector2 to)
        {
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();
        }

        private static void RectPath(Painter2D painter, Vector2 center, Vector2 half)
        {
            painter.BeginPath();
            painter.MoveTo(center - half);
            painter.LineTo(center + new Vector2(half.x, -half.y));
            painter.LineTo(center + half);
            painter.LineTo(center + new Vector2(-half.x, half.y));
            painter.ClosePath();
        }
    }
}
