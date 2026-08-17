using Client.Game.InGame.Environment.Terrain.Build.Placement;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround
{
    /// <summary>
    ///     クラスタに属さない岩1本の裸地。半径も減衰もクラスタ経路とは別式で、傾斜バイアスもフットプリントも使わない。
    ///     移植元 TerrainGenerator.cs:1666-1707 の逐語移植
    ///     The bare patch of a rock outside any cluster; its radius and falloff follow a different formula from the
    ///     cluster path and use neither slope bias nor footprints. A verbatim port of the source's TerrainGenerator.cs:1666-1707
    /// </summary>
    public static class SurroundSingleRockPainter
    {
        public static void Paint(
            float[,,] alphamap, TerrainGenerationConfig config, SurroundTextureConfig surroundConfig,
            int layerIndex, TileLocalMapObject stoneObject, Vector3 tileWorldPosition)
        {
            var alphaResolution = alphamap.GetLength(0);

            // ノイズをピクセル座標で引く移植元の式は地形1枚が前提。タイル番号ぶんずらして5x5でも模様を連続させる
            // The source sampled the noise in pixel coordinates on a single terrain; the tile index shifts it so the pattern carries across the 5x5 grid
            var pixelOriginX = Mathf.RoundToInt(tileWorldPosition.x / config.terrainWidth) * (alphaResolution - 1);
            var pixelOriginZ = Mathf.RoundToInt(tileWorldPosition.z / config.terrainLength) * (alphaResolution - 1);

            var normalizedX = stoneObject.LocalPosition.x / config.terrainWidth;
            var normalizedZ = stoneObject.LocalPosition.z / config.terrainLength;

            // 半径はピクセル単位。クラスタ経路と違いScaleを見ず、singleRockRadiusだけで決まる
            // The radius is in pixels; unlike the cluster path it ignores Scale and comes from singleRockRadius alone
            var radiusInPixels = surroundConfig.singleRockRadius / config.terrainWidth * alphaResolution;
            var centerX = Mathf.RoundToInt(normalizedX * (alphaResolution - 1));
            var centerZ = Mathf.RoundToInt(normalizedZ * (alphaResolution - 1));
            var scanRadius = Mathf.CeilToInt(radiusInPixels);

            for (var offsetZ = -scanRadius; offsetZ <= scanRadius; offsetZ++)
            for (var offsetX = -scanRadius; offsetX <= scanRadius; offsetX++)
            {
                var pixelX = centerX + offsetX;
                var pixelZ = centerZ + offsetZ;
                if (pixelX < 0 || alphaResolution <= pixelX || pixelZ < 0 || alphaResolution <= pixelZ) continue;

                var distance = Mathf.Sqrt(offsetX * offsetX + offsetZ * offsetZ);
                if (radiusInPixels < distance) continue;

                // ノイズはワールド座標ではなくピクセル座標で引く（移植元のまま）。減衰は距離の2乗で効く
                // The noise samples pixel coordinates rather than world ones, as in the source; the falloff is squared
                var falloff = 1f - distance / radiusInPixels;
                var noise = Mathf.PerlinNoise(
                    (pixelOriginX + pixelX) * surroundConfig.noiseHighFrequency,
                    (pixelOriginZ + pixelZ) * surroundConfig.noiseHighFrequency);
                var blend = falloff * falloff * surroundConfig.singleRockBlend * (0.5f + noise);

                SurroundBlendWriter.Blend(alphamap, pixelZ, pixelX, layerIndex, blend);
            }
        }
    }
}
