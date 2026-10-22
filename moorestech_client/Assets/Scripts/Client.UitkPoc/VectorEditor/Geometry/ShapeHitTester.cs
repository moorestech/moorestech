using System.Collections.Generic;
using Client.UitkPoc.VectorEditor.Model;
using UnityEngine;

namespace Client.UitkPoc.VectorEditor.Geometry
{
    // Painter2Dには当たり判定が無いので、図形ごとの内外判定を自前で持つ
    // Painter2D has no hit testing, so inside/outside tests per shape kind are hand-written
    public static class ShapeHitTester
    {
        private const int SamplesPerSegment = 16;

        public static VectorShape TopmostAt(List<VectorShape> shapes, Vector2 world, float tolerance)
        {
            for (var i = shapes.Count - 1; i >= 0; i--) if (Contains(shapes[i], world, tolerance)) return shapes[i];
            return null;
        }

        public static bool Contains(VectorShape shape, Vector2 world, float tolerance)
        {
            var unit = shape.Transform.ToUnit(world);
            if (shape.Kind == ShapeKind.Rectangle) return Mathf.Abs(unit.x) <= 0.5f && Mathf.Abs(unit.y) <= 0.5f;
            if (shape.Kind == ShapeKind.Ellipse) return unit.sqrMagnitude <= 0.25f;

            // パスは折れ線に分け、閉じていれば偶奇則の内外、開いていれば線までの距離で判定する
            // Flatten the path; closed paths use the even-odd rule, open ones use distance to the stroke
            var polyline = Flatten(shape);
            var reach = shape.StrokeWidth * 0.5f + tolerance;
            for (var i = 1; i < polyline.Count; i++) if (DistanceToSegment(world, polyline[i - 1], polyline[i]) <= reach) return true;
            return shape.ClosedPath && InsidePolygon(polyline, world);
        }

        public static List<Vector2> Flatten(VectorShape shape)
        {
            var t = shape.Transform;
            var points = shape.PathPoints;
            var result = new List<Vector2> { t.ToWorld(points[0].Anchor) };
            var segmentCount = shape.ClosedPath ? points.Count : points.Count - 1;
            for (var i = 0; i < segmentCount; i++)
            {
                var from = points[i];
                var to = points[(i + 1) % points.Count];
                for (var s = 1; s <= SamplesPerSegment; s++)
                {
                    var unit = Bezier(from.Anchor, from.OutHandle, to.InHandle, to.Anchor, s / (float)SamplesPerSegment);
                    result.Add(t.ToWorld(unit));
                }
            }
            return result;
        }

        private static Vector2 Bezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }

        private static bool InsidePolygon(List<Vector2> polygon, Vector2 point)
        {
            var inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            return Vector2.Distance(point, a + ab * t);
        }
    }
}
