using System;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Spawn;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Tiling;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Transfer;
using Unity.Collections;
using UnityEngine;

namespace Game.MapGeneration.Pipeline
{
    // VanillaGenerator アルゴリズムの本体。gridSizeX/Z の格子ぶんタイルを独立生成し、配置結果を1つの出力へまとめる。
    // The VanillaGenerator algorithm body: generates one independent tile per gridSizeX/Z cell into a single output.
    public class VanillaGenerator : IMapGenerator
    {
        public GenerationRun Generate(TerrainGenerationConfig sourceConfig)
        {
            // 探索結果を config へ書き戻すため作業コピーで通す。引数を汚すと同じ config での再実行が別地形になる。
            // Work on a copy since the search result is written back; mutating the argument would make a re-run differ.
            var config = sourceConfig.ShallowCopy();

            // 転送層のタイル並びは正方かつ1枚以上の格子前提。非正方は index と coord の対応が崩れ、0以下は
            // EnumerateTileCoordinates の完全平方判定を素通りしてチャンク0本のワイヤ値になる(TerrainTransferMeta参照)。
            // The transfer layer's tile order assumes a square, non-empty grid; a non-square one breaks the index-to-coord
            // mapping, and a non-positive one slips past EnumerateTileCoordinates' perfect-square check into a zero-chunk wire value (see TerrainTransferMeta).
            if (config.gridSizeX != config.gridSizeZ || config.gridSizeX <= 0)
                throw new InvalidOperationException(
                    $"[VanillaGenerator] gridSizeX ({config.gridSizeX}) and gridSizeZ ({config.gridSizeZ}) must be equal and positive.");

            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);

            // G はノイズのサンプル座標に効くため、全タイルより前にスポーン探索を1回だけ確定させる。
            // The spawn search settles once before every tile since G feeds the noise sample coordinates.
            RunSpawnSearch(config, biomeTypes);

            // シーン座標化の基準は探索の戻り値ではなく探索後の config から読む（探索無効時に master worldOffset を捨てないため）。
            // Read the noise-to-scene basis from the post-search config, not the search result, so a disabled search keeps the master worldOffset.
            var noiseToSceneShift = new Vector2(config.worldOffsetX, config.worldOffsetZ);

            int halfX = config.gridSizeX / 2;
            int halfZ = config.gridSizeZ / 2;
            var sceneOrigin = config.TileScenePosition(0, 0);

            // pass-2(見た目焼き)へ渡す配置台帳。結果出力ではなく GenerationRun の別枠で運ぶ。
            // The ledger handed to pass-2 (visual bake); it travels in its own GenerationRun slot, not in the result output.
            var ledger = new PlacementLedger();
            var output = new MapGenerationOutput
            {
                Resolution = config.Resolution,

                // クライアントは分類段を再実行するのでノイズ窓の原点が要り、地形の設置にはシーン原点が要る。
                // Clients re-run the classification stage, needing the noise window origin, and place the terrain at the scene origin.
                NoiseOrigin = noiseToSceneShift + sceneOrigin,
                SceneOrigin = sceneOrigin,
            };

            // スポーンのXZはタイル生成前に確定する（高さYだけ中心タイル生成後に採取する）。
            // Spawn XZ settles before tile generation; only its height Y is sampled after the center tile.
            Vector2 sceneSpawnXz = ComputeSceneSpawnXz(config, noiseToSceneShift);
            // halo の到達距離は格子で1つ。全バイオーム・全エントリの距離制約の最大値をここで1回だけ決める。
            // One halo reach for the whole grid, settled once here as the maximum distance constraint across every biome and entry.
            var helper = new BiomePlacementHelper(config);
            var halo = new PlacementHaloStore(PlacementHaloRadius.Resolve(config, biomeTypes, helper));
            var runner = new TilePlacementRunner(helper, biomeTypes,
                noiseToSceneShift, new Vector3(sceneSpawnXz.x, 0f, sceneSpawnXz.y), output, halo, ledger);

            // タイル窓の基準はindex(0,0)タイル。config.worldOffset は中心タイル基準なのでそのままでは基準にできない。
            // The tile windows are based on the index (0,0) tile; config.worldOffset is center-tile based and cannot serve as one.
            var gridConfig = config.ShallowCopy();
            gridConfig.worldOffsetX = output.NoiseOrigin.x;
            gridConfig.worldOffsetZ = output.NoiseOrigin.y;

            // biomeParams と noiseOffsets が1タイルでも違うとそのタイルだけ別地形になるため、格子で1組だけ作る。
            // A single differing biomeParams or noiseOffsets would give that tile a different world, so the grid shares one set.
            var biomeParams = JobDataConverter.ConvertBiomeParams(config, biomeTypes, Allocator.TempJob);
            var noiseOffsets = JobDataConverter.GenerateNoiseOffsets(config, biomeParams, biomeTypes, Allocator.TempJob);
            float[] centerTileHeights = null;
            try
            {
                foreach (var (tileX, tileZ) in TerrainTransferMeta.EnumerateTileCoordinates(config.gridSizeX * config.gridSizeZ))
                {
                    var tile = GenerateTile(tileX, tileZ);
                    output.Tiles.Add(tile);
                    if (tileX == halfX && tileZ == halfZ) centerTileHeights = tile.Heights;
                }
            }
            finally
            {
                noiseOffsets.Dispose();
                biomeParams.Dispose();
            }

            output.SpawnPoint = ComputeSpawn(config, centerTileHeights, sceneSpawnXz);

            // 返す config は探索結果を書き戻した作業コピー。pass-2 が入力側を読むと探索前のスポーン座標を掴む。
            // The returned config is the working copy carrying the search write-back; reading the input side would hand pass-2 the pre-search spawn position.
            return new GenerationRun(output, ledger, config);

            #region Internal

            // タイル1枚を生成する。窓側の巨大バッファは PaddedWindowStage の内部で確保・破棄される。
            // Generates one tile; the large padded-window buffers are allocated and freed inside PaddedWindowStage.
            TerrainTileOutput GenerateTile(int tileX, int tileZ)
            {
                var tileConfig = gridConfig.CreateTileConfig(tileX, tileZ);

                // クロップされないチャネル(rawBiomeIndex等)に前タイルの値を残さないよう、タイル毎に確保する。
                // Allocate per tile so the non-cropped channels (rawBiomeIndex, ...) cannot retain the previous tile's values.
                var buffers = JobDataConverter.AllocateBuffers(config.Resolution, biomeTypes.Length, 1, Allocator.TempJob);
                buffers.biomeParams = biomeParams;
                buffers.noiseOffsets = noiseOffsets;
                try
                {
                    PaddedWindowStage.Run(tileConfig, biomeTypes, buffers);
                    var heights = buffers.heights.ToArray();
                    var tileScene = config.TileScenePosition(tileX, tileZ);
                    runner.Run(tileConfig, buffers, heights, tileScene, tileX, tileZ);
                    return new TerrainTileOutput { TileX = tileX, TileZ = tileZ, Heights = heights };
                }
                finally
                {
                    // 共有した2本を切り離してから破棄する。付けたままだと次タイルが解放済みの配列を読む。
                    // Detach the two shared arrays before disposing, otherwise the next tile would read freed arrays.
                    buffers.biomeParams = default;
                    buffers.noiseOffsets = default;
                    buffers.Dispose();
                }
            }

            #endregion
        }

        // スポーン探索を走らせ、中央化オフセット G を config のノイズ座標へ書き込む（結果の唯一の置き場が config）。
        // Run the spawn search and write the centering offset G into the config's noise coordinates, config being the sole home of the result.
        static void RunSpawnSearch(TerrainGenerationConfig config, BiomeType[] biomeTypes)
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
        static Vector2 ComputeSceneSpawnXz(TerrainGenerationConfig config, Vector2 noiseToSceneShift)
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

        // 中心タイルのハイトマップからスポーン高さを採り、シーン座標のスポーン地点にする。
        // Samples the spawn height from the center tile's heightmap to complete the scene-space spawn point.
        static Vector3 ComputeSpawn(TerrainGenerationConfig config, float[] centerTileHeights, Vector2 sceneSpawnXz)
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
