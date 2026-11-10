using Client.Ui.Foundation.Theme;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Theme
{
    /// <summary>
    /// 影の輪の共有 writer が帯の結び順と時計回りの向きを固定の並びで書くことを確かめる（L0）
    /// Verifies the shared shadow-ring writer emits bands in a fixed order with clockwise winding (L0)
    /// </summary>
    public class ShadowRingMeshWriterTest
    {
        [Test]
        public void 正方形の輪2本の帯は辺ごとに時計回りの三角2枚で結ぶ()
        {
            // 内の輪 0..3 と外の輪 4..7（左上・右上・右下・左下、y 下向き）
            // Inner ring 0..3 and outer ring 4..7 (top-left, top-right, bottom-right, bottom-left, y down)
            var vertices = new[]
            {
                At(1, 1), At(2, 1), At(2, 2), At(1, 2),
                At(0, 0), At(3, 0), At(3, 3), At(0, 3),
            };
            var indices = new ushort[4 * 6];
            var cursor = 0;
            ShadowRingMeshWriter.WriteBands(vertices, indices, ref cursor, 1, 4);

            Assert.AreEqual(24, cursor);
            CollectionAssert.AreEqual(new ushort[]
            {
                0, 5, 1, 0, 4, 5,
                1, 6, 2, 1, 5, 6,
                2, 7, 3, 2, 6, 7,
                3, 4, 0, 3, 7, 4,
            }, indices);
            for (var i = 0; i < indices.Length; i += 3) Assert.IsTrue(IsClockwise(vertices, indices, i), $"triangle {i / 3}");
        }

        [Test]
        public void 反時計回りに渡した三角は時計回りへ入れ替える()
        {
            var vertices = new[] { At(0, 0), At(0, 1), At(1, 0) };
            var indices = new ushort[3];
            var cursor = 0;
            ShadowRingMeshWriter.WriteClockwise(vertices, indices, ref cursor, 0, 1, 2);

            CollectionAssert.AreEqual(new ushort[] { 0, 2, 1 }, indices);
            Assert.IsTrue(IsClockwise(vertices, indices, 0));
        }

        private static Vertex At(float x, float y)
        {
            return new Vertex { position = new Vector3(x, y, Vertex.nearZ) };
        }

        private static bool IsClockwise(Vertex[] vertices, ushort[] indices, int first)
        {
            var pa = vertices[indices[first]].position;
            var pb = vertices[indices[first + 1]].position;
            var pc = vertices[indices[first + 2]].position;
            return 0f < (pb.x - pa.x) * (pc.y - pa.y) - (pb.y - pa.y) * (pc.x - pa.x);
        }
    }
}
