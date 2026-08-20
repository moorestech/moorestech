using System.Linq;
using Game.MapGeneration.Pipeline;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // 生成パイプラインの決定論を検証する。同一 seed は完全同一出力、異なる seed は異なる高さ、
    // 鉱脈 AABB は配置点中心の固定サイズであること。
    // Verify pipeline determinism: same seed => identical output, different seed => different heights,
    // vein AABBs stay a fixed size centred on their placement point.
    public class MapGenerationPipelineTest
    {
        [Test]
        public void SameSeedProducesIdenticalOutput()
        {
            var config = TestGenerationConfigFactory.CreateSmall();
            var a = MapGenerationPipeline.Generate(config, 12345, TestGenerationConfigFactory.ServerDataDirectory);
            var b = MapGenerationPipeline.Generate(config, 12345, TestGenerationConfigFactory.ServerDataDirectory);

            Assert.That(a.Tiles[0].Heights, Is.EqualTo(b.Tiles[0].Heights));
            Assert.That(a.Tiles[0].BiomeIndices, Is.EqualTo(b.Tiles[0].BiomeIndices));
            Assert.That(a.MapObjects.Count, Is.EqualTo(b.MapObjects.Count));
            Assert.That(a.ItemVeins.Count, Is.EqualTo(b.ItemVeins.Count));

            // 鉱脈 AABB は配置点から一意に決まるため、同一 seed では要素単位でも一致する。
            // A vein AABB is derived solely from its placement point, so it matches element-wise for the same seed.
            for (int i = 0; i < a.ItemVeins.Count; i++)
            {
                Assert.That(a.ItemVeins[i].VeinGuid, Is.EqualTo(b.ItemVeins[i].VeinGuid));
                Assert.That(a.ItemVeins[i].Min, Is.EqualTo(b.ItemVeins[i].Min));
                Assert.That(a.ItemVeins[i].Max, Is.EqualTo(b.ItemVeins[i].Max));
            }
            for (int i = 0; i < a.MapObjects.Count; i++)
                Assert.That(a.MapObjects[i].Position, Is.EqualTo(b.MapObjects[i].Position));
        }

        [Test]
        public void DifferentSeedProducesDifferentHeights()
        {
            var config = TestGenerationConfigFactory.CreateSmall();
            var a = MapGenerationPipeline.Generate(config, 1, TestGenerationConfigFactory.ServerDataDirectory);
            var b = MapGenerationPipeline.Generate(config, 2, TestGenerationConfigFactory.ServerDataDirectory);
            Assert.That(a.Tiles[0].Heights.SequenceEqual(b.Tiles[0].Heights), Is.False);
        }

        [Test]
        public void VeinAabbIsFixedSizeAndNonEmpty()
        {
            var config = TestGenerationConfigFactory.CreateSmall();
            var output = MapGenerationPipeline.Generate(config, 12345, TestGenerationConfigFactory.ServerDataDirectory);

            Assert.That(output.ItemVeins, Is.Not.Empty);
            Assert.That(output.FluidVeins, Is.Not.Empty);

            // 生成もシーン座標化も通した後で、どの鉱脈も一辺2の固定AABBであること。
            // After both generation and the scene-space shift, every vein stays a fixed AABB two units per edge.
            foreach (var vein in output.ItemVeins)
                Assert.That(vein.Max - vein.Min, Is.EqualTo(new Vector3Int(2, 2, 2)));
            foreach (var vein in output.FluidVeins)
                Assert.That(vein.Max - vein.Min, Is.EqualTo(new Vector3Int(2, 2, 2)));
        }
    }
}
