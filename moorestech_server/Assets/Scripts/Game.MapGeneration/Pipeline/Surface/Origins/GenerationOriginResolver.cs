using System;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Spawn;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface.Origins
{
    public static class GenerationOriginResolver
    {
        // スポーン探索を走らせ、中央化オフセット G を config のノイズ座標へ書き込む（結果の唯一の置き場が config）。
        // Run the spawn search and write the centering offset G into the config's noise coordinates, config being the sole home of the result.
        public static void RunSpawnSearch(TerrainGenerationConfig config, BiomeType[] biomeTypes)
        {
            // 探索無効も1行残す。無効とフォールバックはどちらもオフセット0で、ログが無いと後から区別できない（ADR#13）
            // Log the disabled path too: disabled and fallback both yield a zero offset and become indistinguishable without it (ADR#13)
            if (!config.useSpawnOffsetSearch)
            {
                Debug.Log("[SpawnSearch] 探索無効（useSpawnOffsetSearch=false）");
                return;
            }

            var result = SpawnRegionFinder.Find(config, biomeTypes);

            // 成否と診断を必ず残す。候補ゼロならフォールバックして生成は続ける（spawn targetがタイル外だと SpawnRegionFinder / ComputeSpawn が落とす・別途裁定・ADR#13）
            // Always record the outcome and diagnostics: zero candidates fall back and generation continues (an off-tile spawn target still aborts in SpawnRegionFinder and ComputeSpawn, pending a separate ruling; ADR#13)
            Debug.Log($"[SpawnSearch] {(result.Success ? "成功" : "フォールバック")}\n{result.Diagnostics}");

            // 成功/失敗いずれも offset と spawn を必ず同期させる（片方だけ残ると鉱脈帯とスポーンがズレる）。
            // Always sync offset and spawn for both outcomes; a stale pair skews the vein bands against the spawn.
            // 探索は master の worldOffsetX を読まず絶対ノイズ空間で S を決めるため、G は加算ではなく上書き。
            // The search settles S in absolute noise space without reading the master worldOffsetX, so G replaces it instead of adding.
            config.spawnWorldPosition = result.SpawnWorldPosition;
            config.worldOffsetX = result.WorldOffset.x;
            config.worldOffsetZ = result.WorldOffset.y;
        }

        // スポーンのXZをシーン座標で返す。config.spawnWorldPosition はノイズ座標 S なので窓原点ぶん引く。
        // Return the spawn XZ in scene space; config.spawnWorldPosition is the noise-space S, so subtract the window origin.
        public static Vector2 ComputeSceneSpawnXz(TerrainGenerationConfig config, Vector2 noiseToSceneShift)
        {
            var sceneSpawn = config.spawnWorldPosition - noiseToSceneShift;

            // 全分岐で落下復帰先を中心タイルの開区間へ固定し、探索無効時だけ角スポーンが通る抜け道を残さない
            // Keep fall recovery inside the center tile's open interval in every branch, leaving no corner-spawn gap when search is disabled
            if (sceneSpawn.x <= 0f || config.terrainWidth <= sceneSpawn.x ||
                sceneSpawn.y <= 0f || config.terrainLength <= sceneSpawn.y)
                throw new InvalidOperationException(
                    $"[VanillaGenerator] scene spawn ({sceneSpawn.x}, {sceneSpawn.y}) is not inside the center tile " +
                    $"(0, {config.terrainWidth}) x (0, {config.terrainLength}).");

            return sceneSpawn;
        }

    }
}
