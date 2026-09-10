using System.Collections.Generic;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Stages;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // シーン座標化でAABBのサイズが変わらないことを固定する。Min/Maxを独立に丸めると偶奇差で1ずれる。
    // Pins that the scene-space shift never changes an AABB's size; rounding Min and Max apart drifts by one on mixed parity.
    public class VeinSceneOffsetTest
    {
        [Test]
        public void 半整数シフトでも鉱脈AABBのサイズは保存される()
        {
            // Min偶数・Max奇数の奇数サイズAABB。0.5シフトで丸め方向が割れる最小の反例。
            // An odd-sized AABB with even Min and odd Max: the smallest counterexample where a 0.5 shift splits the rounding.
            var veins = new List<PlacedVein>
            {
                new("11111111-1111-1111-1111-111111111111", new Vector3Int(2, 0, 2), new Vector3Int(3, 0, 3)),
            };

            PlacementSceneOffset.ToSceneSpace(veins, new Vector2(0.5f, 0.5f));

            Assert.That(veins[0].Max - veins[0].Min, Is.EqualTo(new Vector3Int(1, 0, 1)));
        }

        [Test]
        public void 鉱脈AABBはシフトぶん平行移動する()
        {
            var veins = new List<PlacedVein>
            {
                new("11111111-1111-1111-1111-111111111111", new Vector3Int(9, 19, 29), new Vector3Int(11, 21, 31)),
            };

            PlacementSceneOffset.ToSceneSpace(veins, new Vector2(4f, 6f));

            Assert.That(veins[0].Min, Is.EqualTo(new Vector3Int(5, 19, 23)));
            Assert.That(veins[0].Max, Is.EqualTo(new Vector3Int(7, 21, 25)));
        }
    }
}
