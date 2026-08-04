using System;
using Game.MapGeneration.Pipeline;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration
{
    public class SpawnBoundaryTest
    {
        private const int Seed = 12345;

        [Test]
        public void 偶数gridSizeで格子中心がタイル外へ落ちるならワールド生成を落とす()
        {
            var generation = TestGenerationConfigFactory.CreateWithAlgorithmParamOverrides(
                TestGenerationConfigFactory.SpawnSearchSetup.Enabled,
                new JObject { ["gridSizeX"] = 4, ["gridSizeZ"] = 4 });

            var exception = Assert.Throws<InvalidOperationException>(() => MapGenerationPipeline.Generate(generation, Seed));
            StringAssert.Contains("spawn target", exception.Message);
        }

        [Test]
        public void 探索無効時の地形外スポーンはワールド生成を落とす()
        {
            var generation = TestGenerationConfigFactory.CreateWithAlgorithmParamOverrides(
                TestGenerationConfigFactory.SpawnSearchSetup.Disabled,
                new JObject { ["spawnWorldPosition"] = new JArray(2116.69922, -807.6172) });

            var exception = Assert.Throws<InvalidOperationException>(() => MapGenerationPipeline.Generate(generation, Seed));
            StringAssert.Contains("scene spawn", exception.Message);
        }

        [Test]
        public void 探索無効時の地形角スポーンはワールド生成を落とす()
        {
            var generation = TestGenerationConfigFactory.CreateWithAlgorithmParamOverrides(
                TestGenerationConfigFactory.SpawnSearchSetup.Disabled,
                new JObject { ["spawnWorldPosition"] = new JArray(0, 0) });

            var exception = Assert.Throws<InvalidOperationException>(() => MapGenerationPipeline.Generate(generation, Seed));
            StringAssert.Contains("scene spawn", exception.Message);
        }
    }
}
