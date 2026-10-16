using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    /// <summary>
    ///     地形の高さから設置セルYを決める（ADR 0082）
    ///     Decides the placement cell Y from the terrain height (ADR 0082)
    /// </summary>
    public static class PlacementGroundCellResolver
    {
        // 底面からの沈み許容量（ADR 0082）
        // Allowed sink depth below the bottom (ADR 0082)
        private const float AllowedTerrainSinkMeters = 1f / 3f;

        // 浮動小数点誤差の余裕。y≈1000から約1000m落とすRaycastのfloat刻み（約6e-5m）の10倍以上をとる
        // Floating-point margin; over ten times the float ulp (~6e-5 m) of a ~1000 m raycast from y≈1000
        private const float FloatingPointGroundTolerance = 0.001f;

        // 占有範囲の地形最高点からYを決め直す。地表が無ければ失敗を返し、呼び出し側が設置不可として扱う
        // Re-decides Y from the footprint's terrain max height; a missing ground fails so the caller can block the cell
        public static bool TryResolveCellFromGround(Vector3Int cellPosition, BlockDirection blockDirection, Vector3Int blockSize, int heightOffset, out Vector3Int resolvedPosition)
        {
            resolvedPosition = cellPosition;
            if (!GroundHeightProbe.TryGetFootprintMaxGroundHeight(cellPosition, blockDirection, blockSize, out var groundMaxHeight)) return false;

            resolvedPosition = new Vector3Int(cellPosition.x, ResolveCellY(groundMaxHeight, heightOffset), cellPosition.z);
            return true;
        }

        // 沈みが許容量以下になる最も低いセルを返す。整数直下に格納された鉱脈パッドもこの許容量に収まり、その整数セルになる
        // Returns the lowest cell whose sink stays within the allowance; a vein pad stored just under an integer also fits and lands on that integer
        internal static int ResolveCellY(float groundMaxHeight, int heightOffset)
        {
            return Mathf.CeilToInt(groundMaxHeight - AllowedTerrainSinkMeters - FloatingPointGroundTolerance) + heightOffset;
        }
    }
}
