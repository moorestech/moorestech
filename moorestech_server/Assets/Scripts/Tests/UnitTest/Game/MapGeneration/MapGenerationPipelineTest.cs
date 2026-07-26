using System.Linq;
using Game.MapGeneration.Pipeline;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration
{
    // 生成パイプラインの決定論を検証する。同一 seed は完全同一出力、異なる seed は異なる高さ、
    // 鉱脈 AABB は整数スナップ済みで空でないこと。
    // Verify pipeline determinism: same seed => identical output, different seed => different heights,
    // vein AABBs are integer-snapped and non-empty.
    public class MapGenerationPipelineTest
    {
        [Test]
        public void SameSeedProducesIdenticalOutput()
        {
            var config = TestGenerationConfigFactory.CreateSmall();
            var a = MapGenerationPipeline.Generate(config, 12345);
            var b = MapGenerationPipeline.Generate(config, 12345);

            Assert.That(a.Heights, Is.EqualTo(b.Heights));
            Assert.That(a.BiomeIndices, Is.EqualTo(b.BiomeIndices));
            Assert.That(a.MapObjects.Count, Is.EqualTo(b.MapObjects.Count));
            Assert.That(a.ItemVeins.Count, Is.EqualTo(b.ItemVeins.Count));

            // クラスター採番リセットが効いていれば鉱脈 AABB も要素単位で一致する（NextClusterId 危険の検出）。
            // With the cluster-id reset in place the vein AABBs match element-wise (catches the NextClusterId hazard).
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
            var a = MapGenerationPipeline.Generate(config, 1);
            var b = MapGenerationPipeline.Generate(config, 2);
            Assert.That(a.Heights.SequenceEqual(b.Heights), Is.False);
        }

        [Test]
        public void VeinAabbIsIntegerSnappedAndNonEmpty()
        {
            var config = TestGenerationConfigFactory.CreateSmall();
            var output = MapGenerationPipeline.Generate(config, 12345);

            Assert.That(output.ItemVeins, Is.Not.Empty);
            foreach (var vein in output.ItemVeins)
            {
                Assert.That(vein.Min.x, Is.LessThanOrEqualTo(vein.Max.x));
                Assert.That(vein.Min.y, Is.LessThanOrEqualTo(vein.Max.y));
                Assert.That(vein.Min.z, Is.LessThanOrEqualTo(vein.Max.z));
            }
        }
    }
}
