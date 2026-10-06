using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline.Generators;
using Game.MapGeneration.Pipeline.Surface;
using Game.MapGeneration.Pipeline.Surface.Placement;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    public class SurfaceQuantizationRoundTripTest
    {
        [TestCase(1994.5435791015625f)]
        [TestCase(600f)]
        [TestCase(5f)]
        public void SerializedFloorRetainsTheDoubleEnvelope(float height)
        {
            var envelope = SurfaceEnvelope.GeneratedV5;
            double minimum = (double)envelope.SeaY + envelope.MaximumWaveRise + envelope.LandClearance;
            float floor = SurfaceQuantization.LandFloor(height, envelope);
            float decoded = RoundTrip(floor, height);
            Assert.That((double)decoded, Is.GreaterThanOrEqualTo(minimum));
            Assert.That(decoded, Is.EqualTo(floor));
        }

        [TestCase(1988.8231201171875f, 20)]
        [TestCase(600f, 5)]
        [TestCase(600f, 20)]
        [TestCase(600f, 599)]
        [TestCase(600f, 600)]
        public void SerializedPadRetainsTheMiningGap(float height, int bottom)
        {
            float pad = SurfaceQuantization.PadHeight(bottom, height);
            float decoded = RoundTrip(pad, height);
            Assert.That((double)decoded, Is.LessThanOrEqualTo(bottom - 0.001d));
            Assert.That(decoded, Is.EqualTo(pad));
            Assert.That(bottom - decoded, Is.LessThanOrEqualTo(height / 65535d + 0.00101d));
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

        private static float RoundTrip(float meters, float height)
        {
            // TerrainFileWriterと読込側のfloat算術を同じ順に通す
            // Execute the float arithmetic in the same order as TerrainFileWriter and the loader
            float normalized = meters / height;
            int units = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(normalized) * ushort.MaxValue), 0, ushort.MaxValue);
            byte low = (byte)(units & 255);
            byte high = (byte)(units >> 8);
            ushort stored = (ushort)(low | high << 8);
            var loadedHeights = new float[1, 1];
            loadedHeights[0, 0] = stored / (float)ushort.MaxValue;
            return loadedHeights[0, 0] * height;
        }
    }
}
