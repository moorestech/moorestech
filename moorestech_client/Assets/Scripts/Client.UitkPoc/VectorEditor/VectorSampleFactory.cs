using System.Collections.Generic;
using Client.UitkPoc.VectorEditor.Model;
using UnityEngine;

namespace Client.UitkPoc.VectorEditor
{
    // 試験用の図形を並べる。矩形・楕円・星形パスを順に置き、回転と大きさを少しずつ変える
    // Lays out test shapes: rectangles, ellipses and star paths in turn, with varied rotation and size
    public static class VectorSampleFactory
    {
        public static VectorDocument Create(int shapeCount)
        {
            var document = new VectorDocument();
            var random = new System.Random(7);
            var columns = Mathf.CeilToInt(Mathf.Sqrt(shapeCount * 1.6f));
            for (var i = 0; i < shapeCount; i++)
            {
                var cell = new Vector2(i % columns, i / columns) * 130f;
                var size = new Vector2(random.Next(50, 110), random.Next(50, 110));
                var rotation = random.Next(0, 4) == 0 ? random.Next(-30, 30) : 0f;
                var transform = new ShapeTransform(cell + Vector2.one * 65f, size, rotation);
                var kind = (ShapeKind)(i % 3);
                var points = kind == ShapeKind.Path ? Star(5, 0.38f) : null;
                document.Shapes.Add(new VectorShape(document.AllocateId(), kind, transform, VectorPalette.FillAt(i), VectorPalette.Stroke, 2f, points, kind == ShapeKind.Path));
            }
            return document;
        }

        // 星形を単位正方形内の折れ線（取っ手なし）で作る
        // Builds a star as a polyline (no handles) inside the unit square
        public static List<PathPoint> Star(int spikes, float innerRatio)
        {
            var points = new List<PathPoint>();
            for (var i = 0; i < spikes * 2; i++)
            {
                var radius = i % 2 == 0 ? 0.5f : 0.5f * innerRatio;
                var angle = Mathf.PI * i / spikes - Mathf.PI * 0.5f;
                var p = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                points.Add(new PathPoint(p, p, p));
            }
            return points;
        }
    }
}
