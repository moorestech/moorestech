using System;
using System.IO;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Export;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using Game.Paths;
using Game.MapGeneration.Surface;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Surface;
using Game.MapGeneration.Pipeline.Surface.Placement;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    public class SurfaceQuantizationRoundTripTest
    {
        private string _scratch;
        private TerrainData _terrain;

        [TearDown]
        public void TearDown()
        {
            if (_terrain != null) UnityEngine.Object.DestroyImmediate(_terrain);
            if (_scratch != null && Directory.Exists(_scratch)) Directory.Delete(_scratch, true);
        }

        [TestCase(1994.5435791015625f)]
        [TestCase(600f)]
        [TestCase(5f)]
        public void SerializedFloorRetainsTheDoubleEnvelope(float height)
        {
            var envelope = SurfaceEnvelope.GeneratedV5;
            double minimum = (double)envelope.SeaY + envelope.MaximumWaveRise + envelope.LandClearance;
            float floor = SurfaceQuantization.LandFloor(new TerrainGenerationConfig { terrainHeight = height }, envelope, "fixture");
            float decoded = RoundTrip(floor, height);
            Assert.That((double)decoded, Is.GreaterThanOrEqualTo(minimum));
            Assert.That(decoded, Is.EqualTo(floor));
        }

        [TestCase(1988.8231201171875f, 20)]
        [TestCase(600f, 5)]
        [TestCase(600f, 20)]
        [TestCase(600f, 37)]
        [TestCase(600f, 120)]
        [TestCase(600f, 599)]
        [TestCase(600f, 600)]
        public void SerializedPadRetainsTheMiningGap(float height, int bottom)
        {
            float pad = SurfaceQuantization.PadHeight(bottom, new TerrainGenerationConfig { terrainHeight = height }, "fixture");
            float decoded = RoundTrip(pad, height);
            Assert.That((double)decoded, Is.LessThanOrEqualTo(bottom - SurfaceQuantization.MiningBottomClearanceMeters));
            Assert.That(decoded, Is.EqualTo(pad));
            Assert.That(bottom - decoded, Is.LessThanOrEqualTo(height / (double)SurfaceQuantization.TerrainStorageSteps + SurfaceGuaranteeBounds.RangeGapTolerance));
        }

        [Test]
        public void FractionalNoiseOriginUsesTheActualIntegerVeinShift()
        {
            var geometry = new SurfaceLattice(Vector2.zero, Vector2.one * 0.25f, 81, 81);
            var land = new bool[81 * 81];
            for (int i = 0; i < land.Length; i++) land[i] = true;
            land[40 * 81 + 59] = false;
            var constraint = new GroundedVeinLandConstraint(new LandCellField(geometry, land),
                new Vector2(0.49f, 0f), SurfaceEnvelope.GeneratedV5);
            Assert.That(constraint.Accept(VeinAabbBuilder.Build("fixture", new Vector3(10f, 0f, 10f))), Is.False);
        }

        [Test]
        public void EveryStorageStepSurvivesProductionR16AndTerrainData()
        {
            const int resolution = 257;
            _scratch = Path.Combine(Path.GetTempPath(), "vtg-grid-" + Guid.NewGuid().ToString("N"));
            var saved = WorldDataDirectory.FromWorldRoot(_scratch);
            var output = new MapGenerationOutput { Resolution = resolution };
            var values = new float[resolution * resolution];
            for (int units = 0; units <= SurfaceQuantization.TerrainStorageSteps; units++)
                values[units] = SurfaceQuantization.EncodeNormalized(units / (float)SurfaceQuantization.TerrainStorageSteps);
            output.Tiles.Add(new TerrainTileOutput { TileX = 0, TileZ = 0, Heights = values });

            // 全格納段を実ファイルとUnityで検査
            // Inspect every storage step through real files and Unity
            TerrainFileWriter.Write(saved, output);
            var loaded = HeightFileLoader.LoadHeights(saved, 0, 0, resolution);
            _terrain = new TerrainData { heightmapResolution = resolution, size = new Vector3(256f, 600f, 256f) };
            _terrain.SetHeights(0, 0, loaded);
            var stored = _terrain.GetHeights(0, 0, resolution, resolution);
            for (int units = 0; units <= SurfaceQuantization.TerrainStorageSteps; units++)
                Assert.That(stored[units / resolution, units % resolution],
                    Is.EqualTo((float)(units * (double)(1f / SurfaceQuantization.TerrainStorageSteps))), $"Storage step {units}");
        }

        [TestCase(1025, 0.03128242492675781f)]
        [TestCase(32111, 0.9800097346305847f)]
        public void StorageReadbackMatchesObservedFloatReciprocal(int units, float observed)
        {
            // 初回失敗の実Unity値を許容なしで固定
            // Pin the first-failure Unity values without tolerance
            float encoded = SurfaceQuantization.EncodeNormalized(units / (float)SurfaceQuantization.TerrainStorageSteps);
            Assert.That(SurfaceQuantization.StoredNormalized(encoded), Is.EqualTo(observed));
        }

        private float RoundTrip(float meters, float height)
        {
            _scratch = Path.Combine(Path.GetTempPath(), "vtg-quantization-" + Guid.NewGuid().ToString("N"));
            var saved = WorldDataDirectory.FromWorldRoot(_scratch);
            var output = new MapGenerationOutput { Resolution = 33 };
            var values = new float[33 * 33];
            float encoded = SurfaceQuantization.EncodeNormalized(meters / height);
            for (int index = 0; index < values.Length; index++) values[index] = encoded;
            output.Tiles.Add(new TerrainTileOutput { TileX = 0, TileZ = 0, Heights = values });

            // 本番r16保存と読込後に実地形へ格納
            // Store into real TerrainData after production r16 save and load
            TerrainFileWriter.Write(saved, output);
            var loaded = HeightFileLoader.LoadHeights(saved, 0, 0, 33);
            Assert.That(loaded[0, 0], Is.EqualTo(encoded), "r16 reload must preserve the encoded value");
            _terrain = new TerrainData { heightmapResolution = 33, size = new Vector3(32f, height, 32f) };
            _terrain.SetHeights(0, 0, loaded);
            float normalized = _terrain.GetHeights(0, 0, 1, 1)[0, 0];
            Assert.That(normalized, Is.EqualTo(SurfaceQuantization.StoredNormalized(encoded)));
            return (float)((double)normalized * height);
        }
    }
}
