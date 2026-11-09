using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Gauges
{
    /// <summary>
    /// Web の矢印の SVG path（M2 27H69V2L119 39L69 76V51H2Z・viewBox 121×78）の頂点と当てはめ・ずらし
    /// Vertices, fitting and offsetting of the Web arrow SVG path (M2 27H69V2L119 39L69 76V51H2Z, viewBox 121x78)
    /// </summary>
    internal static class ProgressArrowGlyphPath
    {
        public const float ViewBoxWidth = 121f;
        public const float ViewBoxHeight = 78f;
        public const float ArrowLeft = 2f;
        public const float ArrowRight = 119f;
        public const float ArrowSpan = ArrowRight - ArrowLeft;
        public const int VertexCount = 7;
        public const int FillTriangleIndexCount = 9;

        // path の頂点を画面座標（y 下向き）の時計回りに並べる（Web の ARROW_PATH と同じ順）
        // Path vertices clockwise in screen space (y down), in the Web ARROW_PATH order
        private static readonly Vector2[] Vertices = { new(2f, 27f), new(69f, 27f), new(69f, 2f), new(119f, 39f), new(69f, 76f), new(69f, 51f), new(2f, 51f) };

        // 軸の長方形 2 枚と先端の三角 1 枚。ずらしても x=69 の 4 頂点が一直線に並び続けるので有効なまま
        // Two shaft triangles and the head; the four x=69 vertices stay collinear under offsetting, so they remain valid
        private static readonly ushort[] FillTriangles = { 0, 1, 5, 0, 5, 6, 2, 3, 4 };

        public static ushort FillTriangleIndexAt(int position)
        {
            return FillTriangles[position];
        }

        // SVG の既定 preserveAspectRatio（xMidYMid meet）の倍率
        // The scale of the SVG default preserveAspectRatio (xMidYMid meet)
        public static float ScaleOf(Vector2 size)
        {
            return Mathf.Min(size.x / ViewBoxWidth, size.y / ViewBoxHeight);
        }

        public static Rect FitViewBox(Vector2 size)
        {
            var scale = ScaleOf(size);
            var width = ViewBoxWidth * scale;
            var height = ViewBoxHeight * scale;
            return new Rect((size.x - width) / 2f, (size.y - height) / 2f, width, height);
        }

        // 倍率を掛けた path を Painter2D に閉じた多角形として描き込む（塗りと輪郭が共有する）
        // Trace the scaled path into Painter2D as a closed polygon (shared by fill and outline)
        public static void Trace(Painter2D painter, float scale)
        {
            painter.BeginPath();
            painter.MoveTo(Vertices[0] * scale);
            for (var i = 1; i < VertexCount; i++) painter.LineTo(Vertices[i] * scale);
            painter.ClosePath();
        }

        public static void WriteOffset(float scale, float distance, Vector2 shift, Vector2[] destination)
        {
            // 各頂点を前後の辺の外向き法線の和の向きへ、辺が distance だけ平行に動く長さ（miter）で動かす
            // Move each vertex along the sum of its two edges' outward normals by the miter length that shifts each edge by distance
            for (var i = 0; i < VertexCount; i++)
            {
                var incoming = OutwardNormal(Vertices[(i + VertexCount - 1) % VertexCount], Vertices[i]);
                var outgoing = OutwardNormal(Vertices[i], Vertices[(i + 1) % VertexCount]);
                var miter = (incoming + outgoing) / (1f + Vector2.Dot(incoming, outgoing));
                destination[i] = Vertices[i] * scale + miter * distance + shift;
            }

            #region Internal

            // 時計回り（y 下向き）の辺の外側は進む向きを左へ 90° 回した側
            // For a clockwise edge in y-down space the outside is the direction rotated 90 degrees to the left
            Vector2 OutwardNormal(Vector2 from, Vector2 to)
            {
                var edge = (to - from).normalized;
                return new Vector2(edge.y, -edge.x);
            }

            #endregion
        }
    }
}
