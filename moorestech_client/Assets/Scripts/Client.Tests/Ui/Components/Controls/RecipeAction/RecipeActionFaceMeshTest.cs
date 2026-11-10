using Client.Ui.Components.Controls;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Controls.RecipeAction
{
    /// <summary>
    /// 主要アクションの面の帯（頂点の位置・色・向き）
    /// Primary action face strip (vertex positions, colors, winding)
    /// </summary>
    public class RecipeActionFaceMeshTest
    {
        // クラフトボタンの寸法と Web の --recipe-action-background の 3 色
        // Craft button size and the three Web --recipe-action-background stops
        private const float Width = 80.433f;
        private const float Height = 19f;
        private static readonly Color Start = new(0f, 114f / 255f, 216f / 255f, 1f);
        private static readonly Color Middle = new(0f, 143f / 255f, 237f / 255f, 1f);
        private static readonly Color End = new(4f / 255f, 216f / 255f, 236f / 255f, 1f);

        // Vertex.tint は Color32 なので 8 bit の量子化 1/255 と丸めを許す
        // Vertex.tint is Color32, so allow the 1/255 8-bit quantum plus rounding
        private const float ColorTolerance = 1f / 255f + 0.001f;

        private readonly Vertex[] _vertices = new Vertex[RecipeActionFaceMesh.VertexCount];
        private readonly ushort[] _indices = new ushort[RecipeActionFaceMesh.IndexCount];

        // Mantine Button の透明な 1px の枠。グラデは内側 78.433px に敷く
        // Mantine Button's transparent 1px border; the gradient spans the inner 78.433px
        private const float Border = 1f;
        private const float InnerWidth = Width - Border * 2f;

        [SetUp]
        public void BuildCraftSize()
        {
            RecipeActionFaceMesh.Build(Width, Height, Border, Start, Middle, End, _vertices, _indices);
        }

        [Test]
        public void 帯の頂点は枠の内側の17列の上下2行で両端と中央が停止の位置と色になる()
        {
            Assert.AreEqual(42, RecipeActionFaceMesh.VertexCount);
            AssertVertex(0, Border, 0f, Start);
            AssertVertex(8, Width / 2f, 0f, Middle);
            AssertVertex(16, Width - Border, 0f, End);
            AssertVertex(17, Border, Height, Start);
            AssertVertex(25, Width / 2f, Height, Middle);
            AssertVertex(33, Width - Border, Height, End);
        }

        [Test]
        public void 左の枠は終わりの色で右の枠は始めの色が回り込み枠0では帯が箱いっぱい()
        {
            // Web の padding-box 起点の repeat: 左の枠は左隣の 1 枚の右端、右の枠は右隣の 1 枚の左端
            // Web padding-box origin with repeat: the left border shows the previous tile's right end, the right border the next tile's left end
            var left = RecipeActionFaceMesh.LeftBorderFirstVertex;
            AssertVertex(left, 0f, 0f, Color.Lerp(Middle, End, (1f - Border / InnerWidth - 0.5f) * 2f));
            AssertVertex(left + 1, Border, 0f, End);
            AssertVertex(left + 3, Border, Height, End);
            var right = RecipeActionFaceMesh.RightBorderFirstVertex;
            AssertVertex(right, Width - Border, 0f, Start);
            AssertVertex(right + 1, Width, 0f, Color.Lerp(Start, Middle, Border / InnerWidth * 2f));
            AssertVertex(right + 2, Width - Border, Height, Start);

            // 研究（border: 0）は帯が箱の端から端まで
            // Research (border: 0) spans the strip edge to edge
            RecipeActionFaceMesh.Build(300f, 40f, 0f, Start, Middle, End, _vertices, _indices);
            Assert.AreEqual(0f, _vertices[0].position.x, 0.001f);
            Assert.AreEqual(300f, _vertices[16].position.x, 0.001f);
        }

        [Test]
        public void 中間の列はsRGBの成分で線形に補間した色で上下の行が同じ色()
        {
            // 4 列目は start→middle の半分、12 列目は middle→end の半分
            // Column 4 is halfway from start to middle, column 12 halfway from middle to end
            AssertTint(Color.Lerp(Start, Middle, 0.5f), _vertices[4].tint, "column 4");
            AssertTint(Color.Lerp(Middle, End, 0.5f), _vertices[12].tint, "column 12");
            for (var column = 0; column < RecipeActionFaceMesh.ColumnCount; column++)
                AssertTint(_vertices[column].tint, _vertices[RecipeActionFaceMesh.ColumnCount + column].tint, $"column {column} bottom");
        }

        [Test]
        public void 添字は帯の全列と左右の枠を画面座標で時計回りの2三角ずつで覆う()
        {
            Assert.AreEqual(108, RecipeActionFaceMesh.IndexCount);
            foreach (var index in _indices) Assert.Less(index, RecipeActionFaceMesh.VertexCount);
            for (var i = 0; i < _indices.Length; i += 3)
            {
                var a = _vertices[_indices[i]].position;
                var b = _vertices[_indices[i + 1]].position;
                var c = _vertices[_indices[i + 2]].position;
                var cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                Assert.Greater(cross, 0f, $"triangle {i / 3}");
            }
        }

        private void AssertVertex(int index, float x, float y, Color color)
        {
            Assert.AreEqual(x, _vertices[index].position.x, 0.001f, $"vertex {index} x");
            Assert.AreEqual(y, _vertices[index].position.y, 0.001f, $"vertex {index} y");
            AssertTint(color, _vertices[index].tint, $"vertex {index}");
        }

        private static void AssertTint(Color expected, Color actual, string what)
        {
            Assert.AreEqual(expected.r, actual.r, ColorTolerance, $"{what} r");
            Assert.AreEqual(expected.g, actual.g, ColorTolerance, $"{what} g");
            Assert.AreEqual(expected.b, actual.b, ColorTolerance, $"{what} b");
            Assert.AreEqual(expected.a, actual.a, ColorTolerance, $"{what} a");
        }
    }
}
