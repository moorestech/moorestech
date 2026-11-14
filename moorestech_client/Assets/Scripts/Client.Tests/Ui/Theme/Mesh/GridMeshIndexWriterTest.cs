using Client.Ui.Foundation.Theme.Mesh;
using NUnit.Framework;

namespace Client.Tests.Ui.Theme.Mesh
{
    public class GridMeshIndexWriterTest
    {
        // 2×3 の格子（stride 4）: 頂点は 0..3 / 4..7 / 8..11
        // A 2x3 grid (stride 4): vertices 0..3 / 4..7 / 8..11
        private const int Stride = 4;

        [Test]
        public void FirstQuadWindsTopLeftTopRightBottomRightThenTopLeftBottomRightBottomLeft()
        {
            var quad = GridMeshIndexWriter.QuadIndices(0, 0, Stride);
            AssertCorners(quad, 0, 1, 5, 4);
            CollectionAssert.AreEqual(new ushort[] { 0, 1, 5, 0, 5, 4 }, Written(quad));
        }

        [Test]
        public void LastQuadOfTwoByThreeGridUsesStrideForTheNextRow()
        {
            var quad = GridMeshIndexWriter.QuadIndices(1, 2, Stride);
            AssertCorners(quad, 6, 7, 11, 10);
            CollectionAssert.AreEqual(new ushort[] { 6, 7, 11, 6, 11, 10 }, Written(quad));
        }

        private static void AssertCorners(GridQuadIndices quad, int topLeft, int topRight, int bottomRight, int bottomLeft)
        {
            Assert.AreEqual(topLeft, quad.TopLeft);
            Assert.AreEqual(topRight, quad.TopRight);
            Assert.AreEqual(bottomRight, quad.BottomRight);
            Assert.AreEqual(bottomLeft, quad.BottomLeft);
        }

        private static ushort[] Written(GridQuadIndices quad)
        {
            var indices = new ushort[GridQuadIndices.IndexCount];
            quad.WriteTo(indices, 0);
            return indices;
        }
    }
}
