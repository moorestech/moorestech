using System.Collections.Generic;
using UnityEngine;

namespace Client.UitkPoc.VectorEditor.Model
{
    public enum ShapeKind
    {
        Rectangle,
        Ellipse,
        Path,
    }

    // パスの1点。座標はすべて単位正方形（-0.5〜0.5）内の絶対位置で持ち、大きさ変更は変換側で効かせる
    // One path point; all coordinates are absolute in the unit square (-0.5..0.5) so resizing lives in the transform
    public readonly struct PathPoint
    {
        public readonly Vector2 Anchor;
        public readonly Vector2 InHandle;
        public readonly Vector2 OutHandle;

        public PathPoint(Vector2 anchor, Vector2 inHandle, Vector2 outHandle)
        {
            Anchor = anchor;
            InHandle = inHandle;
            OutHandle = outHandle;
        }
    }

    // 図形の置き方（中心・大きさ・回転）。元に戻すときの前後の写しにも使う
    // Placement of a shape (center, size, rotation); also the before/after snapshot for undo
    public readonly struct ShapeTransform
    {
        public readonly Vector2 Center;
        public readonly Vector2 Size;
        public readonly float RotationDegrees;

        public ShapeTransform(Vector2 center, Vector2 size, float rotationDegrees)
        {
            Center = center;
            Size = size;
            RotationDegrees = rotationDegrees;
        }

        // 単位正方形内の点をワールド座標へ写す（拡大→回転→平行移動）
        // Maps a unit-square point to world space (scale, rotate, translate)
        public Vector2 ToWorld(Vector2 unit)
        {
            return Center + Rotate(Vector2.Scale(unit, Size), RotationDegrees);
        }

        public Vector2 ToUnit(Vector2 world)
        {
            var local = Rotate(world - Center, -RotationDegrees);
            return new Vector2(local.x / Mathf.Max(Size.x, 0.0001f), local.y / Mathf.Max(Size.y, 0.0001f));
        }

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
    }

    public class VectorShape
    {
        public readonly int Id;
        public readonly ShapeKind Kind;
        public readonly List<PathPoint> PathPoints;
        public readonly bool ClosedPath;
        public readonly Color Fill;
        public readonly Color Stroke;
        public readonly float StrokeWidth;
        public ShapeTransform Transform { get; private set; }

        public VectorShape(int id, ShapeKind kind, ShapeTransform transform, Color fill, Color stroke, float strokeWidth, List<PathPoint> pathPoints, bool closedPath)
        {
            Id = id;
            Kind = kind;
            Transform = transform;
            Fill = fill;
            Stroke = stroke;
            StrokeWidth = strokeWidth;
            PathPoints = pathPoints;
            ClosedPath = closedPath;
        }

        public void SetTransform(ShapeTransform transform)
        {
            Transform = transform;
        }

        // 回転込みの4隅を包む軸平行の矩形（スマートガイドと表示範囲の判定に使う）
        // Axis-aligned box around the rotated corners (for smart guides and viewport culling)
        public Rect WorldBounds()
        {
            var a = Transform.ToWorld(new Vector2(-0.5f, -0.5f));
            var b = Transform.ToWorld(new Vector2(0.5f, -0.5f));
            var c = Transform.ToWorld(new Vector2(0.5f, 0.5f));
            var d = Transform.ToWorld(new Vector2(-0.5f, 0.5f));
            return Rect.MinMaxRect(Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x)), Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y)),
                Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x)), Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y)));
        }
    }
}
