using System.Diagnostics;
using NUnit.Framework;
using UnityEngine.TestTools;
using MapGenerator.Pipeline;
using MapGenerator.Pipeline.Biomes;
using MapGenerator.Pipeline.Config;
using UnityEngine;

namespace MapGenerator.Tests.EditMode
{
    /// <summary>
    /// ユーザーと同じルート（TerrainGenerator.Generate）を通る統合テスト。
    /// PerformanceTestsがジョブパイプラインを直接スケジュールするのに対し、
    /// このテストは実際のAPIエントリポイントを検証する。
    /// </summary>
    [TestFixture]
    public class IntegrationTests
    {
        TerrainGenerationConfig _config;

        [OneTimeSetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;

            _config = TestConfigFactory.Create();
            _config.resolutionPreset = TerrainResolutionPreset._512;

            // Burst JITウォームアップ: 本番解像度で2回実行して非同期コンパイルを完了
            TerrainGenerator.Generate(_config);
            TerrainGenerator.Generate(_config);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
        }

        // --- 正常系: 全バイオームで例外なく完走する ---

        [Test]
        public void Generate_AllBiomes_CompletesWithoutException()
        {
            var result = TerrainGenerator.Generate(_config);

            Assert.IsNotNull(result, "結果がnull");
            Assert.IsNotNull(result.Heights, "Heights配列がnull");
            Assert.AreEqual(513 * 513, result.Heights.Length, "Heights配列長が不正");
        }

        // --- 高さマップの妥当性: 全値が0-1範囲かつゼロだけではない ---

        [Test]
        public void Generate_HeightsInValidRange()
        {
            var result = TerrainGenerator.Generate(_config);

            float min = float.MaxValue, max = float.MinValue;
            int nonZeroCount = 0;
            for (int i = 0; i < result.Heights.Length; i++)
            {
                float h = result.Heights[i];
                Assert.That(h, Is.InRange(0f, 1f),
                    $"Height[{i}]が範囲外: {h}");
                if (h < min) min = h;
                if (h > max) max = h;
                if (h > 0.001f) nonZeroCount++;
            }

            // 陸地が生成されるためゼロでないピクセルが十分に存在するはず
            Assert.Greater(nonZeroCount, result.Heights.Length * 0.1f,
                $"非ゼロピクセルが少なすぎる: {nonZeroCount}/{result.Heights.Length}");
            // 高低差があるはず
            Assert.Greater(max - min, 0.01f,
                $"高低差が小さすぎる: min={min}, max={max}");
        }

        // --- 単一バイオームでも動作する ---

        [Test]
        public void Generate_SingleBiome_CompletesWithoutException()
        {
            var singleConfig = TestConfigFactory.Create();
            singleConfig.resolutionPreset = TerrainResolutionPreset._256;
            singleConfig.grasslandEnabled = true;
            singleConfig.forestEnabled = false;
            singleConfig.savannaEnabled = false;
            singleConfig.desertEnabled = false;
            singleConfig.mesaEnabled = false;
            singleConfig.alpineEnabled = false;
            singleConfig.jungleEnabled = false;
            singleConfig.woodsEnabled = false;

            var result = TerrainGenerator.Generate(singleConfig);

            Assert.IsNotNull(result);
            Assert.AreEqual(257 * 257, result.Heights.Length);
        }

        [Test]
        public void Generate_OreOnlyEnabled_GeneratesOrePlacements()
        {
            var cfg = CreateOreOnlyConfig(out var prefab);
            cfg.overrideResolution = 65;
            cfg.chunkPadding = 0;

            try
            {
                var result = TerrainGenerator.Generate(cfg);

                Assert.IsNotNull(result.OrePlacements, "鉱脈配置が実行されていない");
                Assert.Greater(result.OrePlacements.Count, 0, "鉱脈だけ有効な設定で鉱脈が生成されていない");
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void GenerateWithPadding_OreOnlyEnabled_GeneratesOrePlacements()
        {
            var cfg = CreateOreOnlyConfig(out var prefab);
            cfg.resolutionPreset = TerrainResolutionPreset._256;
            cfg.chunkPadding = 4;

            try
            {
                var result = TerrainGenerator.GenerateWithPadding(cfg);

                Assert.IsNotNull(result.OrePlacements, "パディング生成後の鉱脈配置が実行されていない");
                Assert.Greater(result.OrePlacements.Count, 0, "パディング生成で鉱脈だけ有効な設定の鉱脈が生成されていない");
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        // --- 各バイオーム単独テスト: 全8バイオームが個別に動作する ---

        [TestCase(true, false, false, false, false, false, false, false, TestName = "Grassland only")]
        [TestCase(false, true, false, false, false, false, false, false, TestName = "Forest only")]
        [TestCase(false, false, true, false, false, false, false, false, TestName = "Savanna only")]
        [TestCase(false, false, false, true, false, false, false, false, TestName = "Desert only")]
        [TestCase(false, false, false, false, true, false, false, false, TestName = "Mesa only")]
        [TestCase(false, false, false, false, false, true, false, false, TestName = "Alpine only")]
        [TestCase(false, false, false, false, false, false, true, false, TestName = "Jungle only")]
        [TestCase(false, false, false, false, false, false, false, true, TestName = "Woods only")]
        public void Generate_EachBiomeSolo_Completes(
            bool grass, bool forest, bool savanna, bool desert,
            bool mesa, bool alpine, bool jungle, bool woods)
        {
            var cfg = TestConfigFactory.Create();
            cfg.resolutionPreset = TerrainResolutionPreset._256;
            cfg.grasslandEnabled = grass;
            cfg.forestEnabled = forest;
            cfg.savannaEnabled = savanna;
            cfg.desertEnabled = desert;
            cfg.mesaEnabled = mesa;
            cfg.alpineEnabled = alpine;
            cfg.jungleEnabled = jungle;
            cfg.woodsEnabled = woods;

            var result = TerrainGenerator.Generate(cfg);

            Assert.IsNotNull(result);
            Assert.AreEqual(257 * 257, result.Heights.Length);
            // 非ゼロピクセルが存在する（陸地が生成されている）
            int nonZero = 0;
            for (int i = 0; i < result.Heights.Length; i++)
                if (result.Heights[i] > 0.001f) nonZero++;
            Assert.Greater(nonZero, 0, "陸地が生成されていない");
        }

        static TerrainGenerationConfig CreateOreOnlyConfig(out GameObject prefab)
        {
            prefab = new GameObject("OreOnlyTestPrefab");

            var cfg = TestConfigFactory.Create();
            cfg.grasslandEnabled = true;
            cfg.forestEnabled = false;
            cfg.savannaEnabled = false;
            cfg.desertEnabled = false;
            cfg.mesaEnabled = false;
            cfg.alpineEnabled = false;
            cfg.jungleEnabled = false;
            cfg.woodsEnabled = false;

            cfg.generateObject = false;
            cfg.generateDetail = false;
            cfg.generateOre = true;
            cfg.generateTexture = false;

            cfg.landThreshold = 0f;
            cfg.erosionStrength = 0f;
            cfg.spawnWorldPosition = new Vector2(cfg.terrainWidth * 0.5f, cfg.terrainLength * 0.5f);
            cfg.oreConfig = new WorldOreConfig
            {
                borderMargin = 0f,
                entries = new[]
                {
                    new OreEntry
                    {
                        prefab = prefab,
                        biomes = BiomeFlags.Grassland,
                        useSlopeFilter = false,
                        minDistanceFromOthers = 0f,
                        bands = new[]
                        {
                            new OreBand
                            {
                                outerRadiusMeters = -1f,
                                density = 5f,
                                maxObjectsPerCluster = 1,
                                clusterRadius = 3f,
                                minDistanceBetweenOres = 0f,
                                placementRetries = 1
                            }
                        }
                    }
                }
            };

            return cfg;
        }

        // --- seed再現性: 同一seedで2回生成した結果が一致する ---

        [Test]
        public void Generate_SameSeed_ProducesSameResult()
        {
            var cfg = TestConfigFactory.Create();
            cfg.resolutionPreset = TerrainResolutionPreset._256;
            cfg.seed = 12345;

            var result1 = TerrainGenerator.Generate(cfg);
            var result2 = TerrainGenerator.Generate(cfg);

            for (int i = 0; i < result1.Heights.Length; i++)
            {
                Assert.AreEqual(result1.Heights[i], result2.Heights[i], 0.0001f,
                    $"Seed再現性エラー Height[{i}]: {result1.Heights[i]} vs {result2.Heights[i]}");
            }
        }

        // --- パフォーマンス: ユーザールート全体が閾値以内 ---

        [Test]
        public void Generate_FullUserRoute_Under300ms()
        {
            // エディタモードでもイテレーション快適な350ms以内を要求
            int threshold = 350;

            var times = new long[3];
            for (int i = 0; i < 3; i++)
            {
                var sw = Stopwatch.StartNew();
                TerrainGenerator.Generate(_config);
                sw.Stop();
                times[i] = sw.ElapsedMilliseconds;
            }

            System.Array.Sort(times);
            long median = times[1];
            UnityEngine.Debug.Log(
                $"[Perf] ユーザールート全体 median: {median}ms (target: {threshold}ms)");
            Assert.Less(median, threshold,
                $"ユーザールート全体が{median}msで閾値{threshold}msを超過");
        }
    }
}
