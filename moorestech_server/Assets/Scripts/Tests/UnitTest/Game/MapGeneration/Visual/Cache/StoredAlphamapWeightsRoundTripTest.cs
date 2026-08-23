using System;
using System.IO;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Surround;
using NUnit.Framework;
using UnityEngine;
using static Tests.UnitTest.Game.MapGeneration.Visual.Surround.SurroundTestFixtures;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Cache
{
    /// <summary>
    ///     裸地の塗りで合計が1を超えた画素が、平面化とキャッシュ往復で色配分を変えないことを検証する。
    ///     平面は1画素1バイトなので、畳まずに焼くと1で頭打ちして比率が変わり、
    ///     同じワールドでも初回生成時とリロード後で岩まわりの色が食い違う
    ///     Verifies pixels pushed past a weight sum of one by the bare-ground painting keep their colour split through the
    ///     flattening and a cache round trip. A plane holds one byte per pixel, so an unfolded bake clips at one and shifts
    ///     the ratio, leaving the same world's rocks differently coloured after a restart
    /// </summary>
    public class StoredAlphamapWeightsRoundTripTest
    {
        private const int ClusterId = 7;
        private const int NoCluster = -1;
        private const int HeightmapResolution = 2;
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
        public void ThePlanesComeBackFromTheCacheUnchanged()
        {
            var planes = StoredAlphamapWeights.ToPlanes(PaintOverlappingRocks());

            var restored = WriteAndRead(planes);

            Assert.That(restored.Count, Is.EqualTo(planes.Length));
            for (var planeIndex = 0; planeIndex < planes.Length; planeIndex++)
                Assert.That(restored[planeIndex], Is.EqualTo(planes[planeIndex]), $"plane={planeIndex}");
        }

        [Test]
        public void TheFoldKeepsTheRatioOfAPixelAboveOne()
        {
            // 頭打ちなら [1.4, 0.2] は 0.833/0.167 になる。比率を保った畳み込みだけが 0.875/0.125 を返す
            // Clipping would turn [1.4, 0.2] into 0.833/0.167; only a ratio-preserving fold returns 0.875/0.125
            var alphamap = new float[1, 1, 2];
            alphamap[0, 0, 0] = 1.4f;
            alphamap[0, 0, 1] = 0.2f;

            var planes = StoredAlphamapWeights.ToPlanes(alphamap);

            Assert.That(planes[0][0] / 255f, Is.EqualTo(0.875f).Within(1f / 255f));
            Assert.That(planes[0][1] / 255f, Is.EqualTo(0.125f).Within(1f / 255f));
            Assert.That((planes[0][0] + planes[0][1]) / 255f, Is.EqualTo(1f).Within(1f / 255f));
        }

        [Test]
        public void EachLayerLandsOnItsOwnPlaneChannel()
        {
            // 平面と channel の割り当てが崩れると、層の入れ替わりが起きても合計だけは合ってしまう
            // A broken plane-and-channel assignment still keeps the sums right while the layers swap places
            // 合計1に収めて畳み込みの正規化を挟ませない。ここで見たいのは層と channel の対応だけ
            // Keep the sum at one so the fold's normalization stays out of it; only the layer-to-channel mapping is under test
            var alphamap = new float[1, 1, 6];
            alphamap[0, 0, 0] = 0.25f;
            alphamap[0, 0, 5] = 0.75f;

            var planes = StoredAlphamapWeights.ToPlanes(alphamap);

            Assert.That(planes.Length, Is.EqualTo(2), "6層はRGBA2枚に載る");
            Assert.That(planes[0][0], Is.EqualTo(64), "layer0は平面0のR");
            Assert.That(planes[1][1], Is.EqualTo(191), "layer5は平面1のG");
        }

        [Test]
        public void QuantizedWeightsSumToOneAcrossNineteenLayers()
        {
            // 1/19は各channelの独立丸めだけでは合計255にならないため残差配分を直接踏む
            // One nineteenth does not total 255 under independent channel rounding, directly exercising residue distribution
            const int layerCount = 19;
            var alphamap = new float[1, 1, layerCount];
            for (var layer = 0; layer < layerCount; layer++) alphamap[0, 0, layer] = 1f / layerCount;

            var planes = StoredAlphamapWeights.ToPlanes(alphamap);

            var total = 0;
            for (var layer = 0; layer < layerCount; layer++)
                total += planes[layer / TerrainVisualCacheFormat.LayersPerAlphamapPlane]
                    [layer % TerrainVisualCacheFormat.LayersPerAlphamapPlane];
            Assert.That(total, Is.EqualTo(byte.MaxValue));
        }

        // 3つの有効層を別平面へ散らし、独立丸めの加算・減算補正が最強層へ入ることを両方向で固定する
        // Spreads three active layers across planes and pins both additive and subtractive correction to the strongest layer
        [TestCase(50.4f, 154.2f, 254, 155, TestName = "AddsResidueToStrongestLayerWhenNineteenLayerRoundingTotals254Test")]
        [TestCase(50.6f, 153.8f, 256, 153, TestName = "SubtractsResidueFromStrongestLayerWhenNineteenLayerRoundingTotals256Test")]
        public void CorrectsNineteenLayerRoundingResidueTest(
            float smallScaledWeight, float strongestScaledWeight, int independentTotal, int expectedStrongestByte)
        {
            const int layerCount = 19;
            const int middleLayer = 5;
            const int strongestLayer = 18;
            var alphamap = new float[1, 1, layerCount];
            alphamap[0, 0, 0] = smallScaledWeight / byte.MaxValue;
            alphamap[0, 0, middleLayer] = smallScaledWeight / byte.MaxValue;
            alphamap[0, 0, strongestLayer] = strongestScaledWeight / byte.MaxValue;

            Assert.That(
                Mathf.RoundToInt(smallScaledWeight) * 2 + Mathf.RoundToInt(strongestScaledWeight),
                Is.EqualTo(independentTotal), "補正前の独立丸め合計");
            var planes = StoredAlphamapWeights.ToPlanes(alphamap);

            var correctedTotal = 0;
            for (var layer = 0; layer < layerCount; layer++)
                correctedTotal += planes[layer / TerrainVisualCacheFormat.LayersPerAlphamapPlane]
                    [layer % TerrainVisualCacheFormat.LayersPerAlphamapPlane];
            Assert.That(correctedTotal, Is.EqualTo(byte.MaxValue));
            Assert.That(planes[strongestLayer / TerrainVisualCacheFormat.LayersPerAlphamapPlane]
                [strongestLayer % TerrainVisualCacheFormat.LayersPerAlphamapPlane], Is.EqualTo(expectedStrongestByte));
        }

        [Test]
        public void ZeroWeightPixelRemainsZero()
        {
            var planes = StoredAlphamapWeights.ToPlanes(new float[1, 1, 19]);

            foreach (var plane in planes)
                Assert.That(plane, Is.All.EqualTo(0));
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

        private System.Collections.Generic.IReadOnlyList<byte[]> WriteAndRead(byte[][] planes)
        {
            TerrainVisualCacheWriter.Write(_filePath, CacheKey, new TerrainTileVisual(
                new float[HeightmapResolution, HeightmapResolution],
                TileAlphamap.Create(planes, AlphaResolution, LayerCount), Array.Empty<int[,]>()));

            var succeeded = TerrainVisualCacheReader.TryRead(
                _filePath, CacheKey, HeightmapResolution, AlphaResolution, LayerCount, 0, 0, out var tileVisual, out var brokenReason);
            Assert.That(succeeded, Is.True, brokenReason);

            return tileVisual.Alphamap.Planes;
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
