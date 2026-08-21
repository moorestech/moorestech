using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Generators.Util;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Stages;
using Game.MapGeneration.Pipeline.Visual.Placement;
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
        private readonly Vector2 _noiseToSceneShift;
        private readonly Vector3 _sceneSpawn;
        private readonly MapGenerationOutput _output;

        // pass-2(見た目)へ渡す配置台帳。生成システムの外へは出ない
        // The placement ledger handed to pass-2 (visuals); it never leaves the generation system
        private readonly PlacementLedger _ledger;

        // 格子で1つの halo 帳面。タイルを順に回す間、確定済みの配置を持ち回して次のタイルの近傍判定へ渡す。
        // One halo ledger for the whole grid, carried through the tile loop so confirmed placements reach the next tile's neighbour tests.
        private readonly PlacementHaloStore _halo;

        // クラスタIDはタイルごとに0から採番されるため、書き出し済みタイルの最大値+1を積み上げて格子全体で一意化する。
        // Cluster ids restart at 0 per tile, so accumulate the max written so far + 1 to uniquify them across the whole grid.
        private int _nextClusterIdOffset;

        public TilePlacementRunner(
            BiomePlacementHelper helper, BiomeType[] biomeTypes,
            Vector2 noiseToSceneShift, Vector3 sceneSpawn, MapGenerationOutput output,
            PlacementHaloStore halo, PlacementLedger ledger)
        {
            _helper = helper;
            _biomeTypes = biomeTypes;
            _noiseToSceneShift = noiseToSceneShift;
            _sceneSpawn = sceneSpawn;
            _output = output;
            _halo = halo;
            _ledger = ledger;
        }

        // buffers は PaddedWindowStage がクロップ済みの分類で、ここで分類を回し直すと転送する分類と境界で食い違う。
        // 戻り値はこのタイルの biomeIndices（heights と同じくクロップ済み分類から作る）。
        // buffers carry PaddedWindowStage's cropped classification; re-running it here would disagree with the transferred one at the borders.
        // The return value is this tile's biomeIndices, built from that same cropped classification.
        public byte[] Run(
            TerrainGenerationConfig tileConfig, JobBuffers buffers, float[] heights, Vector2 tileScene,
            int tileIndexX, int tileIndexZ)
        {
            var tile = new TilePlacementContext(tileIndexX, tileIndexZ, _halo);
            var res = tileConfig.Resolution;
            var biomeCount = _biomeTypes.Length;
            var weights2D = PlacementInputBuilder.BuildPlacementWeights(
                buffers.biomeWeights, buffers.shoreMask, buffers.beachFactor, res, biomeCount, biomeCount + 2);
            var masks = BiomeMaskBuilder.BuildAllWinnerMasks(weights2D, res, biomeCount);
            var heights2D = PlacementInputBuilder.ConvertHeights(heights, res);

            var treeEntries = new List<PlacementEntry>();
            TreePlacementStage.Generate(tileConfig, _helper, _biomeTypes, masks, heights, treeEntries, tile);

            var objectEntries = new List<PlacementEntry>();
            List<PlacedVein> itemVeins = null;
            List<PlacedVein> fluidVeins = null;
            PlaceObjectsAndVeinsInNoiseSpace();

            // 木はタイルローカル座標なのでタイルの設置位置ぶん進め、ノイズ座標の残りは窓原点ぶん引いてシーン座標へ揃える。
            // Trees are tile-local and advance by the tile's placement position; the rest are noise-space and realign by the window origin.
            PlacementSceneOffset.ToTileScene(treeEntries, tileScene);
            PlacementSceneOffset.ToSceneSpace(objectEntries, _noiseToSceneShift);
            PlacementSceneOffset.ToSceneSpace(itemVeins, _noiseToSceneShift);
            PlacementSceneOffset.ToSceneSpace(fluidVeins, _noiseToSceneShift);

            // 安全域はシーン座標で判定する。ループ中は output.SpawnPoint が未確定なので採取済みのXZを使う。
            // Clearance is judged in scene space; output.SpawnPoint is unsettled mid-loop, so use the pre-sampled XZ.
            SpawnPlacementExclusionStage.RemoveInsideSpawnClearance(treeEntries, _sceneSpawn);
            SpawnPlacementExclusionStage.RemoveInsideSpawnClearance(objectEntries, _sceneSpawn);

            // 安全域で消えた配置は halo に残さない。残すと存在しない木が隣タイルの候補を弾く。
            // シーン座標は world - 窓原点なので、足し戻してワールドへ揃える。
            // Placements deleted by the clearance never enter the halo, otherwise a tree that does not exist would reject the neighbouring tile's candidates.
            // Scene space is world minus the window origin, so adding it back returns to world space.
            _halo.Trees.AddPlacements(treeEntries, _noiseToSceneShift.x, _noiseToSceneShift.y);
            _halo.Objects.AddPlacements(objectEntries, _noiseToSceneShift.x, _noiseToSceneShift.y);

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
                        treeEntries, tile, out objectEntries, out objectPlacements);

                itemVeins = OrePlacementStage.Generate(
                    tileConfig, masks, _biomeTypes, heights2D, treeEntries, objectPlacements, tile);
                fluidVeins = FluidVeinPlacementStage.Generate(
                    tileConfig, masks, _biomeTypes, heights2D, treeEntries, objectPlacements, itemVeins, tile);
            }

            void AppendMapObjects(List<PlacementEntry> entries)
            {
                if (entries == null) return;

                // このタイルの書き出し開始時点のオフセットを固定して使う。木呼び出しはクラスタを持たないため素通りする。
                // Freeze the offset as of this tile's write start; the tree call carries no cluster so it passes through untouched.
                var offset = _nextClusterIdOffset;
                var maxLocalClusterId = -1;

                foreach (var entry in entries)
                {
                    if (string.IsNullOrEmpty(entry.MapObjectGuid)) continue;

                    // 独立配置は Cluster を -1 の空情報で持つため、オフセットを掛けると隣タイルの実クラスタIDへ化ける。
                    // An independent placement carries an empty -1 Cluster, so offsetting it would morph into a neighbouring tile's real id.
                    var hasCluster = entry.Cluster.HasValue && 0 <= entry.Cluster.Value.ClusterId;
                    var clusterId = hasCluster ? entry.Cluster.Value.ClusterId + offset : -1;
                    var clusterCenter = hasCluster
                        ? new Vector2(entry.Cluster.Value.Center.x, entry.Cluster.Value.Center.z)
                        : Vector2.zero;
                    if (hasCluster) maxLocalClusterId = Mathf.Max(maxLocalClusterId, entry.Cluster.Value.ClusterId);

                    _output.MapObjects.Add(new PlacedMapObject
                    {
                        MapObjectGuid = entry.MapObjectGuid,
                        Position = entry.WorldPosition,
                        Rotation = entry.Rotation,
                        Scale = entry.Scale,
                    });

                    _ledger.Add(new LedgerPlacement(entry.MapObjectGuid, entry.WorldPosition, entry.Rotation, entry.Scale,
                        entry.SurroundEffect, clusterId, clusterCenter));
                }

                if (0 <= maxLocalClusterId) _nextClusterIdOffset = offset + maxLocalClusterId + 1;
            }

            #endregion
        }
    }
}
