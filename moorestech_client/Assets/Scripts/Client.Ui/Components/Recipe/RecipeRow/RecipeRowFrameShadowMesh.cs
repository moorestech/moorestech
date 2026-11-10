using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Recipe
{
    /// <summary>
    /// レシピ行の光彩（box-shadow 0 0 2px）と括弧の drop-shadow（0 0 1px）を、縁からの距離で刻む矩形の輪の頂点カラーメッシュで組む
    /// Builds the recipe row glow (box-shadow 0 0 2px) and the bracket drop-shadow (0 0 1px) as vertex-colored meshes of rectangle rings stepped by distance from the edge
    /// </summary>
    /// <remarks>
    /// 前例 ProgressArrowGlyphShadowMesh と同じく輪を 8 段で刻み、濃さは Web の α に GaussianEdgeCoverage の被覆率を掛ける（括弧の棒は太さが σ と同じ程度なので両縁の差）。
    /// UI は sRGB 空間で合成するので α 補正は要らず、頂点の α はトークンの α × 被覆率をそのまま使う。
    /// box-shadow のぼかし 2px は σ = 1px（ぼかしの半分）、drop-shadow の 1px は σ そのもの。光彩は線の箱の外だけに出る（外向きの box-shadow は要素の下を塗らない）。
    /// Rings are stepped in eight like the ProgressArrowGlyphShadowMesh precedent, and the alpha is the Web alpha times the GaussianEdgeCoverage coverage (the two-edge difference for bracket bars, whose thickness is about σ).
    /// The UI composites in sRGB, so no alpha correction is needed; a vertex alpha is the token alpha times the coverage as-is.
    /// A 2px box-shadow blur is σ = 1px (half the blur) and a 1px drop-shadow is σ itself. The glow only shows outside the border box (an outer box-shadow never paints under the element).
    /// </remarks>
    internal static class RecipeRowFrameShadowMesh
    {
        public const int RingCount = 8;
        public const int GlowVertexCount = (RingCount + 1) * 4;
        public const int GlowIndexCount = RingCount * 4 * 6;
        public const int BarShadowVertexCount = (RingCount + 1) * 4;
        public const int BarShadowIndexCount = 6 + RingCount * 4 * 6;

        // 1px の棒の輪を内側へ裏返さない止め（px）
        // Stop that keeps a 1px bar's rings from flipping inward (px)
        private const float BarCenterMargin = 0.01f;

        public static void BuildGlow(Rect borderBox, float sigma, Color glowColor, Vertex[] vertices, ushort[] indices)
        {
            // 線の箱の縁（被覆率 0.5）から外へ 2σ まで刻む
            // Step outward from the border box edge (coverage 0.5) to 2σ
            for (var k = 0; k <= RingCount; k++)
            {
                var distance = 2f * sigma * k / RingCount;
                WriteRing(vertices, k * 4, Grow(borderBox, distance), Tint(glowColor, GaussianEdgeCoverage.At(distance, sigma)));
            }
            var cursor = 0;
            for (var k = 0; k < RingCount; k++) WriteBand(vertices, indices, ref cursor, k * 4);
        }

        public static void BuildBarShadow(Rect bar, float sigma, Color shadowColor, Vertex[] vertices, ushort[] indices)
        {
            // 縁の内外 2σ を刻む。内側は棒の太さの半分の手前で止める（前例の中心線の手前で止める形）
            // Step 2σ inside and outside the edge; inward steps stop short of half the bar thickness (the precedent's stop before the centre line)
            var thickness = Mathf.Min(bar.width, bar.height);
            var innerLimit = BarCenterMargin - thickness * 0.5f;
            for (var k = 0; k <= RingCount; k++)
            {
                var fromEdge = Mathf.Max((2f * k / RingCount - 1f) * 2f * sigma, innerLimit);
                WriteRing(vertices, k * 4, Grow(bar, fromEdge), Tint(shadowColor, BarEdgeCoverage(fromEdge, thickness, sigma)));
            }

            // 輪 0 の内側は芯として輪 0 の濃さで塗る
            // The inside of ring 0 fills as the core at ring 0's alpha
            var cursor = 0;
            WriteClockwise(vertices, indices, ref cursor, 0, 1, 2);
            WriteClockwise(vertices, indices, ref cursor, 0, 2, 3);
            for (var k = 0; k < RingCount; k++) WriteBand(vertices, indices, ref cursor, k * 4);
        }

        private static float BarEdgeCoverage(float fromEdge, float thickness, float sigma)
        {
            // 細い棒は反対側の縁が σ の内にあるので、帯 [-太さ, 0] の被覆率で両縁の差を取る（1px・σ 1px で芯 0.383）
            // A thin bar's far edge lies within σ, so take the two-edge difference as the coverage of the band [-thickness, 0] (0.383 at the core for 1px and σ 1px)
            return GaussianEdgeCoverage.BandCoverage(fromEdge, -thickness, 0f, sigma);
        }

        private static Rect Grow(Rect rect, float distance)
        {
            return new Rect(rect.xMin - distance, rect.yMin - distance, rect.width + distance * 2f, rect.height + distance * 2f);
        }

        private static Color Tint(Color tokenColor, float coverage)
        {
            var tint = tokenColor;
            tint.a = tokenColor.a * coverage;
            return tint;
        }

        private static void WriteRing(Vertex[] vertices, int first, Rect rect, Color tint)
        {
            // 左上・右上・右下・左下の順（y 下向き）
            // Top-left, top-right, bottom-right, bottom-left (y down)
            vertices[first] = new Vertex { position = new Vector3(rect.xMin, rect.yMin, Vertex.nearZ), tint = tint };
            vertices[first + 1] = new Vertex { position = new Vector3(rect.xMax, rect.yMin, Vertex.nearZ), tint = tint };
            vertices[first + 2] = new Vertex { position = new Vector3(rect.xMax, rect.yMax, Vertex.nearZ), tint = tint };
            vertices[first + 3] = new Vertex { position = new Vector3(rect.xMin, rect.yMax, Vertex.nearZ), tint = tint };
        }

        private static void WriteBand(Vertex[] vertices, ushort[] indices, ref int cursor, int innerFirst)
        {
            // 隣り合う輪の同じ辺を四角で結ぶ
            // Join the same side of neighbouring rings with a quad
            for (var side = 0; side < 4; side++)
            {
                var innerFrom = (ushort)(innerFirst + side);
                var innerTo = (ushort)(innerFirst + (side + 1) % 4);
                var outerFrom = (ushort)(innerFrom + 4);
                var outerTo = (ushort)(innerTo + 4);
                WriteClockwise(vertices, indices, ref cursor, innerFrom, outerTo, innerTo);
                WriteClockwise(vertices, indices, ref cursor, innerFrom, outerFrom, outerTo);
            }
        }

        private static void WriteClockwise(Vertex[] vertices, ushort[] indices, ref int cursor, ushort a, ushort b, ushort c)
        {
            // 時計回り（y 下向き）の順で書く（前例 ProgressArrowGlyphShadowMesh と同じ判定）
            // Write in clockwise order (y down), with the same test as the ProgressArrowGlyphShadowMesh precedent
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
