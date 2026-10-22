using Client.UitkPoc.VectorEditor.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.VectorEditor.Geometry
{
    // 図形をPainter2Dのパスにする。単位正方形の制御点をワールドへ写してから描画縮尺を掛ける（アフィン変換なのでベジェは厳密）
    // Turns a shape into a Painter2D path; unit control points go to world then draw scale (affine, so beziers stay exact)
    public static class ShapePathBuilder
    {
        private const float Kappa = 0.5522848f * 0.5f;

        public static void Draw(Painter2D painter, VectorShape shape, float drawScale)
        {
            Build(painter, shape, drawScale);
            if (shape.Kind != ShapeKind.Path || shape.ClosedPath)
            {
                painter.fillColor = shape.Fill;
                painter.Fill();
            }
            painter.strokeColor = shape.Stroke;
            painter.lineWidth = shape.StrokeWidth * drawScale;
            painter.Stroke();
        }

        public static void Build(Painter2D painter, VectorShape shape, float drawScale)
        {
            var t = shape.Transform;
            painter.BeginPath();
            if (shape.Kind == ShapeKind.Rectangle)
            {
                painter.MoveTo(P(t, -0.5f, -0.5f, drawScale));
                painter.LineTo(P(t, 0.5f, -0.5f, drawScale));
                painter.LineTo(P(t, 0.5f, 0.5f, drawScale));
                painter.LineTo(P(t, -0.5f, 0.5f, drawScale));
                painter.ClosePath();
                return;
            }
            if (shape.Kind == ShapeKind.Ellipse)
            {
                // 楕円は4本の3次ベジェで近似する
                // An ellipse is approximated by four cubic beziers
                painter.MoveTo(P(t, 0.5f, 0f, drawScale));
                painter.BezierCurveTo(P(t, 0.5f, Kappa, drawScale), P(t, Kappa, 0.5f, drawScale), P(t, 0f, 0.5f, drawScale));
                painter.BezierCurveTo(P(t, -Kappa, 0.5f, drawScale), P(t, -0.5f, Kappa, drawScale), P(t, -0.5f, 0f, drawScale));
                painter.BezierCurveTo(P(t, -0.5f, -Kappa, drawScale), P(t, -Kappa, -0.5f, drawScale), P(t, 0f, -0.5f, drawScale));
                painter.BezierCurveTo(P(t, Kappa, -0.5f, drawScale), P(t, 0.5f, -Kappa, drawScale), P(t, 0.5f, 0f, drawScale));
                painter.ClosePath();
                return;
            }
            var points = shape.PathPoints;
            painter.MoveTo(W(t, points[0].Anchor, drawScale));
            for (var i = 1; i < points.Count; i++) Segment(painter, t, points[i - 1], points[i], drawScale);
            if (!shape.ClosedPath) return;
            Segment(painter, t, points[^1], points[0], drawScale);
            painter.ClosePath();
        }

        private static void Segment(Painter2D painter, ShapeTransform t, PathPoint from, PathPoint to, float drawScale)
        {
            painter.BezierCurveTo(W(t, from.OutHandle, drawScale), W(t, to.InHandle, drawScale), W(t, to.Anchor, drawScale));
        }

        private static Vector2 P(ShapeTransform t, float x, float y, float drawScale) => t.ToWorld(new Vector2(x, y)) * drawScale;
        private static Vector2 W(ShapeTransform t, Vector2 unit, float drawScale) => t.ToWorld(unit) * drawScale;
    }
}
