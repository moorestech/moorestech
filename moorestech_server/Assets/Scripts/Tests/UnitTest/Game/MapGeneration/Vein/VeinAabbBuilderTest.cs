using Game.MapGeneration.Pipeline.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // 鉱脈AABBは配置点を中心とした固定サイズであることを固定する（ADR-0023）。
    // Pins that a vein AABB is a fixed size centred on its placement point (ADR-0023).
    public class VeinAabbBuilderTest
    {
        [Test]
        public void AABBは配置点を中心に張られる()
        {
            var vein = VeinAabbBuilder.Build("11111111-1111-1111-1111-111111111111", new Vector3(10f, 20f, 30f));

            Assert.That(vein.VeinGuid, Is.EqualTo("11111111-1111-1111-1111-111111111111"));
            Assert.That(vein.Min, Is.EqualTo(new Vector3Int(9, 19, 29)));
            Assert.That(vein.Max, Is.EqualTo(new Vector3Int(11, 21, 31)));
        }

        [Test]
        public void 小数座標は丸めてから中心にする()
        {
            var vein = VeinAabbBuilder.Build("11111111-1111-1111-1111-111111111111", new Vector3(10.4f, 19.6f, -0.4f));

            Assert.That(vein.Min, Is.EqualTo(new Vector3Int(9, 19, -1)));
            Assert.That(vein.Max, Is.EqualTo(new Vector3Int(11, 21, 1)));
        }
    }
}
