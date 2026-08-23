using System;
using Game.MapGeneration.Pipeline.Visual;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Visual
{
    public class TileAlphamapTest
    {
        private const int Resolution = 3;
        private const int PlaneByteLength = Resolution * Resolution * 4;

        [TestCase(1, 1)]
        [TestCase(4, 1)]
        [TestCase(5, 2)]
        [TestCase(19, 5)]
        public void CreatesValidLayerShapes(int layerCount, int expectedPlaneCount)
        {
            var planes = CreatePlanes(expectedPlaneCount, PlaneByteLength);

            var alphamap = TileAlphamap.Create(planes, Resolution, layerCount);

            Assert.That(alphamap.Planes.Count, Is.EqualTo(expectedPlaneCount));
            for (var planeIndex = 0; planeIndex < expectedPlaneCount; planeIndex++)
                Assert.That(alphamap.Planes[planeIndex].Length, Is.EqualTo(PlaneByteLength));
            Assert.That(alphamap.Resolution, Is.EqualTo(Resolution));
            Assert.That(alphamap.LayerCount, Is.EqualTo(layerCount));
        }

        [Test]
        public void RejectsWrongPlaneCount()
        {
            var planes = CreatePlanes(1, PlaneByteLength);

            Assert.Throws<ArgumentException>(() => TileAlphamap.Create(planes, Resolution, 5));
        }

        [Test]
        public void RejectsWrongPlaneByteLength()
        {
            var planes = CreatePlanes(1, PlaneByteLength - 1);

            Assert.Throws<ArgumentException>(() => TileAlphamap.Create(planes, Resolution, 4));
        }

        [TestCase(0, 1)]
        [TestCase(-1, 1)]
        [TestCase(Resolution, 0)]
        [TestCase(Resolution, -1)]
        public void RejectsNonPositiveResolutionOrLayerCount(int resolution, int layerCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => TileAlphamap.Create(Array.Empty<byte[]>(), resolution, layerCount));
        }

        [Test]
        public void RejectsNullPlanes()
        {
            Assert.Throws<ArgumentNullException>(() => TileAlphamap.Create(null, Resolution, 1));
        }

        [Test]
        public void SourceMutationAfterCreateCannotChangeStoredState()
        {
            var planes = CreatePlanes(1, PlaneByteLength);
            var alphamap = TileAlphamap.Create(planes, Resolution, 1);

            planes[0][0] = 123;
            planes[0] = Array.Empty<byte>();

            Assert.That(alphamap.Planes[0].Span[0], Is.EqualTo(0));
            Assert.That(alphamap.Planes[0].Length, Is.EqualTo(PlaneByteLength));
        }

        [Test]
        public void PubliclyObtainedCopyCannotChangeStoredState()
        {
            var alphamap = TileAlphamap.Create(CreatePlanes(1, PlaneByteLength), Resolution, 1);

            var obtainedBytes = alphamap.Planes[0].ToArray();
            obtainedBytes[0] = 123;

            Assert.That(alphamap.Planes[0].Span[0], Is.EqualTo(0));
        }

        private static byte[][] CreatePlanes(int planeCount, int planeByteLength)
        {
            var planes = new byte[planeCount][];
            for (var planeIndex = 0; planeIndex < planeCount; planeIndex++)
                planes[planeIndex] = new byte[planeByteLength];

            return planes;
        }
    }
}
