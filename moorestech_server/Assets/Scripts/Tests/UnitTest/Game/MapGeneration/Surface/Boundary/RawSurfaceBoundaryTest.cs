using System.IO;
using Game.MapGeneration.Pipeline.Runtime;
using Game.MapGeneration.Pipeline.Surface;
using Mooresmaster.Loader.GenerationModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    public class RawSurfaceBoundaryTest
    {
        [TestCase(777)]
        [TestCase(5)]
        [TestCase(12345)]
        public void ReportedUnitMasterSeedsShareRawBoundariesAndReconstructedLand(int seed)
        {
            // 失敗した先焼きテストと同じ条件を使う
            // Use the same conditions as the failing prebake tests
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "generation.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var parameters = (JObject)json["algorithmParam"];
            parameters["gridSizeX"] = 2;
            parameters["gridSizeZ"] = 2;
            parameters["overrideResolution"] = 129;
            parameters["detailResolution"] = 128;
            var config = GenerationRuntimeConfigFactory.Build(GenerationLoader.Load(json));
            config.seed = seed;
            var grid = SurfaceGridBuilder.Build(config);
            int resolution = config.Resolution;
            int stride = resolution - 1;
            for (int i = 0; i < resolution; i++)
            {
                Assert.That(grid.Output.Tiles[0].Heights[i * resolution + stride],
                    Is.EqualTo(grid.Output.Tiles[1].Heights[i * resolution]));
                Assert.That(grid.Output.Tiles[0].Heights[stride * resolution + i],
                    Is.EqualTo(grid.Output.Tiles[2].Heights[i]));
                Assert.That(grid.Output.Tiles[2].Heights[i * resolution + stride],
                    Is.EqualTo(grid.Output.Tiles[3].Heights[i * resolution]));
                Assert.That(grid.Output.Tiles[1].Heights[stride * resolution + i],
                    Is.EqualTo(grid.Output.Tiles[3].Heights[i]));
            }

            // 再ロードは再生成の副産物から陸地を得るので、同じ組み立ての再実行が同じ陸地を返す
            // Reload takes land as a by-product of regeneration, so rerunning the same assembly returns the same land
            var replay = SurfaceGridBuilder.Build(config.ShallowCopy()).Land;
            int protectedVertices = 0;
            for (int z = 0; z < grid.Geometry.Depth; z++)
            for (int x = 0; x < grid.Geometry.Width; x++)
            {
                Assert.That(replay.IsLandVertex(x, z), Is.EqualTo(grid.Land.IsLandVertex(x, z)));
                if (grid.Land.IsProtectedVertex(x, z)) protectedVertices++;
            }
            Assert.That(protectedVertices, Is.GreaterThan(0));
        }
    }
}
