using System.Collections.Generic;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Tiling
{
    // パディング窓生成→中央クロップ→タイルループの全経路を通した出力で、隣接タイルの境界が一致することを検証する（R2）。
    // ジョブ単体の座標基準は WorldOffsetSlopeSeamTest が見るので、ここは VanillaGenerator の最終出力だけを突き合わせる。
    // Verifies adjacent tiles agree at their shared border on the full path: padded window, center crop, then the tile loop (R2).
    // WorldOffsetSlopeSeamTest covers the per-job coordinate basis, so this one compares only VanillaGenerator's final output.
    public class TileBoundarySeamTest
    {
        private const int GridSide = 3;
        private const int Seed = 42;

        // 局所カーネル（重み補間 blendRadius・ブラー blendRadius/divisor・ビーチ半径16px）が窓の外を読まない値にする。
        // padding = max(chunkPadding, blendRadius/2) なので実効パディングは chunkPadding 側で決まる。
        // Sized so no local kernel (weight interpolation blendRadius, blur blendRadius/divisor, 16px beach radius) reads past the window.
        // padding = max(chunkPadding, blendRadius/2), so chunkPadding is what settles the effective padding here.
        private const int ChunkPadding = 32;
        private const int BiomeBlendRadius = 8;

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

        private static void AssertNoSeamAcrossGrid(TerrainGenerationConfig config)
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
            var resolution = config.Resolution;
            for (var z = 0; z < GridSide; z++)
            for (var x = 0; x < GridSide; x++)
            {
                var tile = tilesByIndex[new Vector2Int(x, z)];
                if (x + 1 < GridSide) AssertColumnBorderMatches(tile, tilesByIndex[new Vector2Int(x + 1, z)], resolution);
                if (z + 1 < GridSide) AssertRowBorderMatches(tile, tilesByIndex[new Vector2Int(x, z + 1)], resolution);
            }
        }

        // 左タイルの最右列と右タイルの最左列は同一ワールドXをサンプルする
        // The left tile's rightmost column and the right tile's leftmost one sample the same world X
        private static void AssertColumnBorderMatches(TerrainTileOutput left, TerrainTileOutput right, int resolution)
        {
            for (var z = 0; z < resolution; z++)
                AssertSampleMatches(left, right, z * resolution + (resolution - 1), z * resolution, $"z={z}");
        }

        // 手前タイルの最奥行と奥タイルの最手前行は同一ワールドZをサンプルする
        // The near tile's farthest row and the far tile's nearest one sample the same world Z
        private static void AssertRowBorderMatches(TerrainTileOutput near, TerrainTileOutput far, int resolution)
        {
            for (var x = 0; x < resolution; x++)
                AssertSampleMatches(near, far, (resolution - 1) * resolution + x, x, $"x={x}");
        }

        private static void AssertSampleMatches(
            TerrainTileOutput near, TerrainTileOutput far, int nearIndex, int farIndex, string borderPosition)
        {
            var location = $"({near.TileX},{near.TileZ})->({far.TileX},{far.TileZ}) {borderPosition}";
            Assert.AreEqual(near.Heights[nearIndex], far.Heights[farIndex], HeightTolerance, $"height seam {location}");
            Assert.AreEqual(near.BiomeIndices[nearIndex], far.BiomeIndices[farIndex], $"biome seam {location}");
        }
    }
}
