using Game.MapGeneration.Pipeline;
using Mooresmaster.Model.GenerationModule;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration
{
    // FluidVeinPlacementStageがOrePlacementStageと同じ配置ロジックで流体鉱脈を生成し、
    // GUIDが設定値と一致・AABBが地形範囲内に収まることを検証する。
    // Verify FluidVeinPlacementStage places fluid veins via the same logic as OrePlacementStage,
    // with the configured GUID and AABBs bounded within the terrain.
    public class FluidVeinPlacementStageTest
    {
        [Test]
        public void FluidVeinsAreGeneratedWithinTerrainBounds()
        {
            var generation = TestGenerationConfigFactory.CreateSmall();
            var output = MapGenerationPipeline.Generate(generation, 12345);

            Assert.That(output.FluidVeins, Is.Not.Empty);

            var vp = (VanillaGeneratorAlgorithmParam)generation.AlgorithmParam;
            int minWorldX = (int)vp.WorldOffsetX;
            int maxWorldX = (int)(vp.WorldOffsetX + vp.TerrainWidth);
            int minWorldZ = (int)vp.WorldOffsetZ;
            int maxWorldZ = (int)(vp.WorldOffsetZ + vp.TerrainLength);
            int maxWorldY = (int)vp.TerrainHeight;

            foreach (var vein in output.FluidVeins)
            {
                Assert.That(vein.VeinGuid, Is.EqualTo(TestGenerationConfigFactory.TestFluidVeinGuid));

                Assert.That(vein.Min.x, Is.LessThanOrEqualTo(vein.Max.x));
                Assert.That(vein.Min.y, Is.LessThanOrEqualTo(vein.Max.y));
                Assert.That(vein.Min.z, Is.LessThanOrEqualTo(vein.Max.z));

                Assert.That(vein.Min.x, Is.GreaterThanOrEqualTo(minWorldX));
                Assert.That(vein.Max.x, Is.LessThanOrEqualTo(maxWorldX));
                Assert.That(vein.Min.z, Is.GreaterThanOrEqualTo(minWorldZ));
                Assert.That(vein.Max.z, Is.LessThanOrEqualTo(maxWorldZ));
                Assert.That(vein.Min.y, Is.GreaterThanOrEqualTo(0));
                Assert.That(vein.Max.y, Is.LessThanOrEqualTo(maxWorldY));
            }
        }
    }
}
