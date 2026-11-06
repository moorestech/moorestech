using System.Collections.Generic;
using Client.Ui.Screens.ModeHud.Parts;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// 黄の帯の多角形が CSS repeating-linear-gradient の色の決まり方と同じ点を覆う（L1）
    /// The yellow stripe polygons cover the same points as the CSS repeating-linear-gradient colouring (L1)
    /// </summary>
    public class DeleteModeWarningStripeGeometryTest
    {
        private const float Angle = 117f;
        private const float Stripe = 32f;

        [TestCase(1280f, 20f)]
        [TestCase(1706.6666f, 20f)]
        public void 矩形内の各点は多角形の内外がCSSの色と一致する(float width, float height)
        {
            var size = new Vector2(width, height);
            var polygons = DeleteModeWarningStripeGeometry.FirstColorPolygons(size, Angle, Stripe);

            // 帯の境目から 0.5px 以上離れた点だけを見る（境目は描画の縁のにじみで決まらない）
            // Check only points at least 0.5px from a stripe boundary (the boundary itself is antialiased)
            for (var x = 0.5f; x < width; x += 3.7f)
            for (var y = 0.5f; y < height; y += 1.9f)
            {
                var point = new Vector2(x, y);
                var t = CssGradientPosition(point, size, Angle) % (Stripe * 2f);
                if (Mathf.Abs(t - Stripe) < 0.5f || t < 0.5f || Stripe * 2f - t < 0.5f) continue;
                Assert.AreEqual(t < Stripe, IsInsideAny(polygons, point), $"point {point} t={t}");
            }
        }

        [Test]
        public void 多角形はどれも矩形の外へ出ない()
        {
            var size = new Vector2(1280f, 20f);
            foreach (var polygon in DeleteModeWarningStripeGeometry.FirstColorPolygons(size, Angle, Stripe))
            foreach (var vertex in polygon)
            {
                Assert.That(vertex.x, Is.InRange(-0.001f, size.x + 0.001f));
                Assert.That(vertex.y, Is.InRange(-0.001f, size.y + 0.001f));
            }
        }

        // CSS の linear-gradient の位置: 方向 (sin a, -cos a)、線の長さ |w sin a| + |h cos a|、中心を通る
        // CSS linear-gradient position: direction (sin a, -cos a), line length |w sin a| + |h cos a|, through the centre
        private static float CssGradientPosition(Vector2 point, Vector2 size, float angleDegrees)
        {
            var radians = angleDegrees * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
            var length = Mathf.Abs(size.x * Mathf.Sin(radians)) + Mathf.Abs(size.y * Mathf.Cos(radians));
            var start = size / 2f - direction * (length / 2f);
            return Vector2.Dot(point - start, direction);
        }

        private static bool IsInsideAny(IReadOnlyList<IReadOnlyList<Vector2>> polygons, Vector2 point)
        {
            foreach (var polygon in polygons)
            {
                var inside = true;
                for (var i = 0; i < polygon.Count && inside; i++)
                {
                    var a = polygon[i];
                    var b = polygon[(i + 1) % polygon.Count];
                    var cross = (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
                    inside = cross >= 0f;
                }
                if (inside) return true;
            }
            return false;
        }
    }
}
