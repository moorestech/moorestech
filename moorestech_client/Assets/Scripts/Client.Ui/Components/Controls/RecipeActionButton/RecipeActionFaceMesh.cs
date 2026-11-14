using Client.Ui.Foundation.Theme.Mesh;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// 主要アクションの面の横 3 色グラデを頂点カラーの帯で組む
    /// Builds the three-stop horizontal gradient of the primary action face as a vertex-colored strip
    /// </summary>
    /// <remarks>
    /// 停止位置は Web の linear-gradient(90deg, …) の 0%・50%・100%。各半分を前例 GamePanelFace と同じ 8 列に刻み、列の色を sRGB の成分で補間して置く（結果は端点 3 色の sRGB 線形補間と一致する）。グラデは枠の太さだけ内側の箱に敷き、枠の帯には Web の background（padding-box 起点・repeat）と同じく隣の 1 枚の端を回り込ませる（A-128）。
    /// Stops are the 0%, 50% and 100% of the Web linear-gradient(90deg, …). Each half is cut into 8 columns like the GamePanelFace precedent, with column colors lerped per sRGB channel (the result matches the sRGB linear interpolation of the three stops). The gradient spans the box inset by the border width, and the border bands show the neighbouring tile's edge like the Web background (padding-box origin, repeat; A-128).
    /// </remarks>
    internal static class RecipeActionFaceMesh
    {
        public const int SegmentsPerHalf = 8;
        public const int ColumnCount = SegmentsPerHalf * 2 + 1;
        public const int LeftBorderFirstVertex = ColumnCount * 2;
        public const int RightBorderFirstVertex = LeftBorderFirstVertex + 4;
        public const int VertexCount = RightBorderFirstVertex + 4;
        public const int IndexCount = (ColumnCount - 1) * 6 + 12;

        public static void Build(float width, float height, float borderWidth, Color start, Color middle, Color end, Vertex[] vertices, ushort[] indices)
        {
            var innerWidth = width - borderWidth * 2f;
            WriteStrip();
            WriteBorderBands();
            WriteIndices();

            #region Internal

            // 枠の内側の箱に、上の行と下の行で同じ色の列を並べる
            // Lay the same column colors on the top and bottom rows inside the border box
            void WriteStrip()
            {
                for (var column = 0; column < ColumnCount; column++)
                {
                    var ratio = (float)column / (ColumnCount - 1);
                    var tint = ColorAt(ratio);
                    var x = borderWidth + innerWidth * ratio;
                    vertices[column] = MakeVertex(x, 0f, tint);
                    vertices[ColumnCount + column] = MakeVertex(x, height, tint);
                }
            }

            // 左の枠には左隣の 1 枚の右端、右の枠には右隣の 1 枚の左端が回り込む（padding-box 起点の repeat）
            // The left border shows the right end of the tile to its left, the right border the left end of the next tile (padding-box origin, repeat)
            void WriteBorderBands()
            {
                var wrapRatio = borderWidth / innerWidth;
                WriteBand(LeftBorderFirstVertex, 0f, borderWidth, ColorAt(1f - wrapRatio), end);
                WriteBand(RightBorderFirstVertex, width - borderWidth, width, start, ColorAt(wrapRatio));
            }

            void WriteBand(int first, float left, float right, Color leftTint, Color rightTint)
            {
                vertices[first] = MakeVertex(left, 0f, leftTint);
                vertices[first + 1] = MakeVertex(right, 0f, rightTint);
                vertices[first + 2] = MakeVertex(left, height, leftTint);
                vertices[first + 3] = MakeVertex(right, height, rightTint);
            }

            // 前半は start→middle、後半は middle→end を sRGB の成分で補間する
            // The first half lerps start to middle and the second middle to end, per sRGB channel
            Color ColorAt(float ratio)
            {
                return ratio <= 0.5f ? Color.Lerp(start, middle, ratio * 2f) : Color.Lerp(middle, end, (ratio - 0.5f) * 2f);
            }

            // y 下向きで時計回りに 1 区画 2 三角（帯 16 区画→左右の枠）
            // Two clockwise triangles per cell, y down (16 strip cells, then side borders)
            void WriteIndices()
            {
                for (var column = 0; column < ColumnCount - 1; column++)
                    GridMeshIndexWriter.QuadIndices(0, column, ColumnCount).WriteTo(indices, column * GridQuadIndices.IndexCount);
                WriteBandQuad((ColumnCount - 1) * GridQuadIndices.IndexCount, LeftBorderFirstVertex);
                WriteBandQuad(ColumnCount * GridQuadIndices.IndexCount, RightBorderFirstVertex);
            }

            // 枠の帯の頂点は WriteBand の順（左上・右上・左下・右下）
            // Border band vertices follow WriteBand (top-left, top-right, bottom-left, bottom-right)
            void WriteBandQuad(int offset, int first)
            {
                new GridQuadIndices((ushort)first, (ushort)(first + 1), (ushort)(first + 3), (ushort)(first + 2)).WriteTo(indices, offset);
            }

            Vertex MakeVertex(float x, float y, Color tint)
            {
                return new Vertex { position = new Vector3(x, y, Vertex.nearZ), tint = tint };
            }

            #endregion
        }
    }
}
