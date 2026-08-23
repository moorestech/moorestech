using System;
using System.IO;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Pipeline.Visual.Surround;
using NUnit.Framework;
using UnityEngine;
using static Tests.UnitTest.Game.MapGeneration.Visual.Surround.SurroundTestFixtures;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Cache
{
    /// <summary>
    ///     裸地の塗りで合計が1を超えた画素が、キャッシュ往復で色配分を変えないことを検証する。
    ///     キャッシュは1画素1バイトなので、畳まずに焼くと1で頭打ちして再読込時だけ比率が変わり、
    ///     同じワールドでも初回生成時とリロード後で岩まわりの色が食い違う
    ///     Verifies pixels pushed past a weight sum of one by the bare-ground painting keep their colour split across a
    ///     cache round trip. The cache holds one byte per pixel, so an unfolded bake clips at one and shifts the ratio on
    ///     reload alone, leaving the same world's rocks differently coloured after a restart
    /// </summary>
    public class StoredAlphamapWeightsRoundTripTest
    {
        private const int ClusterId = 7;
        private const int NoCluster = -1;
        private const string CacheKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private string _directoryPath;
        private string _filePath;

        [SetUp]
        public void SetUp()
        {
            LoadMasterData();
            _directoryPath = Path.Combine(Path.GetTempPath(), $"moorestech_alphamap_round_trip_{Guid.NewGuid()}");
            _filePath = Path.Combine(_directoryPath, "visual.bin");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directoryPath)) Directory.Delete(_directoryPath, true);
        }

        [Test]
        public void TheOverlappingRocksPaintPixelsWhoseWeightSumExceedsOne()
        {
            // この盤面が合計1以下で収まると、以下の往復テストは頭打ちを一度も踏まずに通ってしまう
            // Were this board to stay at or below one, the round-trip tests below would pass without ever meeting the clipping
            Assert.That(MaximumWeightSum(PaintOverlappingRocks()), Is.GreaterThan(1.0001f));
        }

        [Test]
        public void AFoldedAlphamapComesBackFromTheCacheUnchanged()
        {
            var alphamap = PaintOverlappingRocks();
            StoredAlphamapWeights.Fold(alphamap);

            var restored = WriteAndRead(alphamap);

            for (var z = 0; z < AlphaResolution; z++)
            for (var x = 0; x < AlphaResolution; x++)
            for (var layer = 0; layer < LayerCount; layer++)
                Assert.That(restored[z, x, layer], Is.EqualTo(alphamap[z, x, layer]), $"z={z} x={x} layer={layer}");
        }

        [Test]
        public void AnUnfoldedAlphamapDoesNotSurviveTheSameRoundTrip()
        {
            // 畳み込みを外したときに何が起きるかを固定する。合計1超の画素は1へ頭打ちし、他の画素も量子化でずれる
            // Pins down what dropping the fold does: a pixel above one clips to one and the rest drift by quantization
            var alphamap = PaintOverlappingRocks();
            var restored = WriteAndRead(alphamap);

            Assert.That(HasDifferentPixel(alphamap, restored), Is.True);
        }

        [Test]
        public void TheFoldKeepsTheRatioOfAPixelAboveOne()
        {
            // 頭打ちなら [1.4, 0.2] は 0.833/0.167 になる。比率を保った畳み込みだけが 0.875/0.125 を返す
            // Clipping would turn [1.4, 0.2] into 0.833/0.167; only a ratio-preserving fold returns 0.875/0.125
            var alphamap = new float[1, 1, 2];
            alphamap[0, 0, 0] = 1.4f;
            alphamap[0, 0, 1] = 0.2f;

            StoredAlphamapWeights.Fold(alphamap);

            Assert.That(alphamap[0, 0, 0], Is.EqualTo(0.875f).Within(1f / 255f));
            Assert.That(alphamap[0, 0, 1], Is.EqualTo(0.125f).Within(1f / 255f));
            Assert.That(alphamap[0, 0, 0] + alphamap[0, 0, 1], Is.EqualTo(1f).Within(1f / 255f));
        }

        // クラスタ経路と単体岩経路の両方が同じ画素へ書く配置。2回目の書き込みは書き込み先に重みがある状態で走る
        // A layout where the cluster path and the lone-rock path write the same pixels, so the second write lands on weight already there
        private static float[,,] PaintOverlappingRocks()
        {
            var alphamap = CreateAlphamapWithSurroundWeight();
            ObjectSurroundTexturePainter.Apply(
                alphamap, CreateConfig(), CreateLayerTable(), new[] { CreateSurroundConfig() },
                CreateBiomeWeights(0), 1, CreateHeights(0f),
                new[] { CreateRock(ClusterId), CreateRock(NoCluster) }, Vector3.zero);

            return alphamap;
        }

        private float[,,] WriteAndRead(float[,,] alphamap)
        {
            TerrainVisualCacheWriter.Write(_filePath, CacheKey, new TerrainTileVisual(alphamap, Array.Empty<int[,]>()));

            var succeeded = TerrainVisualCacheReader.TryRead(
                _filePath, CacheKey, AlphaResolution, LayerCount, 0, 0, out var tileVisual, out var brokenReason);
            Assert.That(succeeded, Is.True, brokenReason);

            return tileVisual.Alphamap;
        }

        private static bool HasDifferentPixel(float[,,] left, float[,,] right)
        {
            for (var z = 0; z < AlphaResolution; z++)
            for (var x = 0; x < AlphaResolution; x++)
            for (var layer = 0; layer < LayerCount; layer++)
                if (left[z, x, layer] != right[z, x, layer])
                    return true;

            return false;
        }

        private static float MaximumWeightSum(float[,,] alphamap)
        {
            var maximum = 0f;
            for (var z = 0; z < AlphaResolution; z++)
            for (var x = 0; x < AlphaResolution; x++)
            {
                var sum = 0f;
                for (var layer = 0; layer < LayerCount; layer++) sum += alphamap[z, x, layer];
                if (maximum < sum) maximum = sum;
            }

            return maximum;
        }
    }
}
