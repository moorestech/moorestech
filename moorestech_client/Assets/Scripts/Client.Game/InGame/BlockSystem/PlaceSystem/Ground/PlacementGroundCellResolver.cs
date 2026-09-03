using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    /// <summary>
    ///     地形の高さから設置セルYを決める（ADR 0047）
    ///     Decides the placement cell Y from the terrain height (ADR 0047)
    /// </summary>
    public static class PlacementGroundCellResolver
    {
        // 整数の地表が誤差で1段沈むのを防ぐ
        // Keeps ground exactly on an integer from sinking one cell
        private const float IntegerGroundTolerance = 0.001f;

        // 占有範囲の地形最高点からYを決め直す。地表が無ければ失敗を返し、呼び出し側が設置不可として扱う
        // Re-decides Y from the footprint's terrain max height; a missing ground fails so the caller can block the cell
        public static bool TryResolveCellFromGround(Vector3Int cellPosition, BlockDirection blockDirection, Vector3Int blockSize, int heightOffset, out Vector3Int resolvedPosition)
        {
            resolvedPosition = cellPosition;
            if (!GroundHeightProbe.TryGetFootprintMaxGroundHeight(cellPosition, blockDirection, blockSize, out var groundMaxHeight)) return false;

            resolvedPosition = new Vector3Int(cellPosition.x, ResolveCellY(groundMaxHeight, heightOffset), cellPosition.z);
            return true;
        }

        // 地形最高点を含むセルを返す
        // Returns the cell containing the terrain max height
        private static int ResolveCellY(float groundMaxHeight, int heightOffset)
        {
            return Mathf.FloorToInt(groundMaxHeight + IntegerGroundTolerance) + heightOffset;
        }
    }
}
