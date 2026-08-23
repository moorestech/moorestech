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
            // 意図的に既定の5x5から外れ4x4を指定する。この上書きは明示済みなので helper に握り潰されない
            // Intentionally deviates from the default 5x5 to 4x4; the helper leaves an explicit override like this untouched
            var generation = SpawnSearchTestWorld.CreateGeneration(
                TestGenerationConfigFactory.SpawnSearchSetup.Enabled,
                new JObject { ["gridSizeX"] = 4, ["gridSizeZ"] = 4 });

            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                var runtimeConfig = MapGenerationPipeline.BuildConfig(generation, Seed, TestGenerationConfigFactory.ServerDataDirectory);
                MapGenerationPipeline.Generate(generation, runtimeConfig);
            });
            StringAssert.Contains("spawn target", exception.Message);
        }

        [Test]
        public void 探索無効時の地形外スポーンはワールド生成を落とす()
        {
            var generation = SpawnSearchTestWorld.CreateGeneration(
                TestGenerationConfigFactory.SpawnSearchSetup.Disabled,
                new JObject { ["spawnWorldPosition"] = new JArray(2116.69922, -807.6172) });

            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                var runtimeConfig = MapGenerationPipeline.BuildConfig(generation, Seed, TestGenerationConfigFactory.ServerDataDirectory);
                MapGenerationPipeline.Generate(generation, runtimeConfig);
            });
            StringAssert.Contains("scene spawn", exception.Message);
        }

        [Test]
        public void 探索無効時の地形角スポーンはワールド生成を落とす()
        {
            var generation = SpawnSearchTestWorld.CreateGeneration(
                TestGenerationConfigFactory.SpawnSearchSetup.Disabled,
                new JObject { ["spawnWorldPosition"] = new JArray(0, 0) });

            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                var runtimeConfig = MapGenerationPipeline.BuildConfig(generation, Seed, TestGenerationConfigFactory.ServerDataDirectory);
                MapGenerationPipeline.Generate(generation, runtimeConfig);
            });
            StringAssert.Contains("scene spawn", exception.Message);
        }
    }
}
