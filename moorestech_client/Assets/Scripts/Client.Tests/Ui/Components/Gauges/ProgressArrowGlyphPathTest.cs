using Client.Ui.Components.Gauges;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Components.Gauges
{
    /// <summary>
    /// 矢印 path の当てはめ・ずらし・三角
    /// Arrow path fitting, offsetting, triangles
    /// </summary>
    public class ProgressArrowGlyphPathTest
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void 既定寸法ではviewBoxを高さに合わせ横の余白を左右に等分する()
        {
            var box = ProgressArrowGlyphPath.FitViewBox(new Vector2(53.622f, 30.53f));

            var scale = 30.53f / 78f;
            Assert.AreEqual(scale, ProgressArrowGlyphPath.ScaleOf(new Vector2(53.622f, 30.53f)), 1e-6f);
            Assert.AreEqual(121f * scale, box.width, Tolerance);
            Assert.AreEqual(30.53f, box.height, Tolerance);
            Assert.AreEqual((53.622f - 121f * scale) / 2f, box.x, Tolerance);
            Assert.AreEqual(0f, box.y, Tolerance);
        }

        [Test]
        public void 縦長の箱では幅に合わせ上下の余白を等分する()
        {
            var box = ProgressArrowGlyphPath.FitViewBox(new Vector2(121f, 200f));

            Assert.AreEqual(new Rect(0f, 61f, 121f, 78f), box);
        }

        [Test]
        public void 距離0のずらしは頂点を倍率で拡げて平行に動かすだけ()
        {
            var points = new Vector2[ProgressArrowGlyphPath.VertexCount];
            ProgressArrowGlyphPath.WriteOffset(0.5f, 0f, new Vector2(0f, 1f), points);

            var expected = new[] { new Vector2(2, 27), new Vector2(69, 27), new Vector2(69, 2), new Vector2(119, 39), new Vector2(69, 76), new Vector2(69, 51), new Vector2(2, 51) };
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].x * 0.5f, points[i].x, Tolerance, $"x{i}");
                Assert.AreEqual(expected[i].y * 0.5f + 1f, points[i].y, Tolerance, $"y{i}");
            }
        }

        [Test]
        public void 外へのずらしは辺を平行に1px動かした角のmiterの位置になる()
        {
            // 先端の角は 1/sin(36.5°)=1.681 倍、直角の角は対角へ √2 倍だけ動く
            // The tip corner moves 1/sin(36.5°)=1.681 times and right-angle corners move √2 along the diagonal
            var points = new Vector2[ProgressArrowGlyphPath.VertexCount];
            ProgressArrowGlyphPath.WriteOffset(1f, 1f, Vector2.zero, points);

            Assert.AreEqual(new Vector2(1f, 26f).x, points[0].x, Tolerance);
            Assert.AreEqual(26f, points[0].y, Tolerance);
            Assert.AreEqual(68f, points[1].x, Tolerance);
            Assert.AreEqual(26f, points[1].y, Tolerance);
            Assert.AreEqual(119f + 1.6810f, points[3].x, 0.001f);
            Assert.AreEqual(39f, points[3].y, Tolerance);
            Assert.AreEqual(1f, points[6].x, Tolerance);
            Assert.AreEqual(52f, points[6].y, Tolerance);
        }

        [TestCase(0f, 3458f)]
        [TestCase(1f, 3795.72f)]
        [TestCase(-0.6f, 3260.47f)]
        public void 塗りの三角はずらしても時計回りで面積の和が形の面積になる(float distance, float expectedArea)
        {
            // 三角の面積の和がずらした多角形の靴紐の面積と一致すれば、3 枚が形を過不足なく覆う
            // Triangle areas summing to the offset polygon's shoelace area means the three cover the shape exactly
            var points = new Vector2[ProgressArrowGlyphPath.VertexCount];
            ProgressArrowGlyphPath.WriteOffset(1f, distance, Vector2.zero, points);

            var area = 0f;
            for (var i = 0; i < ProgressArrowGlyphPath.FillTriangleIndexCount; i += 3)
            {
                var a = points[ProgressArrowGlyphPath.FillTriangleIndexAt(i)];
                var b = points[ProgressArrowGlyphPath.FillTriangleIndexAt(i + 1)];
                var c = points[ProgressArrowGlyphPath.FillTriangleIndexAt(i + 2)];
                var cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                Assert.Greater(cross, 0f, $"triangle {i / 3} must wind clockwise in y-down space");
                area += cross / 2f;
            }
            Assert.AreEqual(expectedArea, area, 0.05f);
            Assert.AreEqual(Shoelace(points), area, 0.01f);
        }

        private static float Shoelace(Vector2[] points)
        {
            var doubled = 0f;
            for (var i = 0; i < points.Length; i++)
            {
                var next = points[(i + 1) % points.Length];
                doubled += points[i].x * next.y - next.x * points[i].y;
            }
            return doubled / 2f;
        }
    }
}
