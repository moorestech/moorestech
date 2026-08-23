using Game.MapGeneration.Pipeline;
using Mooresmaster.Model.GenerationModule;
using NUnit.Framework;
using UnityEngine;

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
            var runtimeConfig = MapGenerationPipeline.BuildConfig(generation, 12345, TestGenerationConfigFactory.ServerDataDirectory);
            var output = MapGenerationPipeline.Generate(generation, runtimeConfig).Output;

            Assert.That(output.FluidVeins, Is.Not.Empty);

            // 鉱脈はシーン座標で出るので、範囲は master の worldOffset ではなく生成が確定させた格子から取る。
            // Veins come out in scene space, so bound them by the grid generation settled on, not the master worldOffset.
            var vp = (VanillaGeneratorAlgorithmParam)generation.AlgorithmParam;
            int minWorldX = (int)output.SceneOrigin.x;
            int maxWorldX = (int)(output.SceneOrigin.x + vp.GridSizeX * vp.TerrainWidth);
            int minWorldZ = (int)output.SceneOrigin.y;
            int maxWorldZ = (int)(output.SceneOrigin.y + vp.GridSizeZ * vp.TerrainLength);
            int maxWorldY = (int)vp.TerrainHeight;

            // 鉱脈は配置点から±1広がるため、格子の外へ1ブロックはみ出しうる。
            // A vein reaches one unit out from its point, so it can overhang the grid by one block.
            const int margin = TestGenerationConfigFactory.VeinGridOverhang;

            foreach (var vein in output.FluidVeins)
            {
                Assert.That(vein.VeinGuid, Is.EqualTo(TestGenerationConfigFactory.TestFluidVeinGuid));

                // AABBはXZ3セル・Y1セルの固定サイズ（ADR-0023、VeinAabbBuilder.Extent）
                // The AABB is a fixed 3x1x3: three cells across XZ and a single cell in Y (ADR-0023, VeinAabbBuilder.Extent)
                Assert.That(vein.Max - vein.Min, Is.EqualTo(new Vector3Int(2, 0, 2)));

                Assert.That(vein.Min.x, Is.GreaterThanOrEqualTo(minWorldX - margin));
                Assert.That(vein.Max.x, Is.LessThanOrEqualTo(maxWorldX + margin));
                Assert.That(vein.Min.z, Is.GreaterThanOrEqualTo(minWorldZ - margin));
                Assert.That(vein.Max.z, Is.LessThanOrEqualTo(maxWorldZ + margin));
                Assert.That(vein.Min.y, Is.GreaterThanOrEqualTo(-margin));
                Assert.That(vein.Max.y, Is.LessThanOrEqualTo(maxWorldY + margin));
            }
        }
    }
}
