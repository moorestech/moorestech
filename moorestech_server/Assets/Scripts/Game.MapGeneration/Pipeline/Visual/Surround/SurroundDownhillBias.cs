using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Surround
{
    /// <summary>
    ///     岩から見て下り側のピクセルほど距離を圧縮して返す倍率。裸地が斜面の下へ流れて見える理由がこれ。
    ///     移植元 TerrainGenerator.cs:1747-1789 の逐語移植で、heights は摂動前の転送高さを [z,x] で受ける
    ///     The factor compressing distance for pixels downhill of a rock, which is why bare ground appears to run
    ///     down a slope; a verbatim port of the source's TerrainGenerator.cs:1747-1789 taking pre-tree heights as [z,x]
    /// </summary>
    public static class SurroundDownhillBias
    {
        public static float Compute(
            TerrainGenerationConfig config, float[,] heights, int heightResolution, int heightX, int heightZ,
            float worldX, float worldZ, IReadOnlyList<(float x, float z, float radius)> footprints)
        {
            // 地表の傾斜方向を前進差分で採る
            // Take the surface gradient with a forward difference
            var height = GetHeightSafe(heightX, heightZ);
            var heightRight = GetHeightSafe(heightX + 1, heightZ);
            var heightUp = GetHeightSafe(heightX, heightZ + 1);
            var slopeX = (heightRight - height) * config.terrainHeight;
            var slopeZ = (heightUp - height) * config.terrainHeight;

            // 最近接フットプリントから見た方向。0除算除けの0.001は移植元の値のまま
            // The direction seen from the nearest footprint; the 0.001 guarding the divide is the source's value
            var nearestSquaredDistance = float.MaxValue;
            var directionX = 0f;
            var directionZ = 0f;
            foreach (var (footprintX, footprintZ, _) in footprints)
            {
                var deltaX = worldX - footprintX;
                var deltaZ = worldZ - footprintZ;
                var squaredDistance = deltaX * deltaX + deltaZ * deltaZ;
                if (nearestSquaredDistance <= squaredDistance) continue;

                nearestSquaredDistance = squaredDistance;
                var length = Mathf.Sqrt(squaredDistance) + 0.001f;
                directionX = deltaX / length;
                directionZ = deltaZ / length;
            }

            // 岩からの方向と下り方向の一致度を内積で採り、一致するほど1.0→1.5倍まで距離を圧縮する
            // The dot product of the from-rock direction and the downhill one compresses distance up to 1.5x as they align
            var slopeLength = Mathf.Sqrt(slopeX * slopeX + slopeZ * slopeZ) + 0.001f;
            var downhillX = -slopeX / slopeLength;
            var downhillZ = -slopeZ / slopeLength;
            var alignment = directionX * downhillX + directionZ * downhillZ;

            return 1f + Mathf.Clamp01(alignment) * 0.5f;

            #region Internal

            float GetHeightSafe(int x, int z)
            {
                x = Mathf.Clamp(x, 0, heightResolution - 1);
                z = Mathf.Clamp(z, 0, heightResolution - 1);
                return heights[z, x];
            }

            #endregion
        }
    }
}
