using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Theme
{
    /// <summary>
    /// 影の輪メッシュのインデックスを書く共有の口（隣り合う輪の帯と時計回りの三角）
    /// Shared index writer for shadow ring meshes (bands between neighbouring rings and clockwise triangles)
    /// </summary>
    /// <remarks>
    /// 輪 k の角 c は頂点 k·角数+c に並ぶ前提。UITK は裏向きの三角を描かないので、三角は頂点の位置から必ず時計回り（y 下向き）で書く。
    /// Corner c of ring k sits at vertex k·corners+c. UITK culls back-facing triangles, so each one is written clockwise (y down) from the vertex positions.
    /// </remarks>
    public static class ShadowRingMeshWriter
    {
        public static void WriteBands(Vertex[] vertices, ushort[] indices, ref int cursor, int ringCount, int cornersPerRing)
        {
            // 隣り合う輪の同じ辺を四角（三角 2 枚）で結ぶ
            // Join the same side of neighbouring rings with a quad (two triangles)
            for (var ring = 0; ring < ringCount; ring++)
            for (var corner = 0; corner < cornersPerRing; corner++)
            {
                var innerFrom = (ushort)(ring * cornersPerRing + corner);
                var innerTo = (ushort)(ring * cornersPerRing + (corner + 1) % cornersPerRing);
                var outerFrom = (ushort)(innerFrom + cornersPerRing);
                var outerTo = (ushort)(innerTo + cornersPerRing);
                WriteClockwise(vertices, indices, ref cursor, innerFrom, outerTo, innerTo);
                WriteClockwise(vertices, indices, ref cursor, innerFrom, outerFrom, outerTo);
            }
        }

        public static void WriteClockwise(Vertex[] vertices, ushort[] indices, ref int cursor, ushort a, ushort b, ushort c)
        {
            // 外積の符号で向きを見て、反時計回りなら b と c を入れ替える
            // Check the winding by the cross product sign and swap b and c when counter-clockwise
            var pa = vertices[a].position;
            var pb = vertices[b].position;
            var pc = vertices[c].position;
            var clockwise = 0f < (pb.x - pa.x) * (pc.y - pa.y) - (pb.y - pa.y) * (pc.x - pa.x);
            indices[cursor++] = a;
            indices[cursor++] = clockwise ? b : c;
            indices[cursor++] = clockwise ? c : b;
        }
    }
}
