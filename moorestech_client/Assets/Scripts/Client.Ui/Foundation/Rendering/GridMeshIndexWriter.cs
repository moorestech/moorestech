using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Rendering
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
            for (var slot = 0; slot < IndexCount; slot++) mesh.SetNextIndex(IndexAt(slot));
        }

        public void WriteTo(ushort[] indices, int offset)
        {
            for (var slot = 0; slot < IndexCount; slot++) indices[offset + slot] = IndexAt(slot);
        }

        // 両方の書き口が回す唯一の並び
        // The single ordering both writers iterate
        private ushort IndexAt(int slot)
        {
            switch (slot)
            {
                case 0: return TopLeft;
                case 1: return TopRight;
                case 2: return BottomRight;
                case 3: return TopLeft;
                case 4: return BottomRight;
                case 5: return BottomLeft;
                default: throw new System.ArgumentOutOfRangeException(nameof(slot), slot, "GridQuadIndices has 6 slots");
            }
        }
    }
}
