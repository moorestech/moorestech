using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Gauges
{
    /// <summary>
    /// 影メッシュの形・向き・濃さ
    /// Shadow mesh shape, winding, alpha
    /// </summary>
    public class ProgressArrowGlyphShadowMeshTest
    {
        // 既定寸法の倍率・線の外縁・影の色
        // Default-size scale, stroke outer edge and shadow color
        private const float Scale = 30.53f / 78f;

        // 輪ごとの頂点数（添字=輪×Corners＋角）
        // Vertices per ring (index = ring*Corners + corner)
        private const int Corners = ProgressArrowGlyphPath.VertexCount;
        private static readonly Color ShadowColor = new(0f, 0f, 0f, 0.45f);

        // Vertex.tint は Color32 なので α を 255 で割って比べ、計算の丸め 0.002 に 8 bit の量子化 1/255 を足す
        // Vertex.tint is Color32, so compare alpha divided by 255 with 0.002 rounding plus the 1/255 8-bit quantum
        private const float AlphaTolerance = 0.002f + 1f / 255f;

        private readonly Vertex[] _vertices = new Vertex[ProgressArrowGlyphShadowMesh.VertexCount];
        private readonly ushort[] _indices = new ushort[ProgressArrowGlyphShadowMesh.IndexCount];

        [SetUp]
        public void BuildDefault()
        {
            ProgressArrowGlyphShadowMesh.Build(Scale, Scale, 1f, 1f, ShadowColor, _vertices, _indices);
        }

        [Test]
        public void 頂点は9本の輪で添字は芯と8本の帯の三角を全部指す()
        {
            Assert.AreEqual(63, ProgressArrowGlyphShadowMesh.VertexCount);
            Assert.AreEqual(9 + 8 * 7 * 6, ProgressArrowGlyphShadowMesh.IndexCount);
            foreach (var index in _indices) Assert.Less(index, ProgressArrowGlyphShadowMesh.VertexCount);
        }

        [Test]
        public void 三角は全部画面座標で時計回り()
        {
            for (var i = 0; i < _indices.Length; i += 3)
            {
                var a = _vertices[_indices[i]].position;
                var b = _vertices[_indices[i + 1]].position;
                var c = _vertices[_indices[i + 2]].position;
                var cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                Assert.Greater(cross, 0f, $"triangle {i / 3}");
            }
        }

        [Test]
        public void 真ん中の輪は線の外縁を1px下げた位置で濃さはWebのαの半分()
        {
            // 輪 4 は縁そのもの（d=0）。被覆率 0.5 を Web の α 0.45 に掛けて 0.225
            // Ring 4 is the edge itself (d=0); coverage 0.5 times the Web alpha 0.45 is 0.225
            var tip = _vertices[4 * Corners + 3];
            Assert.AreEqual(119f * Scale + Scale * 1.6810f, tip.position.x, 0.001f);
            Assert.AreEqual(39f * Scale + 1f, tip.position.y, 0.001f);
            Assert.AreEqual(Vertex.nearZ, tip.position.z);
            Assert.AreEqual(0.225f, tip.tint.a / 255f, AlphaTolerance);
        }

        [Test]
        public void 輪は外へ行くほど薄くなり縁から1px外は標準偏差1pxの被覆率()
        {
            // ぼかし 1px は σ=1px。縁から 1px 外（輪 6）の被覆率は Φ(-1)=0.158655、外端（輪 8）は縁から 2px 外
            // A 1px blur is σ=1px; ring 6 (1px outside the edge) has coverage Φ(-1)=0.158655 and ring 8 sits 2px outside
            Assert.AreEqual(0.158655f, GaussianEdgeCoverage.At(1f, 1f), 1e-5f);
            Assert.AreEqual(0.45f * 0.97725f, _vertices[0].tint.a / 255f, AlphaTolerance);
            Assert.AreEqual(0.45f * 0.158655f, _vertices[6 * Corners].tint.a / 255f, AlphaTolerance);
            Assert.Less(_vertices[8 * Corners].tint.a / 255f, 0.019f + AlphaTolerance);
            Assert.AreEqual(119f * Scale + (Scale + 2f) * 1.6810f, _vertices[8 * Corners + 3].position.x, 0.001f);
            for (var ring = 1; ring <= ProgressArrowGlyphShadowMesh.RingCount; ring++)
                Assert.Less(_vertices[ring * Corners].tint.a, _vertices[(ring - 1) * Corners].tint.a, $"ring {ring}");
        }

        [Test]
        public void 影の色のトークンはWebのα045のまま()
        {
            // UI は Web と同じ sRGB 空間で合成するので、drop-shadow の色の α は Web の 0.45 そのもの（A-180）
            // The UI blends in sRGB space like the Web, so the drop-shadow colour alpha is the Web 0.45 itself (A-180)
            Assert.AreEqual(0.45f, ThemeTokenFile.ResolveColor("--progress-arrow-glyph-shadow").a, 1e-4f);
        }

        [Test]
        public void 高さ8pxでも内端の輪の軸は中心線を越えず裏返った三角も無い()
        {
            // 高さ 8px は軸の半高 12×倍率≈1.23px が内側のずらし 2σ=2px より薄く、そのままずらすと軸の輪が裏返る
            // At 8px tall the shaft half-height 12×scale≈1.23px is thinner than the 2σ=2px inward offset, so a raw offset flips the shaft
            const float smallScale = 8f / 78f;
            ProgressArrowGlyphShadowMesh.Build(smallScale, smallScale, 1f, 1f, ShadowColor, _vertices, _indices);
            Assert.Less(_vertices[0].position.y, _vertices[6].position.y, "shaft left end");
            Assert.Less(_vertices[1].position.y, _vertices[5].position.y, "shaft right end");

            // 中心線で止めた輪どうしの帯は面積 0 になるので、裏返り（負の面積）だけを拒む
            // Bands between rings stopped at the centre line have zero area, so only reject flips (negative area)
            for (var i = 0; i < _indices.Length; i += 3)
            {
                var a = _vertices[_indices[i]].position;
                var b = _vertices[_indices[i + 1]].position;
                var c = _vertices[_indices[i + 2]].position;
                var cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                Assert.GreaterOrEqual(cross, -1e-4f, $"triangle {i / 3}");
            }
        }

        [Test]
        public void 色は影の色のままで輪ごとに同じ濃さ()
        {
            for (var ring = 0; ring <= ProgressArrowGlyphShadowMesh.RingCount; ring++)
            for (var corner = 0; corner < Corners; corner++)
            {
                var tint = _vertices[ring * Corners + corner].tint;
                Assert.AreEqual(0, tint.r + tint.g + tint.b);
                Assert.AreEqual(_vertices[ring * Corners].tint.a, tint.a, $"ring {ring} corner {corner}");
            }
        }
    }
}
