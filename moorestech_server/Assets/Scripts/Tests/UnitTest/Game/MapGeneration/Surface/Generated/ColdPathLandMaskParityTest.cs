using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Transfer;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Spawn;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Surface.Generated
{
    // 表示キャッシュを取り逃したクライアントが原点注入configで作り直す陸マスクが、生成時の陸マスクと一致することを検証する
    // Verifies the land mask a client rebuilds from the settled-origins config on a visual-cache miss equals the generation-time one
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardServerMap2")]
    public class ColdPathLandMaskParityTest
    {
        private const int Seed = 12345;
        private const float LandThreshold = 0.35f;
        private const int GridSide = 3;

        [Test]
        public void 原点注入で再生成した表示高さ方針は生成時と同じ陸マスクを持つ()
        {
            // 生成時はスポーン探索が原点を確定させる。クライアントは保存した原点をconfigへ注入して同じpass-1を回す
            // At generation the spawn search settles the origins; the client injects the saved origins and reruns the same pass-1
            // テスト既定の閾値0は全面陸になり陸マスクの差が見えないので、本番modと同じ閾値で海を残す
            // The test default threshold of 0 makes everything land and hides mask differences, so keep the mod's threshold to leave sea
            // 格子は探索が成立する最小の3x3に絞り、CIで数秒台に収める
            // Shrink the grid to the smallest 3x3 the search still accepts so CI stays within seconds
            var overrides = new JObject { ["landThreshold"] = LandThreshold, ["gridSizeX"] = GridSide, ["gridSizeZ"] = GridSide };
            var generation = SpawnSearchTestWorld.CreateGeneration(TestGenerationConfigFactory.SpawnSearchSetup.Enabled, overrides);
            var sourceConfig = MapGenerationPipeline.BuildConfig(
                generation, Seed, TestGenerationConfigFactory.ServerDataDirectory, WorldGeneratorVersion.CurrentRevision);
            var generated = MapGenerationPipeline.Generate(generation, sourceConfig);
            Assert.That(new Vector2(generated.Config.worldOffsetX, generated.Config.worldOffsetZ),
                Is.Not.EqualTo(new Vector2(sourceConfig.worldOffsetX, sourceConfig.worldOffsetZ)), "fixture: the search moved the origins");
            var savedOrigins = new TerrainOrigins(generated.Output.NoiseOrigin, generated.Output.SceneOrigin);
            var coldConfig = MapGenerationPipeline.BuildConfigWithSettledOrigins(generation, Seed,
                TestGenerationConfigFactory.ServerDataDirectory, savedOrigins, WorldGeneratorVersion.Current, "cold-path-world");
            var regenerated = MapGenerationPipeline.Generate(generation, coldConfig);
            Assert.That(regenerated.Ledger.ComputeDigest(), Is.EqualTo(generated.Ledger.ComputeDigest()));

            // 全面海抜0の探り高さへ両方の方針を当てる。陸保護の床が立つ頂点の集合が陸マスクそのものを映す
            // Apply both policies to an all-zero probe; the vertices lifted to the land floor mirror the land mask itself
            var config = generated.Config;
            int protectedCount = 0;
            int unprotectedCount = 0;
            for (int tileZ = 0; tileZ < config.gridSizeZ; tileZ++)
            for (int tileX = 0; tileX < config.gridSizeX; tileX++)
            {
                var tileConfig = config.CreateTileConfig(tileX, tileZ);
                var tile = config.TileScenePosition(tileX, tileZ);
                var scene = new Vector3(tile.x, 0f, tile.y);
                var probe = new float[tileConfig.Resolution, tileConfig.Resolution];
                var expected = generated.DisplayHeightPolicy.Apply(probe, tileConfig, scene, generated.Ledger);
                var actual = regenerated.DisplayHeightPolicy.Apply(probe, tileConfig, scene, regenerated.Ledger);
                Assert.That(actual, Is.EqualTo(expected), $"tile ({tileX},{tileZ})");
                foreach (var height in expected)
                    if (0f < height) protectedCount++;
                    else unprotectedCount++;
            }

            // 陸と海が両方ある窓でなければ一致は何も示さない
            // Equality proves nothing unless the window holds both land and sea
            Assert.That(protectedCount, Is.GreaterThan(0), "fixture: the window holds land");
            Assert.That(unprotectedCount, Is.GreaterThan(0), "fixture: the window holds sea");
        }
    }
}
