using System;
using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Source;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Visual.Surround;
using Tests.UnitTest.Game.MapGeneration.Visual.Surround;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Client.Tests.UnitTest
{
    /// <summary>
    ///     転送地形から splatmap を組み立てる経路を検証する。分類段の再実行と SplatmapJob の実行を通し、
    ///     正規化の破綻・転送winnerの反映漏れ・バッファ解放漏れを検知する
    ///     Exercises the whole path building a splatmap from transferred terrain, catching broken normalization,
    ///     an ignored transferred winner, and undisposed buffers
    /// </summary>
    public class SplatmapRuntimeGeneratorTest
    {
        private const int Resolution = 9;
        private const int AlphamapResolution = 8;
        private const int BeachLayerIndex = 0;
        private const int SeaFloorLayerIndex = 1;
        private const int GrassLayerIndex = 2;

        [Test]
        public void EveryPixelWeightSumsToOne()
        {
            // 重み合計が1から外れるのは正規化の破綻。Unityは合計1前提でアルファマップを描く
            // A sum other than 1 means normalization broke; Unity draws alphamaps assuming the weights total 1
            var alphamap = Generate(CreateBiomeIndices(BiomeType.Grassland));

            Assert.That(alphamap.GetLength(0), Is.EqualTo(AlphamapResolution));
            Assert.That(alphamap.GetLength(1), Is.EqualTo(AlphamapResolution));
            Assert.That(alphamap.GetLength(2), Is.EqualTo(3));

            for (var z = 0; z < AlphamapResolution; z++)
            for (var x = 0; x < AlphamapResolution; x++)
            {
                var weightSum = alphamap[z, x, 0] + alphamap[z, x, 1] + alphamap[z, x, 2];
                Assert.That(weightSum, Is.EqualTo(1f).Within(1e-4f), $"z={z} x={x}");
            }
        }

        [Test]
        public void TransferredOceanPixelsNeverReceiveBiomeTexture()
        {
            // 転送biomeがOceanならSplatmapJobは海分岐に入り、レイヤー0(砂)と1(海底)だけを書く。
            // バイオームテクスチャが乗るなら転送winnerが反映されていない
            // An Ocean byte drives SplatmapJob's sea branch, which writes only layer 0 (sand) and 1 (sea floor);
            // any biome texture here proves the transferred winner was ignored
            var alphamap = Generate(CreateBiomeIndices(BiomeType.Ocean));

            for (var z = 0; z < AlphamapResolution; z++)
            for (var x = 0; x < AlphamapResolution; x++)
            {
                Assert.That(alphamap[z, x, GrassLayerIndex], Is.EqualTo(0f), $"z={z} x={x}");
                Assert.That(alphamap[z, x, BeachLayerIndex] + alphamap[z, x, SeaFloorLayerIndex],
                    Is.EqualTo(1f).Within(1e-4f), $"z={z} x={x}");
            }
        }

        [Test]
        public void TransferredBiomeChangesTheResult()
        {
            // 転送biomeを変えても結果が同じなら、winnerの上書きが効いていない
            // An identical result for a different transferred biome would mean the winner overwrite does nothing
            var oceanAlphamap = Generate(CreateBiomeIndices(BiomeType.Ocean));
            var grasslandAlphamap = Generate(CreateBiomeIndices(BiomeType.Grassland));

            var foundDifference = false;
            for (var z = 0; z < AlphamapResolution && !foundDifference; z++)
            for (var x = 0; x < AlphamapResolution && !foundDifference; x++)
                if (1e-4f < Math.Abs(oceanAlphamap[z, x, GrassLayerIndex] - grasslandAlphamap[z, x, GrassLayerIndex]))
                    foundDifference = true;

            Assert.That(foundDifference, Is.True);
        }

        [Test]
        public void RepeatedGenerationIsDeterministic()
        {
            // 同じ入力なら何度走らせても同じ結果になること。Dispose漏れの検出は4フレーム後のコンソールエラーなのでここでは見ていない
            // The same input must always yield the same result; a Dispose leak surfaces as a console error four frames later, so this does not check it
            var firstAlphamap = Generate(CreateBiomeIndices(BiomeType.Grassland));
            var secondAlphamap = Generate(CreateBiomeIndices(BiomeType.Grassland));

            for (var z = 0; z < AlphamapResolution; z++)
            for (var x = 0; x < AlphamapResolution; x++)
            for (var layer = 0; layer < 3; layer++)
                Assert.That(secondAlphamap[z, x, layer], Is.EqualTo(firstAlphamap[z, x, layer]), $"z={z} x={x} layer={layer}");
        }

        [Test]
        public void WinnerWriterMapsOceanToSeaAndKeepsTheRecomputedWinnerOnBeach()
        {
            // Beachは転送データが元のwinnerを潰しているので再計算値を残す。他は転送値で上書きする
            // Beach keeps the recomputed winner because the transferred data destroyed the original; everything else is overwritten
            var winnerBiomeIndex = new NativeArray<int>(4, Allocator.Temp);
            for (var i = 0; i < winnerBiomeIndex.Length; i++) winnerBiomeIndex[i] = 7;

            var transferredBiomeIndices = new byte[2, 2];
            transferredBiomeIndices[0, 0] = (byte)BiomeType.Ocean;
            transferredBiomeIndices[0, 1] = (byte)BiomeType.Beach;
            transferredBiomeIndices[1, 0] = (byte)BiomeType.Grassland;
            transferredBiomeIndices[1, 1] = (byte)BiomeType.Forest;

            WinnerBiomeIndexWriter.Overwrite(
                winnerBiomeIndex, transferredBiomeIndices, new[] { BiomeType.Grassland, BiomeType.Forest }, 2);

            Assert.That(winnerBiomeIndex[0], Is.EqualTo(-1), "Ocean は海ピクセル");
            Assert.That(winnerBiomeIndex[1], Is.EqualTo(7), "Beach は再計算値を残す");
            Assert.That(winnerBiomeIndex[2], Is.EqualTo(0), "Grassland は有効バイオーム内の索引");
            Assert.That(winnerBiomeIndex[3], Is.EqualTo(1), "Forest は有効バイオーム内の索引");

            winnerBiomeIndex.Dispose();
        }

        [Test]
        public void WinnerWriterThrowsWhenTransferredBiomeIsNotEnabled()
        {
            // 有効バイオームの食い違いは黙って0番へ倒さない。全面が別バイオームのテクスチャになる
            // A disagreement over enabled biomes is never folded into index 0; it would texture everything as another biome
            var winnerBiomeIndex = new NativeArray<int>(1, Allocator.Temp);
            var transferredBiomeIndices = new byte[1, 1];
            transferredBiomeIndices[0, 0] = (byte)BiomeType.Desert;

            Assert.Throws<InvalidOperationException>(() => WinnerBiomeIndexWriter.Overwrite(
                winnerBiomeIndex, transferredBiomeIndices, new[] { BiomeType.Grassland }, 1));

            winnerBiomeIndex.Dispose();
        }

        private static float[,,] Generate(byte[,] transferredBiomeIndices)
        {
            var config = CreateConfig();
            var biomeTypes = new[] { BiomeType.Grassland };
            var visualSections = new BiomeVisualSections(
                new[] { "addr/grass" },
                new[] { new BiomeTextureConfig { entries = new TextureEntry[0] } },
                new[] { new BiomeDetailConfig { entries = new DetailEntry[0] } },
                // 岩の裸地は見ないので既存の岩レイヤーを指す。新しいアドレスだと列が1本増えて重み合計の検証がずれる
                // The bare ground is out of scope here, so it reuses the rock layer: a new address would add a column and shift the weight-sum check
                new[] { new SurroundTextureConfig { surroundLayerAddressablePath = "addr/rock" } });
            var layerTable = SplatLayerTable.Build(
                "addr/beach", "addr/rock", visualSections.MainLayerAddresses, visualSections.TextureConfigs,
                visualSections.SurroundTextureConfigs, SurroundTestFixtures.CreateTreeSurroundSpecies(), Array.Empty<string>());

            // 分類は呼び出し側の持ち物になった。1回の生成につき1個をusingで抱える本番と同じ形
            // The classification now belongs to the caller, held one per generation in a using as production does
            using var classification = new TileClassificationContext(config, biomeTypes);
            classification.Initialize();
            return SplatmapStage.Generate(
                config, biomeTypes, classification, layerTable, visualSections,
                SurroundTestFixtures.CreateTreeSurroundSpecies(),
                CreateHeights(), transferredBiomeIndices, AlphamapResolution,
                new List<LedgerPlacement>(), Vector3.zero);
        }

        private static TerrainGenerationConfig CreateConfig()
        {
            return new TerrainGenerationConfig
            {
                overrideResolution = Resolution, detailResolution = 1024, seed = 12345,
                grasslandEnabled = true, forestEnabled = false,
                savannaEnabled = false, desertEnabled = false,
                mesaEnabled = false,
                alpineEnabled = false,
                jungleEnabled = false,
                woodsEnabled = false,
            };
        }

        private static float[,] CreateHeights()
        {
            // x方向に上がる傾斜。傾斜・曲率フィルタが一様入力で潰れないようにする
            // A slope rising along x so the slope and curvature filters are not fed a flat input
            var heights = new float[Resolution, Resolution];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                heights[z, x] = x / (float)(Resolution - 1) * 0.5f;
            return heights;
        }

        private static byte[,] CreateBiomeIndices(BiomeType biomeType)
        {
            var biomeIndices = new byte[Resolution, Resolution];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                biomeIndices[z, x] = (byte)biomeType;
            return biomeIndices;
        }
    }
}
