using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Rendering
{
    /// <summary>
    /// 影の輪メッシュのインデックスを書く共有の口
    /// Shared index writer for shadow ring meshes
    /// </summary>
    /// <remarks>
    /// 輪 k の角 c は頂点 k·角数+c に並ぶ前提。UITK は裏向きの三角を描かないので、三角は頂点の位置から必ず時計回り（y 下向き）で書く。
    /// Corner c of ring k sits at vertex k·corners+c. UITK culls back-facing triangles, so each one is written clockwise (y down) from the vertex positions.
    /// </remarks>
    public static class ShadowRingMeshWriter
    {
        // 縁の内外を刻む輪の段数（GamePanelFace の SegmentsPerFade と同じ 8 段）
        // Number of rings stepping across the edge (eight, the same as GamePanelFace.SegmentsPerFade)
        public const int RingCount = 8;

        // 細い形の輪が中心線を越えて裏返らないよう、中心の縫い目から空ける余白（px）
        // Gap in px from the centre seam that keeps a thin shape's rings from crossing it and flipping
        public const float CenterMargin = 0.01f;

        public static void WriteBands(Vertex[] vertices, ushort[] indices, ref int cursor, int ringCount, int cornersPerRing)
        {
            // 隣り合う輪の辺を四角で結ぶ
            // Join neighbouring ring sides with a quad
            for (var ring = 0; ring < ringCount; ring++)
            for (var corner = 0; corner < cornersPerRing; corner++)
            {
                var band = BandQuad(ring, corner, cornersPerRing);
                WriteClockwise(vertices, indices, ref cursor, band.InnerFrom, band.OuterTo, band.InnerTo);
                WriteClockwise(vertices, indices, ref cursor, band.InnerFrom, band.OuterFrom, band.OuterTo);
            }
        }

        // MeshWriteData へ直接書く口。頂点は読み戻せないので、巻き向きは呼び手が同じ並びで渡す vertices から決める
        // Direct MeshWriteData writer; the mesh cannot be read back, so winding is decided from the caller's vertices in the same layout
        public static void WriteBands(MeshWriteData mesh, Vertex[] vertices, int ringCount, int cornersPerRing)
        {
            for (var ring = 0; ring < ringCount; ring++)
            for (var corner = 0; corner < cornersPerRing; corner++)
            {
                var band = BandQuad(ring, corner, cornersPerRing);
                WriteClockwise(mesh, vertices, band.InnerFrom, band.OuterTo, band.InnerTo);
                WriteClockwise(mesh, vertices, band.InnerFrom, band.OuterFrom, band.OuterTo);
            }
        }

        public static void WriteClockwise(Vertex[] vertices, ushort[] indices, ref int cursor, ushort a, ushort b, ushort c)
        {
            // 反時計回りなら b と c を入れ替える
            // Swap b and c when counter-clockwise
            var clockwise = IsClockwise(vertices, a, b, c);
            indices[cursor++] = a;
            indices[cursor++] = clockwise ? b : c;
            indices[cursor++] = clockwise ? c : b;
        }

        private static void WriteClockwise(MeshWriteData mesh, Vertex[] vertices, ushort a, ushort b, ushort c)
        {
            var clockwise = IsClockwise(vertices, a, b, c);
            mesh.SetNextIndex(a);
            mesh.SetNextIndex(clockwise ? b : c);
            mesh.SetNextIndex(clockwise ? c : b);
        }

        private static bool IsClockwise(Vertex[] vertices, ushort a, ushort b, ushort c)
        {
            var pa = vertices[a].position;
            var pb = vertices[b].position;
            var pc = vertices[c].position;
            return 0f < (pb.x - pa.x) * (pc.y - pa.y) - (pb.y - pa.y) * (pc.x - pa.x);
        }

        // 輪 ring の角 corner から次の角への辺と、1 つ外の輪の同じ辺。両方の書き口が使う唯一の並び
        // The side from corner to the next on ring, and the same side one ring out; the single layout both writers use
        private static RingBandQuad BandQuad(int ring, int corner, int cornersPerRing)
        {
            var innerFrom = (ushort)(ring * cornersPerRing + corner);
            var innerTo = (ushort)(ring * cornersPerRing + (corner + 1) % cornersPerRing);
            return new RingBandQuad(innerFrom, innerTo, (ushort)(innerFrom + cornersPerRing), (ushort)(innerTo + cornersPerRing));
        }

        private readonly struct RingBandQuad
        {
            public readonly ushort InnerFrom, InnerTo, OuterFrom, OuterTo;

            public RingBandQuad(ushort innerFrom, ushort innerTo, ushort outerFrom, ushort outerTo)
            {
                InnerFrom = innerFrom;
                InnerTo = innerTo;
                OuterFrom = outerFrom;
                OuterTo = outerTo;
            }
        }
    }
}
