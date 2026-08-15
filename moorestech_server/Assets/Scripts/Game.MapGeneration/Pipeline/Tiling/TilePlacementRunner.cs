using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Stages;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Tiling
{
    // タイル1枚ぶんの配置(木・オブジェクト・鉱脈)を走らせ、結果をシーン座標で output へ積み上げる。
    // 格子全体で不変な入力はコンストラクタで受け、タイルごとに変わるものだけ Run の引数にする。
    // Runs one tile's placement (trees, objects, veins) and accumulates the results into output in scene space.
    // Inputs constant across the grid arrive via the constructor; only per-tile inputs are Run arguments.
    public class TilePlacementRunner
    {
        private readonly BiomePlacementHelper _helper;
        private readonly BiomeType[] _biomeTypes;
        private readonly Vector2 _spawnOffset;
        private readonly Vector3 _sceneSpawn;
        private readonly MapGenerationOutput _output;

        public TilePlacementRunner(
            BiomePlacementHelper helper, BiomeType[] biomeTypes,
            Vector2 spawnOffset, Vector3 sceneSpawn, MapGenerationOutput output)
        {
            _helper = helper;
            _biomeTypes = biomeTypes;
            _spawnOffset = spawnOffset;
            _sceneSpawn = sceneSpawn;
            _output = output;
        }

        // buffers は PaddedWindowStage がクロップ済みの分類で、ここで分類を回し直すと転送する分類と境界で食い違う。
        // 戻り値はこのタイルの biomeIndices（heights と同じくクロップ済み分類から作る）。
        // buffers carry PaddedWindowStage's cropped classification; re-running it here would disagree with the transferred one at the borders.
        // The return value is this tile's biomeIndices, built from that same cropped classification.
        public byte[] Run(
            TerrainGenerationConfig tileConfig, JobBuffers buffers, float[] heights, Vector2 tileScene)
        {
            var res = tileConfig.Resolution;
            var biomeCount = _biomeTypes.Length;
            var weights2D = PlacementInputBuilder.BuildPlacementWeights(
                buffers.biomeWeights, buffers.shoreMask, buffers.beachFactor, res, biomeCount, biomeCount + 2);
            var masks = BiomeMaskBuilder.BuildAllWinnerMasks(weights2D, res, biomeCount);
            var heights2D = PlacementInputBuilder.ConvertHeights(heights, res);

            var treeEntries = new List<PlacementEntry>();
            TreePlacementStage.Generate(tileConfig, _helper, _biomeTypes, masks, heights, treeEntries);

            var objectEntries = new List<PlacementEntry>();
            List<PlacedVein> itemVeins = null;
            List<PlacedVein> fluidVeins = null;
            PlaceObjectsAndVeinsInNoiseSpace();

            // 木はタイルローカル座標なのでタイルの設置位置ぶん進め、ノイズ座標の残りは -G でシーン座標へ揃える。
            // Trees are tile-local and advance by the tile's placement position; the rest are noise-space and realign by -G.
            PlacementSceneOffset.ToTileScene(treeEntries, tileScene);
            PlacementSceneOffset.ToSceneSpace(objectEntries, _spawnOffset);
            PlacementSceneOffset.ToSceneSpace(itemVeins, _spawnOffset);
            PlacementSceneOffset.ToSceneSpace(fluidVeins, _spawnOffset);

            // 安全域はシーン座標で判定する。ループ中は output.SpawnPoint が未確定なので採取済みのXZを使う。
            // Clearance is judged in scene space; output.SpawnPoint is unsettled mid-loop, so use the pre-sampled XZ.
            SpawnPlacementExclusionStage.RemoveInsideSpawnClearance(treeEntries, _sceneSpawn);
            SpawnPlacementExclusionStage.RemoveInsideSpawnClearance(objectEntries, _sceneSpawn);

            // 全タイルぶんを1本のリストへ積む。代入にすると最後のタイルの配置物しか残らない。
            // Every tile appends to one list; assigning would keep only the last tile's placements.
            AppendMapObjects(treeEntries);
            AppendMapObjects(objectEntries);
            _output.ItemVeins.AddRange(itemVeins);
            _output.FluidVeins.AddRange(fluidVeins);

            return PlacementInputBuilder.BuildBiomeIndices(
                buffers.winnerBiomeIndex, buffers.landMask, buffers.beachFactor, _biomeTypes, res * res);

            #region Internal

            // objectPlacements はノイズ座標のままシーン座標化した objectEntries と混ざるため、消費者ごとこの中へ閉じ込める。
            // objectPlacements stays in noise space and would be confused with the scene-space objectEntries, so its consumers live in here.
            void PlaceObjectsAndVeinsInNoiseSpace()
            {
                List<ObjectPlacementResult> objectPlacements = null;
                if (tileConfig.generateObject)
                    ObjectPlacementStage.Generate(tileConfig, _helper, _biomeTypes, masks, heights, heights2D,
                        treeEntries, out objectEntries, out objectPlacements);

                itemVeins = OrePlacementStage.Generate(
                    tileConfig, masks, _biomeTypes, heights2D, treeEntries, objectPlacements);
                fluidVeins = FluidVeinPlacementStage.Generate(
                    tileConfig, masks, _biomeTypes, heights2D, treeEntries, objectPlacements, itemVeins);
            }

            #endregion
        }

        private void AppendMapObjects(List<PlacementEntry> entries)
        {
            if (entries == null) return;
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.MapObjectGuid)) continue;
                _output.MapObjects.Add(
                    new PlacedMapObject { MapObjectGuid = entry.MapObjectGuid, Position = entry.WorldPosition });
            }
        }
    }
}
