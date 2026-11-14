using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Theme.Mesh
{
    /// <summary>
    /// 格子の頂点から四角を 2 枚の三角で書く唯一の並び（画面座標で時計回り。y は下向き）
    /// The single ordering that writes each grid quad as two triangles (clockwise in screen space, y down)
    /// </summary>
    public static class GridMeshIndexWriter
    {
        public static void WriteQuads(MeshWriteData mesh, int rows, int columns, int stride)
        {
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                QuadIndices(row, column, stride).WriteTo(mesh);
            }
        }

        public static GridQuadIndices QuadIndices(int row, int column, int stride)
        {
            var topLeft = (ushort)(row * stride + column);
            var bottomLeft = (ushort)(topLeft + stride);
            return new GridQuadIndices(topLeft, (ushort)(topLeft + 1), (ushort)(bottomLeft + 1), bottomLeft);
        }
    }

    /// <summary>
    /// 四角 1 枚の 4 頂点の index。配列を作らず描き直しの割り当てを 0 に保つ
    /// The 4 vertex indices of one quad; no array is allocated so redraws stay allocation-free
    /// </summary>
    /// <remarks>
    /// 並びは TopLeft・TopRight・BottomRight・TopLeft・BottomRight・BottomLeft の 1 つ。MeshWriteData へ直接書く口と、事前に組む index 配列へ書く口が同じ並びを持つ。
    /// The order is the single TopLeft, TopRight, BottomRight, TopLeft, BottomRight, BottomLeft. The direct MeshWriteData writer and the prebuilt index-array writer share it.
    /// </remarks>
    public readonly struct GridQuadIndices
    {
        public const int IndexCount = 6;

        public readonly ushort TopLeft, TopRight, BottomRight, BottomLeft;

        public GridQuadIndices(ushort topLeft, ushort topRight, ushort bottomRight, ushort bottomLeft)
        {
            TopLeft = topLeft;
            TopRight = topRight;
            BottomRight = bottomRight;
            BottomLeft = bottomLeft;
        }

        public void WriteTo(MeshWriteData mesh)
        {
            mesh.SetNextIndex(TopLeft);
            mesh.SetNextIndex(TopRight);
            mesh.SetNextIndex(BottomRight);
            mesh.SetNextIndex(TopLeft);
            mesh.SetNextIndex(BottomRight);
            mesh.SetNextIndex(BottomLeft);
        }

        public void WriteTo(ushort[] indices, int offset)
        {
            indices[offset] = TopLeft;
            indices[offset + 1] = TopRight;
            indices[offset + 2] = BottomRight;
            indices[offset + 3] = TopLeft;
            indices[offset + 4] = BottomRight;
            indices[offset + 5] = BottomLeft;
        }
    }
}
