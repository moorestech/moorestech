using System.Text.RegularExpressions;
using Game.MapGeneration.Pipeline;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.MapGeneration
{
    // 探索無効とフォールバックはどちらもオフセット0を返すため、出力だけでは経路を区別できない。
    // Disabled search and fallback both yield a zero offset, so the outputs alone cannot tell the paths apart.
    public class SpawnSearchDiagnosticsLogTest
    {
        private const int Seed = 12345;

        [Test]
        public void 探索無効でも診断ログが1行残る()
        {
            var generation = SpawnSearchTestWorld.CreateGeneration(
                TestGenerationConfigFactory.SpawnSearchSetup.Disabled);

            // 既定値 false の主要経路。ここが無言だとフォールバックと同一に見える（ADR#13）
            // This is the default-false main path; staying silent here makes it look identical to the fallback (ADR#13)
            LogAssert.Expect(LogType.Log, new Regex(@"^\[SpawnSearch\] 探索無効（useSpawnOffsetSearch=false）$"));

            var runtimeConfig = MapGenerationPipeline.BuildConfig(generation, Seed, TestGenerationConfigFactory.ServerDataDirectory);
            MapGenerationPipeline.Generate(generation, runtimeConfig);
        }
    }
}
