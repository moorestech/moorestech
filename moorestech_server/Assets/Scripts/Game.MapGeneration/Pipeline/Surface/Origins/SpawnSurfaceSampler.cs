using System;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Spawn;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface.Origins
{
    public static class SpawnSurfaceSampler
    {
        // 中心タイルのハイトマップからスポーン高さを採り、シーン座標のスポーン地点にする。
        // Samples the spawn height from the center tile's heightmap to complete the scene-space spawn point.
        public static Vector3 ComputeLegacy(TerrainGenerationConfig config, float[] centerTileHeights, Vector2 sceneSpawnXz)
        {
            Vector2 spawn = config.spawnWorldPosition;
            int res = config.Resolution;
            int px = Mathf.RoundToInt((spawn.x - config.worldOffsetX) / config.terrainWidth * (res - 1));
            int pz = Mathf.RoundToInt((spawn.y - config.worldOffsetZ) / config.terrainLength * (res - 1));

            // 格子外はスポーン座標が中心タイルの外という不整合。clamp で隅へ寄せると地形外スポーンのまま出荷される。
            // Off-lattice means the spawn lies outside the center tile; clamping to a corner would ship an off-terrain spawn.
            if (px < 0 || res <= px || pz < 0 || res <= pz)
                throw new InvalidOperationException(
                    $"[VanillaGenerator] spawnWorldPosition ({spawn.x}, {spawn.y}) is outside the center tile " +
                    $"[{config.worldOffsetX}, {config.worldOffsetX + config.terrainWidth}] x [{config.worldOffsetZ}, {config.worldOffsetZ + config.terrainLength}].");

            float heightMeters = centerTileHeights[pz * res + px] * config.terrainHeight;
            return new Vector3(sceneSpawnXz.x, heightMeters, sceneSpawnXz.y);
        }
    }
}
