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
            // 本番マスタを標準入力へ戻す
            // Restore standard inputs so production masters do not leak
            MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(
                new ModsResource(Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods")))));
        }

        [Test]
        [Category("HeavyGeneratedSurfaceRuntime")]
        [Timeout(1500000)]
        public void GeneratedFinalTerrainGroundsEveryProductionOutcropMesh()
        {
            // 本番生成後のTerrainData検査
            // Inspect real TerrainData from production generation
            using var generated = new GeneratedSurfaceFixture(196, 3, 1000f, 1000f);
            using var runtime = new RuntimeSurfaceFixture(generated, generated.BakeReload(true));
            runtime.AssertEveryVein();
        }
    }
}
