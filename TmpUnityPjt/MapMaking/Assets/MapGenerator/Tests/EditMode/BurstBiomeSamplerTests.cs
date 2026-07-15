using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using MapGenerator.Pipeline;
using MapGenerator.Pipeline.Biomes;
using MapGenerator.Pipeline.Jobs;

namespace MapGenerator.Tests.EditMode
{
    /// <summary>
    /// BurstBiomeSamplerの全8バイオームについて、出力が妥当な範囲に収まることを検証する。
    /// 各テストはデフォルトconfigパラメータを使い、50点サンプリングで範囲チェックを行う。
    /// </summary>
    [TestFixture]
    public class BurstBiomeSamplerTests
    {
        NativeArray<float2> _offsets;

        [SetUp]
        public void SetUp()
        {
            // 全バイオームの最大オフセット需要をカバーする十分な容量を確保
            _offsets = new NativeArray<float2>(64, Allocator.Temp);
            var rng = new System.Random(42);
            for (int i = 0; i < 64; i++)
                _offsets[i] = new float2(
                    (float)rng.NextDouble() * 10000f,
                    (float)rng.NextDouble() * 10000f);
        }

        [TearDown]
        public void TearDown() => _offsets.Dispose();

        // --- Grassland ---

        [Test]
        public void SampleGrassland_OutputInReasonableRange()
        {
            var p = CreateGrasslandParams();
            for (int i = 0; i < 50; i++)
            {
                float h = BurstBiomeSampler.Sample(p.biomeType,
                    new float2(i * 50f, i * 70f), p, _offsets, p.noiseOffsetBase);
                Assert.That(h, Is.InRange(-0.1f, 1.5f),
                    $"Grassland height out of range at {i}: {h}");
            }
        }

        [Test]
        public void SampleGrassland_IsDeterministic()
        {
            var p = CreateGrasslandParams();
            float2 pos = new float2(100f, 200f);
            float a = BurstBiomeSampler.Sample(p.biomeType, pos, p, _offsets, p.noiseOffsetBase);
            float b = BurstBiomeSampler.Sample(p.biomeType, pos, p, _offsets, p.noiseOffsetBase);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void SampleGrassland_UsesTwoStagePerlinHeightParameters()
        {
            var simple = CreateGrasslandParams();
            var complex = simple;
            complex.domainWarpStrength = 10000f;
            complex.domainWarpIterations = 4;
            complex.domainWarpOctaves = 5;
            complex.terraceEnabled = 1;
            complex.terraceSteps = 8;
            complex.terraceSharpness = 1f;
            complex.terraceHeight = 1f;
            complex.canyonEnabled = 1;
            complex.canyonDepth = 1f;
            complex.plateauFlatten = 1f;
            complex.exponent = 3f;
            complex.ridgeBlend = 2f;

            float2 pos = new float2(321f, 654f);
            float actual = BurstBiomeSampler.Sample(simple.biomeType, pos, simple, _offsets, simple.noiseOffsetBase);
            float withComplexParams = BurstBiomeSampler.Sample(complex.biomeType, pos, complex, _offsets, complex.noiseOffsetBase);
            float basePerlin = (noise.cnoise(pos * simple.frequency + _offsets[simple.noiseOffsetBase]) + 1f) * 0.5f;
            float detailPerlin = (noise.cnoise(pos * simple.secondaryFrequency + _offsets[simple.noiseOffsetBase + 1]) + 1f) * 0.5f;
            float expectedHeight01 = basePerlin * simple.amplitude
                + (detailPerlin - 0.5f) * simple.secondaryAmplitude;
            float expected = simple.baseHeight + expectedHeight01 * simple.hillAmplitude;

            Assert.AreEqual(expected, actual, 0.000001f);
            Assert.AreEqual(actual, withComplexParams, 0.000001f);
        }

        [Test]
        public void GrasslandBiomeConfig_ExposesOnlySimplePerlinHeightFields()
        {
            var fields = typeof(GrasslandBiomeConfig)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(f => f.Name);

            Assert.That(fields, Is.EquivalentTo(new[]
            {
                "frequency",
                "amplitude",
                "detailFrequency",
                "detailAmplitude",
                "baseHeight",
                "hillAmplitude",
                "terrainLayer",
                "textureConfig",
                "treePlacement",
                "objectConfig",
                "detailConfig",
                "shoreConfig",
                "boundaryConfig",
            }));
        }

        [Test]
        public void GrasslandNoiseOffsets_ReserveTwoPerlinOffsets()
        {
            var config = TestConfigFactory.Create();

            int count = JobDataConverter.GetNoiseOffsetCount(config, BiomeType.Grassland);

            Assert.AreEqual(2, count);
        }

        // --- Forest ---

        [Test]
        public void SampleForest_OutputInReasonableRange()
        {
            var p = CreateForestParams();
            for (int i = 0; i < 50; i++)
            {
                float h = BurstBiomeSampler.Sample(p.biomeType,
                    new float2(i * 50f, i * 70f), p, _offsets, p.noiseOffsetBase);
                Assert.That(h, Is.InRange(-0.1f, 1.5f),
                    $"Forest height out of range at {i}: {h}");
            }
        }

        [Test]
        public void SampleForest_IsDeterministic()
        {
            var p = CreateForestParams();
            float2 pos = new float2(300f, 400f);
            float a = BurstBiomeSampler.Sample(p.biomeType, pos, p, _offsets, p.noiseOffsetBase);
            float b = BurstBiomeSampler.Sample(p.biomeType, pos, p, _offsets, p.noiseOffsetBase);
            Assert.AreEqual(a, b);
        }

        // --- Savanna ---

        [Test]
        public void SampleSavanna_OutputInReasonableRange()
        {
            var p = CreateSavannaParams();
            for (int i = 0; i < 50; i++)
            {
                float h = BurstBiomeSampler.Sample(p.biomeType,
                    new float2(i * 50f, i * 70f), p, _offsets, p.noiseOffsetBase);
                Assert.That(h, Is.InRange(-0.1f, 1.5f),
                    $"Savanna height out of range at {i}: {h}");
            }
        }

        [Test]
        public void SampleSavanna_IsDeterministic()
        {
            var p = CreateSavannaParams();
            float2 pos = new float2(500f, 600f);
            float a = BurstBiomeSampler.Sample(p.biomeType, pos, p, _offsets, p.noiseOffsetBase);
            float b = BurstBiomeSampler.Sample(p.biomeType, pos, p, _offsets, p.noiseOffsetBase);
            Assert.AreEqual(a, b);
        }

        // --- Desert ---

        [Test]
        public void SampleDesert_OutputInReasonableRange()
        {
            var p = CreateDesertParams();
            for (int i = 0; i < 50; i++)
            {
                float h = BurstBiomeSampler.Sample(p.biomeType,
                    new float2(i * 50f, i * 70f), p, _offsets, p.noiseOffsetBase);
                Assert.That(h, Is.InRange(-0.1f, 1.5f),
                    $"Desert height out of range at {i}: {h}");
            }
        }

        [Test]
        public void SampleDesert_IsDeterministic()
        {
            var p = CreateDesertParams();
            float2 pos = new float2(700f, 800f);
            float a = BurstBiomeSampler.Sample(p.biomeType, pos, p, _offsets, p.noiseOffsetBase);
            float b = BurstBiomeSampler.Sample(p.biomeType, pos, p, _offsets, p.noiseOffsetBase);
            Assert.AreEqual(a, b);
        }

        // --- Mesa ---

        [Test]
        public void SampleMesa_OutputInReasonableRange()
        {
            var p = CreateMesaParams();
            for (int i = 0; i < 50; i++)
            {
                float h = BurstBiomeSampler.Sample(p.biomeType,
                    new float2(i * 50f, i * 70f), p, _offsets, p.noiseOffsetBase);
                Assert.That(h, Is.InRange(-0.1f, 1.5f),
                    $"Mesa height out of range at {i}: {h}");
            }
        }

        [Test]
        public void SampleMesa_IsDeterministic()
        {
            var p = CreateMesaParams();
            float2 pos = new float2(900f, 1000f);
            float a = BurstBiomeSampler.Sample(p.biomeType, pos, p, _offsets, p.noiseOffsetBase);
            float b = BurstBiomeSampler.Sample(p.biomeType, pos, p, _offsets, p.noiseOffsetBase);
            Assert.AreEqual(a, b);
        }

        // --- Alpine ---

        [Test]
        public void SampleAlpine_OutputInReasonableRange()
        {
            var p = CreateAlpineParams();
            for (int i = 0; i < 50; i++)
            {
                float h = BurstBiomeSampler.Sample(p.biomeType,
                    new float2(i * 50f, i * 70f), p, _offsets, p.noiseOffsetBase);
                Assert.That(h, Is.InRange(-0.1f, 1.5f),
                    $"Alpine height out of range at {i}: {h}");
            }
        }

        [Test]
        public void SampleAlpine_IsDeterministic()
        {
            var p = CreateAlpineParams();
            float2 pos = new float2(1100f, 1200f);
            float a = BurstBiomeSampler.Sample(p.biomeType, pos, p, _offsets, p.noiseOffsetBase);
            float b = BurstBiomeSampler.Sample(p.biomeType, pos, p, _offsets, p.noiseOffsetBase);
            Assert.AreEqual(a, b);
        }

        // --- Jungle ---

        [Test]
        public void SampleJungle_OutputInReasonableRange()
        {
            var p = CreateJungleParams();
            for (int i = 0; i < 50; i++)
            {
                float h = BurstBiomeSampler.Sample(p.biomeType,
                    new float2(i * 50f, i * 70f), p, _offsets, p.noiseOffsetBase);
                Assert.That(h, Is.InRange(-0.1f, 1.5f),
                    $"Jungle height out of range at {i}: {h}");
            }
        }

        [Test]
        public void SampleJungle_IsDeterministic()
        {
            var p = CreateJungleParams();
            float2 pos = new float2(1300f, 1400f);
            float a = BurstBiomeSampler.Sample(p.biomeType, pos, p, _offsets, p.noiseOffsetBase);
            float b = BurstBiomeSampler.Sample(p.biomeType, pos, p, _offsets, p.noiseOffsetBase);
            Assert.AreEqual(a, b);
        }

        // --- Woods ---

        [Test]
        public void SampleWoods_OutputInReasonableRange()
        {
            var p = CreateWoodsParams();
            for (int i = 0; i < 50; i++)
            {
                float h = BurstBiomeSampler.Sample(p.biomeType,
                    new float2(i * 50f, i * 70f), p, _offsets, p.noiseOffsetBase);
                Assert.That(h, Is.InRange(-0.1f, 1.5f),
                    $"Woods height out of range at {i}: {h}");
            }
        }

        [Test]
        public void SampleWoods_IsDeterministic()
        {
            var p = CreateWoodsParams();
            float2 pos = new float2(1500f, 1600f);
            float a = BurstBiomeSampler.Sample(p.biomeType, pos, p, _offsets, p.noiseOffsetBase);
            float b = BurstBiomeSampler.Sample(p.biomeType, pos, p, _offsets, p.noiseOffsetBase);
            Assert.AreEqual(a, b);
        }

        // --- Unknown biome type returns 0 ---

        [Test]
        public void SampleUnknown_ReturnsZero()
        {
            var p = CreateGrasslandParams();
            float h = BurstBiomeSampler.Sample(99, new float2(100f, 200f), p, _offsets, 0);
            Assert.AreEqual(0f, h);
        }

        // =====================================================================
        // パラメータファクトリ: 各BiomeConfigのデフォルト値をBiomeParamsに変換
        // JobDataConverter.Fill*と同一のマッピングを手動で再現
        // =====================================================================

        static BiomeParams CreateGrasslandParams()
        {
            return new BiomeParams
            {
                enabled = 1,
                biomeType = 2,
                baseHeight = 0.03f,
                hillAmplitude = 0.02f,
                frequency = 0.0005f,
                amplitude = 1f,
                secondaryFrequency = 0.02f,
                secondaryAmplitude = 0.08f,
                noiseOffsetBase = 0,
                noiseOffsetCount = 2,
            };
        }

        static BiomeParams CreateForestParams()
        {
            return new BiomeParams
            {
                enabled = 1,
                biomeType = 3,
                baseHeight = 0.06f,
                hillAmplitude = 0.05f + 0.12f,
                frequency = 0.0008f,
                octaves = 4,
                persistence = 0.5f,
                lacunarity = 2f,
                exponent = 0.7f,
                // BurstBiomeSampler用副次パラメータ
                secondaryFrequency = 0.002f,
                secondaryAmplitude = 0.05f,
                noiseOffsetBase = 0,
                noiseOffsetCount = 4,
            };
        }

        static BiomeParams CreateSavannaParams()
        {
            return new BiomeParams
            {
                enabled = 1,
                biomeType = 4,
                baseHeight = 0.03f,
                hillAmplitude = 0.18f,
                frequency = 0.0015f,
                octaves = 4,
                persistence = 0.5f,
                lacunarity = 2f,
                // Savanna固有: 丘陵閾値と個別振幅
                hillThreshold = 0.55f,
                secondaryAmplitude = 0.05f,
                noiseOffsetBase = 0,
                noiseOffsetCount = 7,
            };
        }

        static BiomeParams CreateDesertParams()
        {
            return new BiomeParams
            {
                enabled = 1,
                biomeType = 5,
                baseHeight = 0.03f,
                hillAmplitude = 0.02f + 0.15f,
                frequency = 0.003f,
                octaves = 3,
                persistence = 0.5f,
                lacunarity = 2f,
                canyonDepth = 0.6f,
                canyonFreqMult = 0.001f / 0.003f,
                canyonOctaves = 4,
                ridgeBlend = 0.15f,
                ridgeOctaves = 4,
                // Desert固有: 砂丘振幅と崖周波数
                secondaryAmplitude = 0.02f,
                secondaryFrequency = 0.0012f,
                noiseOffsetBase = 0,
                noiseOffsetCount = 11,
            };
        }

        static BiomeParams CreateMesaParams()
        {
            return new BiomeParams
            {
                enabled = 1,
                biomeType = 6,
                baseHeight = 0.05f,
                hillAmplitude = 0.25f,
                frequency = 0.0018f,
                octaves = 5,
                persistence = 0.45f,
                lacunarity = 2f,
                domainWarpStrength = 400f,
                domainWarpIterations = 1,
                canyonDepth = 0.3f,
                canyonFreqMult = 2f,
                canyonOctaves = 3,
                terraceEnabled = 1,
                terraceSteps = 4,
                terraceSharpness = 0.6f,
                plateauFlatten = 0.5f,
                exponent = 1.3f,
                noiseOffsetBase = 0,
                noiseOffsetCount = 15,
            };
        }

        static BiomeParams CreateAlpineParams()
        {
            return new BiomeParams
            {
                enabled = 1,
                biomeType = 7,
                baseHeight = 0.06f,
                hillAmplitude = 0.60f,
                frequency = 0.0010f,
                octaves = 3,
                persistence = 0.45f,
                lacunarity = 2f,
                domainWarpStrength = 4f,
                domainWarpIterations = 1,
                ridgeBlend = 0.64f,
                ridgeOctaves = 5,
                exponent = 1.72f,
                noiseOffsetBase = 0,
                noiseOffsetCount = 25,
            };
        }

        static BiomeParams CreateJungleParams()
        {
            return new BiomeParams
            {
                enabled = 1,
                biomeType = 8,
                baseHeight = 0.05f,
                hillAmplitude = 0.2f,
                frequency = 0.01f,
                octaves = 1,
                terraceSteps = 7,
                terraceSharpness = 0.293f,
                domainWarpStrength = 30f,
                ridgeBlend = 0f,
                // 境界スロープ: width / repeat / coverage（1段差のみ適用）
                absSmoothing = 0.831f,
                secondaryFrequency = 1.3f,
                secondaryAmplitude = 1f,
                plateauFlatten = 1f,
                exponent = 0.03f,
                noiseOffsetBase = 0,
                noiseOffsetCount = 5, // warp(1*2) + voronoi(1) + surfaceDetail(2)
            };
        }

        static BiomeParams CreateWoodsParams()
        {
            return new BiomeParams
            {
                enabled = 1,
                biomeType = 9,
                baseHeight = 0.05f,
                hillAmplitude = 0.15f,
                frequency = 0.0012f,
                octaves = 4,
                persistence = 0.5f,
                lacunarity = 2f,
                terraceEnabled = 1,
                terraceSteps = 5,
                terraceSharpness = 0.7f,
                noiseOffsetBase = 0,
                noiseOffsetCount = 4,
            };
        }
    }
}
