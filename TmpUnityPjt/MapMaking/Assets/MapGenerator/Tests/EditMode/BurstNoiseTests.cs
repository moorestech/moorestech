using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using MapGenerator.Pipeline.Jobs;

namespace MapGenerator.Tests.EditMode
{
    [TestFixture]
    public class BurstNoiseTests
    {
        NativeArray<float2> _offsets;

        [SetUp]
        public void SetUp()
        {
            _offsets = new NativeArray<float2>(16, Allocator.Temp);
            var rng = new System.Random(42);
            for (int i = 0; i < 16; i++)
                _offsets[i] = new float2(
                    (float)rng.NextDouble() * 10000f,
                    (float)rng.NextDouble() * 10000f);
        }

        [TearDown]
        public void TearDown() => _offsets.Dispose();

        [Test]
        public void FBm_ReturnsInRange01()
        {
            for (int i = 0; i < 100; i++)
            {
                float v = BurstNoise.FBm(new float2(i * 7.3f, i * 11.1f),
                    0.01f, _offsets, 0, 0.5f, 2f, 4);
                Assert.That(v, Is.InRange(0f, 1f),
                    $"FBm out of range at iteration {i}: {v}");
            }
        }

        [Test]
        public void FBm_IsDeterministic()
        {
            float a = BurstNoise.FBm(new float2(100f, 200f), 0.01f, _offsets, 0, 0.5f, 2f, 4);
            float b = BurstNoise.FBm(new float2(100f, 200f), 0.01f, _offsets, 0, 0.5f, 2f, 4);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void FBm_DifferentCoordsProduceDifferentValues()
        {
            float a = BurstNoise.FBm(new float2(0f, 0f), 0.01f, _offsets, 0, 0.5f, 2f, 4);
            float b = BurstNoise.FBm(new float2(500f, 500f), 0.01f, _offsets, 0, 0.5f, 2f, 4);
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void FBmRaw_ReturnsInRangeNeg1To1()
        {
            for (int i = 0; i < 100; i++)
            {
                float v = BurstNoise.FBmRaw(new float2(i * 7.3f, i * 11.1f),
                    0.01f, _offsets, 0, 0.5f, 2f, 4);
                Assert.That(v, Is.InRange(-1f, 1f),
                    $"FBmRaw out of range at iteration {i}: {v}");
            }
        }

        [Test]
        public void FBmRaw_IsDeterministic()
        {
            float a = BurstNoise.FBmRaw(new float2(100f, 200f), 0.01f, _offsets, 0, 0.5f, 2f, 4);
            float b = BurstNoise.FBmRaw(new float2(100f, 200f), 0.01f, _offsets, 0, 0.5f, 2f, 4);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void FBm_And_FBmRaw_AreConsistent()
        {
            // FBmRaw = (FBm - 0.5) * 2 の関係を確認
            float2 pos = new float2(123f, 456f);
            float fbm = BurstNoise.FBm(pos, 0.01f, _offsets, 0, 0.5f, 2f, 4);
            float raw = BurstNoise.FBmRaw(pos, 0.01f, _offsets, 0, 0.5f, 2f, 4);
            float expected = (fbm - 0.5f) * 2f;
            Assert.AreEqual(expected, raw, 0.0001f);
        }

        [Test]
        public void Ridged_ReturnsInRange01()
        {
            for (int i = 0; i < 100; i++)
            {
                float v = BurstNoise.Ridged(new float2(i * 7.3f, i * 11.1f),
                    0.01f, _offsets, 0, 2f, 4, 1f, 2f);
                Assert.That(v, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void Worley_ReturnsInRange01()
        {
            for (int i = 0; i < 100; i++)
            {
                float v = BurstNoise.Worley(new float2(i * 7.3f, i * 11.1f),
                    0.01f, _offsets[0]);
                Assert.That(v, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void WormFBm_ReturnsInRange01()
        {
            for (int i = 0; i < 100; i++)
            {
                float v = BurstNoise.WormFBm(new float2(i * 7.3f, i * 11.1f),
                    0.01f, _offsets, 0, 4, 1f);
                Assert.That(v, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void Terrace_BoundaryValues()
        {
            Assert.AreEqual(0f, BurstNoise.Terrace(0f, 4, 0.85f), 0.01f);
            float top = BurstNoise.Terrace(1f, 4, 0.85f);
            Assert.That(top, Is.InRange(0.95f, 1.0f));
        }

        [Test]
        public void ValleyNetwork_ZeroDepth_ReturnsOne()
        {
            float v = BurstNoise.ValleyNetwork(new float2(100f, 200f),
                0.01f, 4, 0f, _offsets, 0, 0.5f, 2f, 1f);
            Assert.AreEqual(1f, v, 0.001f);
        }

        [Test]
        public void SampleByType_AllTypesInRange()
        {
            var types = new[] { 0, 1, 2, 3, 4 }; // None, WormFBM, Worley, Simple, FBM
            foreach (int t in types)
            {
                float v = BurstNoise.SampleByType(t, new float2(100f, 200f), 0.01f, _offsets, 0);
                Assert.That(v, Is.InRange(0f, 1.01f), $"Type {t} out of range");
            }
        }
    }
}
