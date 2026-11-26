using Client.Game.InGame.Block.GearRotation.Gpu;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Gear
{
    public class GearRotationGpuHashTableTest
    {
        [Test]
        public void 速さを替えても角度は替えた時点から続く()
        {
            var table = new GearRotationGpuHashTable();
            var key = new Vector3Int(3, 4, 5);
            table.SetState(key, 60f, true, 0f);
            table.SetState(key, 120f, true, 1f);

            // 60RPMは毎秒360度。1秒後に120RPM(毎秒720度)へ替わる
            // 60 RPM is 360 deg/s; after one second it switches to 120 RPM (720 deg/s)
            Assert.IsTrue(table.TryFind(key, out var row));
            Assert.AreEqual(360f, row.SignedAngle, 1e-3f);
            Assert.AreEqual(360f, row.AbsoluteAngle, 1e-3f);
            Assert.AreEqual(720f, row.SignedDegreesPerSecond, 1e-3f);
            Assert.AreEqual(1f, row.BaseTime);
        }

        [Test]
        public void 反時計回りは符号付き角度だけが逆へ進む()
        {
            var table = new GearRotationGpuHashTable();
            var key = Vector3Int.one;
            table.SetState(key, 60f, false, 0f);
            table.SetState(key, 60f, false, 0.5f);

            Assert.IsTrue(table.TryFind(key, out var row));
            Assert.AreEqual(3600f - 180f, row.SignedAngle, 1e-3f);
            Assert.AreEqual(180f, row.AbsoluteAngle, 1e-3f);
            Assert.AreEqual(-360f, row.SignedDegreesPerSecond, 1e-3f);
        }

        [Test]
        public void 消した行を飛び越えて残りの行を引ける()
        {
            // 表が広がり、削除済みの印が混ざっても、シェーダーと同じ探し方で全部引けること
            // Every row must stay reachable with the shader's probing after growth and with tombstones mixed in
            var table = new GearRotationGpuHashTable();
            for (var i = 0; i < 3000; i++) table.SetState(new Vector3Int(i % 37, i / 37, -i), i, true, 0f);
            for (var i = 0; i < 3000; i += 2) table.Remove(new Vector3Int(i % 37, i / 37, -i));

            for (var i = 0; i < 3000; i++)
            {
                var found = table.TryFind(new Vector3Int(i % 37, i / 37, -i), out var row);
                Assert.AreEqual(i % 2 == 1, found, $"i={i}");
                if (found) Assert.AreEqual(i * 6f, row.SignedDegreesPerSecond, 1e-3f);
            }
        }

        [Test]
        public void キーはブロック原点の2倍を丸めた整数()
        {
            Assert.AreEqual(new Vector3Int(1245, 166, 841), GearRotationGpuHashTable.ToKey(new Vector3(622.5f, 83f, 420.5f)));
        }
    }
}
