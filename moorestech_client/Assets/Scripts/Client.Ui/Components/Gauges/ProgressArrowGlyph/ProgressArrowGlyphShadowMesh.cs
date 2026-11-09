using Client.Ui.Components.Panels;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Gauges
{
    /// <summary>
    /// 矢印の drop-shadow を、線の外縁を下へずらした縁の内外 2σ を刻む輪の頂点カラーのメッシュとして組む
    /// Builds the arrow drop-shadow as a vertex-colored mesh of rings spanning 2σ inside and outside the stroke's outer edge shifted down
    /// </summary>
    /// <remarks>
    /// 輪 0（内端）の内側は芯として輪 0 の濃さで塗る。輪 k は縁からの距離 (2k/8-1)·2σ にあり、濃さは Web の α に被覆率を掛けて
    /// GamePanelFaceOpacityRamp.OpacityAt で Linear 合成用に補正する。8 段は GamePanelFace の SegmentsPerFade と同じ刻み。
    /// The inside of ring 0 (inner end) fills as the core at ring 0's alpha. Ring k sits at (2k/8-1)·2σ from the edge; its alpha multiplies the Web alpha by coverage
    /// and is corrected for Linear blending through GamePanelFaceOpacityRamp.OpacityAt. Eight steps match GamePanelFace.SegmentsPerFade.
    /// </remarks>
    internal static class ProgressArrowGlyphShadowMesh
    {
        public const int RingCount = 8;
        public const int VertexCount = (RingCount + 1) * ProgressArrowGlyphPath.VertexCount;
        public const int IndexCount = ProgressArrowGlyphPath.FillTriangleIndexCount + RingCount * ProgressArrowGlyphPath.VertexCount * 6;

        // にじみを刻む幅（片側 2σ で被覆率は 2.3% まで落ちる）
        // Span of the blur on each side (coverage falls to 2.3% at 2σ)
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
            WriteBands();

            #region Internal

            // 輪ごとに縁からの距離で形をずらし、その距離の被覆率から濃さを決める
            // Offset the shape by each ring's distance from the edge and derive its alpha from the coverage there
            void WriteRingVertices()
            {
                for (var k = 0; k <= RingCount; k++)
                {
                    var fromEdge = (2f * k / RingCount - 1f) * SigmaSpan * sigma;
                    // 縁を輪で刻む近似で、先端の角は丸まらず 2σ の外の裾も無い（look-exception: LX-56）
                    // Ring approximation of the edge: the tip corner is not rounded and there is no tail beyond 2σ (look-exception: LX-56)
                    ProgressArrowGlyphPath.WriteOffset(scale, silhouetteOutset + fromEdge, new Vector2(0f, offsetY), ring);
                    var tint = shadowColor;
                    // 濃さは形全体で一律の Web α（Web は描いた画素の α で溝・線の下が淡い。look-exception: LX-57）
                    // One Web alpha over the whole shape (Web is lighter under the groove and stroke; look-exception: LX-57)
                    tint.a = GamePanelFaceOpacityRamp.OpacityAt(shadowColor.a, GaussianEdgeCoverage.At(fromEdge, sigma));
                    for (var corner = 0; corner < ring.Length; corner++)
                        vertices[k * ring.Length + corner] = new Vertex { position = new Vector3(ring[corner].x, ring[corner].y, Vertex.nearZ), tint = tint };
                }
            }

            void WriteCore()
            {
                for (var i = 0; i < ProgressArrowGlyphPath.FillTriangleIndexCount; i += 3)
                    WriteClockwise(ProgressArrowGlyphPath.FillTriangleIndexAt(i), ProgressArrowGlyphPath.FillTriangleIndexAt(i + 1), ProgressArrowGlyphPath.FillTriangleIndexAt(i + 2));
            }

            // 隣り合う輪の同じ辺を四角で結ぶ
            // Join the same edge of neighbouring rings with a quad
            void WriteBands()
            {
                var corners = ProgressArrowGlyphPath.VertexCount;
                for (var k = 0; k < RingCount; k++)
                for (var corner = 0; corner < corners; corner++)
                {
                    var innerFrom = (ushort)(k * corners + corner);
                    var innerTo = (ushort)(k * corners + (corner + 1) % corners);
                    var outerFrom = (ushort)(innerFrom + corners);
                    var outerTo = (ushort)(innerTo + corners);
                    WriteClockwise(innerFrom, outerTo, innerTo);
                    WriteClockwise(innerFrom, outerFrom, outerTo);
                }
            }

            // 画面座標（y 下向き）で時計回りになる順に入れ替えて書く（GamePanelFace と同じ向き）
            // Write in the order that winds clockwise in screen space (y down), the same as GamePanelFace
            void WriteClockwise(ushort a, ushort b, ushort c)
            {
                var pa = vertices[a].position;
                var pb = vertices[b].position;
                var pc = vertices[c].position;
                var clockwise = (pb.x - pa.x) * (pc.y - pa.y) - (pb.y - pa.y) * (pc.x - pa.x) > 0f;
                indices[cursor++] = a;
                indices[cursor++] = clockwise ? b : c;
                indices[cursor++] = clockwise ? c : b;
            }

            #endregion
        }
    }
}
