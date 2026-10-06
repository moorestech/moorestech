using Tests.Module.TestMod;
using Mod.Loader;
using Mod.Config;
using System.IO;
using Core.Master;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Surface.Generated;

namespace Client.Tests.Map.Surface
{
    [Category("IgnoreCI")]
    public class GeneratedSurfaceRuntimeTest
    {
        [TearDown]
        public void TearDown()
        {
            // 本番マスタを後続テストへ残さず標準テスト入力へ戻す
            // Restore standard test inputs so production masters do not leak into later tests
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(
                new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")))));
        }

        [Test]
        [Category("HeavyGeneratedSurfaceRuntime")]
        [Timeout(1500000)]
        public void GeneratedFinalTerrainGroundsEveryProductionOutcropMesh()
        {
            // 本番生成・保存・最終木加工を通して実TerrainDataを検査する
            // Inspect actual TerrainData after production generation, saving and final tree processing
            using var generated = new GeneratedSurfaceFixture(196, 3, 1000f, 1000f);
            using var runtime = new RuntimeSurfaceFixture(generated, generated.BakeReload(true));
            runtime.AssertEveryVein();
        }
    }
}
