using Game.Block.Interface;
using Game.MapGeneration.Surface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Ground
{
    /// <summary>
    ///     地形の高さから設置セルYを決める（ADR 0047）
    ///     Decides the placement cell Y from the terrain height (ADR 0047)
    /// </summary>
    public static class PlacementGroundCellResolver
    {
        // 浮動小数点誤差の余裕。y≈1000から約1000m落とすRaycastのfloat刻み（約6e-5m）の10倍以上をとる
        // Floating-point margin; over ten times the float ulp (~6e-5 m) of a ~1000 m raycast from y≈1000
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

        // 地形最高点を含むセルを返す。格子のある地形では整数のつもりの地表（鉱脈パッド）が「格子1段＋採掘底面クリアランス」まで整数を下回って格納されるため、それに浮動小数余裕を足した分を整数へ引き上げる
        // Returns the cell containing the terrain max; on a lattice terrain an integer-intended surface (vein pad) is stored up to one step plus the mining-bottom clearance below, so that plus the float margin is lifted
        internal static int ResolveCellY(float groundMaxHeight, float heightQuantizationStep, int heightOffset)
        {
            var integerGroundTolerance = QuantizedPadUndershoot(heightQuantizationStep) + FloatingPointGroundTolerance;
            return Mathf.FloorToInt(groundMaxHeight + integerGroundTolerance) + heightOffset;
        }

        // 生成側のPadHeightが整数から下げうる最大量。格子の無い地面（step=0）にはパッドが無いので0
        // The most generation's PadHeight can sink a pad below the integer; ground without a lattice (step 0) has no pads, so 0
        private static float QuantizedPadUndershoot(float heightQuantizationStep)
        {
            if (heightQuantizationStep <= 0f) return 0f;
            return heightQuantizationStep + (float)TerrainHeightStorage.MiningBottomClearanceMeters;
        }
    }
}
