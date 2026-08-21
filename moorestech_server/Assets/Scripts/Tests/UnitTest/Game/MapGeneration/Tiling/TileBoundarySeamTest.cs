using System.Collections.Generic;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Tiling
{
    // パディング窓生成→中央クロップ→タイルループの全経路を通した出力で、隣接タイルの境界が一致することを検証する（R2）。
    // ジョブ単体の座標基準は WorldOffsetSlopeSeamTest が見るので、ここは VanillaGenerator の最終出力だけを突き合わせる。
    // Verifies adjacent tiles agree at their shared border on the full path: padded window, center crop, then the tile loop (R2).
    // WorldOffsetSlopeSeamTest covers the per-job coordinate basis, so this one compares only VanillaGenerator's final output.
    //
    // 本テストが保証するのは「クロップ機構と padding 導出が正しいこと」であり「production にシームが無いこと」ではない。
    // SmallSeaRemoval と Alpine 台地の連結成分は到達が無制限で padding では直せず、全設定で無効化してある（bd moorestech-edd.8）。
    // This proves the crop mechanism and the padding derivation are correct, not that production is seam-free.
    // SmallSeaRemoval's and the alpine plateau's connected components have unbounded reach no padding can fix, and stay disabled here (bd moorestech-edd.8).
    public class TileBoundarySeamTest
    {
        private const int GridSide = 3;
        private const int Seed = 42;

        // factory既定の biomeBlendRadius=200 は導出paddingを301まで押し上げ、解像度129に対し窓が 731² になって3タイル格子が実用外の遅さになる。
        // 小さくしても導出paddingが chunkPadding 32 を下回るだけで、測りたいクロップ機構の被覆は落ちない。
        // The factory default biomeBlendRadius=200 pushes the derived padding to 301, a 731 window over a 129 resolution that makes a 3x3 grid impractically slow.
        // Shrinking it only drops the derived padding below chunkPadding 32 and costs none of the crop coverage this test is about.
        private const int ChunkPadding = 32;
        private const int BiomeBlendRadius = 8;

        // 海岸テストだけが使う陸側地形半径。BeachTransitionJob.CoastalSmoothRadius(60) = 120 で ChunkPadding 32 を超える
        // The land-side terrain radius only the coastal test uses; BeachTransitionJob.CoastalSmoothRadius(60) = 120 exceeds ChunkPadding 32
        private const int CoastalBeachLandTerrainRadius = 60;
        private const float CoastalLandThreshold = 0.35f;

        // 高さは0..1正規化。terrainHeight=600m 換算で 1e-4 は 6cm 未満であり、クロップ漏れのシームは必ずこれより大きい。
        // Heights are normalized 0..1; at terrainHeight=600m, 1e-4 is under 6cm, well below any seam a missing crop would leave.
        private const float HeightTolerance = 1e-4f;

        [Test]
        public void 隣接タイルの境界の高さとバイオームは一致する()
        {
            AssertNoSeamAcrossGrid(BuildConfig(desertEnabled: false));
        }

        // 裁定A7: HeightSlopeJob（desert の canyon スロープ）をタイルループ全体で実際に踏ませる。
        // 既定のテスト設定は desert/mesa/jungle/alpine を全て false にするため、明示しないとこの経路はゼロカバーになる。
        // Ruling A7: actually exercise HeightSlopeJob (the desert canyon slope) through the whole tile loop.
        // The default test setup disables desert/mesa/jungle/alpine, so this path has zero coverage unless it is stated here.
        [Test]
        public void 砂漠スロープ有効でも隣接タイルの境界の高さとバイオームは一致する()
        {
            AssertNoSeamAcrossGrid(BuildConfig(desertEnabled: true));
        }

        // 上2件はどちらも海岸系の到達が chunkPadding に収まる設定なので、padding 導出の欠陥をゼロカバーで見逃す。
        // ここだけ coastalSmoothFactor の到達（max(2r, r+12) = 120px）を chunkPadding 32 より大きくして実際に踏ませる。
        // Both tests above keep the shore reach inside chunkPadding, so a broken padding derivation goes uncovered.
        // This one alone pushes coastalSmoothFactor's reach (max(2r, r+12) = 120px) past chunkPadding 32 to actually exercise it.
        [Test]
        public void 海岸平滑の到達がchunkPaddingを超えても隣接タイルの境界は一致する()
        {
            var config = BuildCoastalConfig();
            var output = AssertNoSeamAcrossGrid(config);

            // 砂浜が一画素も出ていなければ海岸系の経路を踏んでおらず、上の突き合わせは空振りになる
            // With no beach pixel at all the shore path never ran and the comparison above proved nothing
            Assert.Less(0, CountBorderBeachSamples(output, config.Resolution),
                "タイル境界に砂浜が一画素も出ておらず、海岸系チャネルを踏んでいない");
        }

        // SmallSeaRemovalJob と AlpinePlateauStage の連結成分は到達が無制限で padding では直せない別問題（bd moorestech-edd.8）。
        // 本テストが測りたい海岸系の到達だけが残るよう、その2つは無効化してから海岸の帯を広げる。
        // SmallSeaRemovalJob and AlpinePlateauStage use connected components, whose unbounded reach no padding can fix (bd moorestech-edd.8).
        // Both are disabled so that only the shore reach this test is about remains, and then the beach band is widened.
        private static TerrainGenerationConfig BuildCoastalConfig()
        {
            var config = BuildConfig(desertEnabled: false);
            config.alpineEnabled = false;
            config.shoreConfig.minSeaRegionSize = 0;

            // 共通設定は landThreshold=0 で全面陸になり海岸が1画素も出ないため、海を作る閾値へ戻す
            // The shared setup uses landThreshold=0 and comes out all land with no coast at all, so restore a threshold that makes sea
            config.landThreshold = CoastalLandThreshold;
            config.shoreConfig.beachLandTerrainRadius = CoastalBeachLandTerrainRadius;
            return config;
        }

        private static int CountBorderBeachSamples(MapGenerationOutput output, int resolution)
        {
            var beach = (byte)BiomeType.Beach;
            var count = 0;
            foreach (var tile in output.Tiles)
            for (var i = 0; i < resolution; i++)
            {
                if (tile.BiomeIndices[i] == beach) count++;
                if (tile.BiomeIndices[(resolution - 1) * resolution + i] == beach) count++;
                if (tile.BiomeIndices[i * resolution] == beach) count++;
                if (tile.BiomeIndices[i * resolution + resolution - 1] == beach) count++;
            }

            return count;
        }

        private static TerrainGenerationConfig BuildConfig(bool desertEnabled)
        {
            var config = MultiTileTestWorld.BuildConfig(GridSide, Seed);
            config.chunkPadding = ChunkPadding;
            config.biomeBlendRadius = BiomeBlendRadius;
            config.desertEnabled = desertEnabled;

            if (desertEnabled)
            {
                // v8マスタの実運用値（裁定A7の背景）。HeightmapStage のスロープゲートを確実に通す組み合わせ
                // The v8 master's production values (ruling A7's background): the combination that clears HeightmapStage's slope gate
                config.desert.canyonOctaves = 4;
                config.desert.duneAmplitude = 0.025f;
                config.desert.absSmoothing = 0.25f;
            }

            return config;
        }

        private static MapGenerationOutput AssertNoSeamAcrossGrid(TerrainGenerationConfig config)
        {
            var output = new VanillaGenerator().Generate(config);
            Assert.AreEqual(GridSide * GridSide, output.Tiles.Count);

            var tilesByIndex = new Dictionary<Vector2Int, TerrainTileOutput>();
            var distinctBiomes = new HashSet<byte>();
            foreach (var tile in output.Tiles)
            {
                tilesByIndex.Add(new Vector2Int(tile.TileX, tile.TileZ), tile);
                foreach (var biomeIndex in tile.BiomeIndices) distinctBiomes.Add(biomeIndex);
            }

            // 全画素が同一バイオームだとバイオーム側の突き合わせが空になるので、2種以上出ていることを先に固定する
            // A single biome everywhere would make the biome comparison vacuous, so pin that at least two of them appear
            Assert.Less(1, distinctBiomes.Count, "バイオームが1種しか出ておらず境界の比較が空になる");

            // 格子の内部境界を全て見る。1組だけだと特定のタイルでしか起きない取りこぼしを見逃す
            // Walks every interior border of the grid; a single pair would miss a slip that only happens on some tiles
            //
            // height と biome を別リストへ収集してから最後にまとめて判定する。Assert.AreEqual を都度呼ぶと
            // 最初の height 不一致で例外が飛び、同じ画素の biome 判定が一度も実行されないまま終わる
            // Height and biome mismatches are collected into separate lists and judged at the end; calling
            // Assert.AreEqual per pixel would throw on the first height miss and never even run the biome check for it
            var resolution = config.Resolution;
            var heightMismatches = new List<string>();
            var biomeMismatches = new List<string>();
            for (var z = 0; z < GridSide; z++)
            for (var x = 0; x < GridSide; x++)
            {
                var tile = tilesByIndex[new Vector2Int(x, z)];
                if (x + 1 < GridSide) CollectColumnBorderMismatches(tile, tilesByIndex[new Vector2Int(x + 1, z)], resolution, heightMismatches, biomeMismatches);
                if (z + 1 < GridSide) CollectRowBorderMismatches(tile, tilesByIndex[new Vector2Int(x, z + 1)], resolution, heightMismatches, biomeMismatches);
            }

            if (0 < heightMismatches.Count || 0 < biomeMismatches.Count)
            {
                var heightSample = 0 < heightMismatches.Count ? heightMismatches[0] : "(none)";
                var biomeSample = 0 < biomeMismatches.Count ? biomeMismatches[0] : "(none)";
                Assert.Fail($"height seam count={heightMismatches.Count} sample={heightSample}\nbiome seam count={biomeMismatches.Count} sample={biomeSample}");
            }

            return output;
        }

        // 左タイルの最右列と右タイルの最左列は同一ワールドXをサンプルする
        // The left tile's rightmost column and the right tile's leftmost one sample the same world X
        private static void CollectColumnBorderMismatches(
            TerrainTileOutput left, TerrainTileOutput right, int resolution, List<string> heightMismatches, List<string> biomeMismatches)
        {
            for (var z = 0; z < resolution; z++)
                CollectSampleMismatch(left, right, z * resolution + (resolution - 1), z * resolution, $"z={z}", heightMismatches, biomeMismatches);
        }

        // 手前タイルの最奥行と奥タイルの最手前行は同一ワールドZをサンプルする
        // The near tile's farthest row and the far tile's nearest one sample the same world Z
        private static void CollectRowBorderMismatches(
            TerrainTileOutput near, TerrainTileOutput far, int resolution, List<string> heightMismatches, List<string> biomeMismatches)
        {
            for (var x = 0; x < resolution; x++)
                CollectSampleMismatch(near, far, (resolution - 1) * resolution + x, x, $"x={x}", heightMismatches, biomeMismatches);
        }

        private static void CollectSampleMismatch(
            TerrainTileOutput near, TerrainTileOutput far, int nearIndex, int farIndex, string borderPosition,
            List<string> heightMismatches, List<string> biomeMismatches)
        {
            var location = $"({near.TileX},{near.TileZ})->({far.TileX},{far.TileZ}) {borderPosition}";
            if (HeightTolerance < Mathf.Abs(near.Heights[nearIndex] - far.Heights[farIndex]))
                heightMismatches.Add($"{location}: {near.Heights[nearIndex]} vs {far.Heights[farIndex]}");
            if (near.BiomeIndices[nearIndex] != far.BiomeIndices[farIndex])
                biomeMismatches.Add($"{location}: {near.BiomeIndices[nearIndex]} vs {far.BiomeIndices[farIndex]}");
        }
    }
}
