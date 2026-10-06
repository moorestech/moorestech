using System;
using System.Text.RegularExpressions;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using Game.MapGeneration.Pipeline.Surface;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    public class SurfaceBoundaryOwnershipTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void UnequalWindowSamplesEmitOneOwnerAcrossTransferableChannelsAndFourWayCorner(bool reverse)
        {
            var config = new TerrainGenerationConfig { overrideResolution = 3, gridSizeX = 2, gridSizeZ = 2 };
            var samples = new SurfaceBoundarySamples(config, 2);
            using var southwest = JobDataConverter.AllocateBuffers(3, 2, 1, Allocator.TempJob);
            using var southeast = JobDataConverter.AllocateBuffers(3, 2, 1, Allocator.TempJob);
            using var northwest = JobDataConverter.AllocateBuffers(3, 2, 1, Allocator.TempJob);
            using var northeast = JobDataConverter.AllocateBuffers(3, 2, 1, Allocator.TempJob);
            var tiles = new[] { southwest, southeast, northwest, northeast };

            // 重複する窓の値を意図的に異ならせ、取得順を反転する
            // Deliberately disagree at overlapping window samples and reverse capture order
            for (int tile = 0; tile < tiles.Length; tile++) Fill(tiles[tile], tile);
            for (int step = 0; step < tiles.Length; step++)
            {
                int tile = reverse ? 3 - step : step;
                samples.CaptureOwned(tile % 2, tile / 2, tiles[tile]);
            }
            for (int step = 0; step < tiles.Length; step++)
            {
                int tile = reverse ? step : 3 - step;
                samples.Emit(tile % 2, tile / 2, tiles[tile]);
            }

            // 縦横境界と四枚の角が同じ所有サンプルへ一致する
            // Vertical and horizontal seams and the four-tile corner share the owning sample
            for (int i = 0; i < 3; i++)
            {
                AssertSample(southeast, i * 3, 0, i * 3 + 2);
                AssertSample(northwest, i, 0, 6 + i);
            }
            AssertSample(northeast, 0, 0, 8);
            AssertSample(northeast, 3, 2, 5);
            AssertSample(northeast, 1, 1, 7);
            AssertSample(northeast, 4, 3, 4);
            for (int tile = 0; tile < tiles.Length; tile++)
            for (int i = 0; i < 9; i++)
                Assert.That(tiles[tile].regionLabels[i], Is.EqualTo(tile * 10 + i));
        }

        [Test]
        public void MissingOwnerFailsRatherThanChoosingFirstArrivingTile()
        {
            var config = new TerrainGenerationConfig { overrideResolution = 3, gridSizeX = 2, gridSizeZ = 2 };
            var samples = new SurfaceBoundarySamples(config, 2);
            using var destination = JobDataConverter.AllocateBuffers(3, 2, 1, Allocator.TempJob);
            samples.CaptureOwned(1, 1, destination);
            LogAssert.Expect(LogType.Error, new Regex("Boundary owner has not emitted vertex 2,2"));
            Assert.Throws<InvalidOperationException>(() => samples.Emit(1, 1, destination));
        }

        private static void Fill(JobBuffers target, int tile)
        {
            for (int i = 0; i < 9; i++)
            {
                float value = tile * 0.1f + i * 0.001f;
                target.heights[i] = value;
                target.landMask[i] = tile % 2;
                target.shoreMask[i] = value + 1f;
                target.beachFactor[i] = value + 2f;
                target.landTextureFactor[i] = value + 3f;
                target.seaTextureFactor[i] = value + 4f;
                target.plateauMask[i] = value + 5f;
                target.winnerBiomeIndex[i] = tile;
                target.regionLabels[i] = tile * 10 + i;
                target.biomeWeights[i * 2] = value + 6f;
                target.biomeWeights[i * 2 + 1] = value + 7f;
            }
        }

        private static void AssertSample(JobBuffers actual, int index, int owner, int ownerIndex)
        {
            float value = owner * 0.1f + ownerIndex * 0.001f;
            Assert.That(actual.heights[index], Is.EqualTo(value));
            Assert.That(actual.landMask[index], Is.EqualTo(owner % 2));
            Assert.That(actual.shoreMask[index], Is.EqualTo(value + 1f));
            Assert.That(actual.beachFactor[index], Is.EqualTo(value + 2f));
            Assert.That(actual.landTextureFactor[index], Is.EqualTo(value + 3f));
            Assert.That(actual.seaTextureFactor[index], Is.EqualTo(value + 4f));
            Assert.That(actual.plateauMask[index], Is.EqualTo(value + 5f));
            Assert.That(actual.winnerBiomeIndex[index], Is.EqualTo(owner));
            Assert.That(actual.biomeWeights[index * 2], Is.EqualTo(value + 6f));
            Assert.That(actual.biomeWeights[index * 2 + 1], Is.EqualTo(value + 7f));
        }
    }
}
