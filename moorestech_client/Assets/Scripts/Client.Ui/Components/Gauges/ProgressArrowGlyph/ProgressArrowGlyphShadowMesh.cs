using Client.Ui.Foundation.Rendering;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Gauges
{
    /// <summary>
    /// 縁の内外 2σ を刻む輪の頂点カラーメッシュ
    /// Vertex-colored ring mesh spanning 2σ inside and outside the edge
    /// </summary>
    /// <remarks>
    /// 輪 0（内端）の内側は芯として輪 0 の濃さで塗る。輪 k は縁からの距離 (2k/8-1)·2σ にあり、濃さは Web の α に被覆率を掛けて
    /// 色の α に被覆率を掛ける。8 段は GamePanelFace の SegmentsPerFade と同じ刻み。
    /// The inside of ring 0 (inner end) fills as the core at ring 0's alpha. Ring k sits at (2k/8-1)·2σ from the edge; its alpha multiplies the Web alpha by coverage
    /// multiplied into the colour alpha. Eight steps match GamePanelFace.SegmentsPerFade.
    /// </remarks>
    internal static class ProgressArrowGlyphShadowMesh
    {
        public const int VertexCount = (ShadowRingMeshWriter.RingCount + 1) * ProgressArrowGlyphPath.VertexCount;
        public const int IndexCount = ProgressArrowGlyphPath.FillTriangleIndexCount + ShadowRingMeshWriter.RingCount * ProgressArrowGlyphPath.VertexCount * 6;

        // にじみの刻み幅（片側 2σ）
        // Blur span (2σ each side)
        private const float SigmaSpan = 2f;

        public static void Build(float scale, float silhouetteOutset, float offsetY, float blur, Color shadowColor, Vertex[] vertices, ushort[] indices)
        {
            // drop-shadow のぼかしは標準偏差そのもの（box-shadow の半分ではない）
            // A drop-shadow blur is the standard deviation itself (not half of it as in box-shadow)
            var sigma = blur;
            var ring = new Vector2[ProgressArrowGlyphPath.VertexCount];
            WriteRingVertices();
            var cursor = 0;
            WriteCore();
            ShadowRingMeshWriter.WriteBands(vertices, indices, ref cursor, ShadowRingMeshWriter.RingCount, ProgressArrowGlyphPath.VertexCount);

            #region Internal

            // 輪ごとにずらし、被覆率から濃さを決める
            // Offset per ring; alpha from coverage
            void WriteRingVertices()
            {
                for (var k = 0; k <= ShadowRingMeshWriter.RingCount; k++)
                {
                    var fromEdge = (2f * k / ShadowRingMeshWriter.RingCount - 1f) * SigmaSpan * sigma;
                    // 縁を輪で刻む近似で、先端の角は丸まらず 2σ の外の裾も無い（look-exception: LX-56）
                    // Ring approximation of the edge: the tip corner is not rounded and there is no tail beyond 2σ (look-exception: LX-56)
                    // 小寸法で軸の半高より内へずらすと軸の輪が裏返り影が二重に乗るので、中心線の手前で止める
                    // At small sizes an inward offset past the shaft half-height flips the ring and doubles the shadow, so stop short of the centre line
                    var distance = Mathf.Max(silhouetteOutset + fromEdge, ShadowRingMeshWriter.CenterMargin - ProgressArrowGlyphPath.ShaftHalfHeight * scale);
                    ProgressArrowGlyphPath.WriteOffset(scale, distance, new Vector2(0f, offsetY), ring);
                    var tint = shadowColor;
                    // 濃さは形全体で一律の Web α（Web は描いた画素の α で溝・線の下が淡い。look-exception: LX-57）
                    // One Web alpha over the whole shape (Web is lighter under the groove and stroke; look-exception: LX-57)
                    tint.a = shadowColor.a * GaussianEdgeCoverage.At(fromEdge, sigma);
                    for (var corner = 0; corner < ring.Length; corner++)
                        vertices[k * ring.Length + corner] = new Vertex { position = new Vector3(ring[corner].x, ring[corner].y, Vertex.nearZ), tint = tint };
                }
            }

            void WriteCore()
            {
                for (var i = 0; i < ProgressArrowGlyphPath.FillTriangleIndexCount; i += 3)
                    ShadowRingMeshWriter.WriteClockwise(vertices, indices, ref cursor, ProgressArrowGlyphPath.FillTriangleIndexAt(i), ProgressArrowGlyphPath.FillTriangleIndexAt(i + 1), ProgressArrowGlyphPath.FillTriangleIndexAt(i + 2));
            }

            #endregion
        }
    }
}
