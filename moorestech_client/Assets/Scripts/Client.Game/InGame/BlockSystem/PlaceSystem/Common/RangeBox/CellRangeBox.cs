using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox
{
    /// <summary>
    ///     対角2セルから範囲（セル [min, max] を含む直方体）を計算する純関数
    ///     Pure helpers that turn two diagonal cells into a cell range (inclusive box)
    /// </summary>
    public static class CellRangeBox
    {
        public static (Vector3Int min, Vector3Int max) Calc(Vector3Int a, Vector3Int b)
        {
            return (Vector3Int.Min(a, b), Vector3Int.Max(a, b));
        }

        // セル境界から中心とサイズを求める（max セルの外側面まで含む）
        // Derive center and size from the cell borders, including the far face of the max cell
        public static Bounds ToWorldBounds(Vector3Int min, Vector3Int max)
        {
            var size = new Vector3(max.x - min.x + 1, max.y - min.y + 1, max.z - min.z + 1);
            return new Bounds(new Vector3(min.x, min.y, min.z) + size * 0.5f, size);
        }
    }
}
