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
        // 浮動小数点誤差で整数の地表が僅かに下回る分の余裕
        // Margin for floating-point error leaving an integer ground just below the integer
        private const float FloatingPointGroundTolerance = 0.001f;

        // 占有範囲の地形最高点からYを決め直す。地表が無ければ失敗を返し、呼び出し側が設置不可として扱う
        // Re-decides Y from the footprint's terrain max height; a missing ground fails so the caller can block the cell
        public static bool TryResolveCellFromGround(Vector3Int cellPosition, BlockDirection blockDirection, Vector3Int blockSize, int heightOffset, out Vector3Int resolvedPosition)
        {
            resolvedPosition = cellPosition;
            if (!GroundHeightProbe.TryGetFootprintMaxGroundHeight(cellPosition, blockDirection, blockSize, out var groundMaxHeight, out var heightQuantizationStep)) return false;

            resolvedPosition = new Vector3Int(cellPosition.x, ResolveCellY(groundMaxHeight, heightQuantizationStep, heightOffset), cellPosition.z);
            return true;
        }

        // 地形最高点を含むセルを返す。TerrainDataの16bit格子では整数のつもりの地表（鉱脈パッド等）が整数を最大1段下回って格納されるため、1段＋誤差ぶんは整数へ引き上げる
        // Returns the cell containing the terrain max; TerrainData's 16-bit lattice can store an integer-intended surface (e.g. a vein pad) up to one step below the integer, so one step plus float error is lifted to it
        internal static int ResolveCellY(float groundMaxHeight, float heightQuantizationStep, int heightOffset)
        {
            var integerGroundTolerance = heightQuantizationStep + FloatingPointGroundTolerance;
            return Mathf.FloorToInt(groundMaxHeight + integerGroundTolerance) + heightOffset;
        }
    }
}
