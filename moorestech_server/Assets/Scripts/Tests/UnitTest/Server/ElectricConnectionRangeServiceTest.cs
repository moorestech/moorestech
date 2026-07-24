using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;

namespace Tests.UnitTest.Server
{
    /// <summary>
    /// 範囲ボックス相互判定の純粋単体テスト。ワールド状態には依存しない
    /// Pure unit tests for the mutual range-box judgement; no world state involved
    /// </summary>
    public class ElectricConnectionRangeServiceTest
    {
        // 1x1x1ブロックのBlockPositionInfoを作る
        // Build a BlockPositionInfo for a 1x1x1 block
        private static BlockPositionInfo Cell(int x, int y, int z)
        {
            return new BlockPositionInfo(new Vector3Int(x, y, z), BlockDirection.North, Vector3Int.one);
        }

        [Test]
        public void 双方の範囲内なら接続可能()
        {
            // 水平7(±3)の電柱同士がX差3で相互に届く
            // Poles with horizontal 7 (±3) reach each other at X distance 3
            var profile = ConnectionRangeProfile.CreateUniform(7, 5);
            var result = ElectricConnectionRangeService.IsMutuallyConnectable(
                Cell(0, 0, 0), profile, true,
                Cell(3, 0, 0), profile, true);
            Assert.IsTrue(result);
        }

        [Test]
        public void 範囲境界の外なら接続不可()
        {
            // X差4は水平7(±3)の外
            // X distance 4 is outside horizontal 7 (±3)
            var profile = ConnectionRangeProfile.CreateUniform(7, 5);
            var result = ElectricConnectionRangeService.IsMutuallyConnectable(
                Cell(0, 0, 0), profile, true,
                Cell(4, 0, 0), profile, true);
            Assert.IsFalse(result);
        }

        [Test]
        public void 片側だけ届く非対称構成は接続不可()
        {
            // Aは広い(±4)がBは狭い(±1)。相互判定なので不可
            // A reaches (±4) but B does not (±1); mutual judgement fails
            var wide = ConnectionRangeProfile.CreateUniform(9, 9);
            var narrow = ConnectionRangeProfile.CreateUniform(3, 3);
            var result = ElectricConnectionRangeService.IsMutuallyConnectable(
                Cell(0, 0, 0), wide, false,
                Cell(3, 0, 0), narrow, false);
            Assert.IsFalse(result);
        }

        [Test]
        public void 高さ範囲も独立に判定される()
        {
            // 水平は届くがY差3が高さ5(±2)の外
            // Horizontal reaches but Y distance 3 exceeds height 5 (±2)
            var profile = ConnectionRangeProfile.CreateUniform(7, 5);
            var result = ElectricConnectionRangeService.IsMutuallyConnectable(
                Cell(0, 0, 0), profile, true,
                Cell(0, 3, 0), profile, true);
            Assert.IsFalse(result);
        }

        [Test]
        public void 電柱は相手種別で使用ボックスが切り替わる()
        {
            // 対電柱7(±3)・対機械5(±2)の電柱と、X差3の機械。電柱側の対機械ボックスが届かず不可
            // Pole with pole-range 7 (±3) and machine-range 5 (±2) versus a machine at X distance 3
            var pole = new ConnectionRangeProfile(7, 5, 5, 5);
            var machine = ConnectionRangeProfile.CreateUniform(9, 9);
            var result = ElectricConnectionRangeService.IsMutuallyConnectable(
                Cell(0, 0, 0), pole, true,
                Cell(3, 0, 0), machine, false);
            Assert.IsFalse(result);

            // 同じX差3でも相手が電柱なら対電柱ボックス(±3)で届く
            // The same X distance 3 connects when the target is a pole (±3 box)
            var result2 = ElectricConnectionRangeService.IsMutuallyConnectable(
                Cell(0, 0, 0), pole, true,
                Cell(3, 0, 0), new ConnectionRangeProfile(7, 5, 5, 5), true);
            Assert.IsTrue(result2);
        }

        [Test]
        public void マルチブロックは占有AABB全体で判定される()
        {
            // 3x1x1の機械の遠端セル(x=5)に±2の電柱ボックスが重なる
            // The pole's ±2 box overlaps the far cell (x=5) of a 3-wide machine
            var pole = new ConnectionRangeProfile(7, 5, 5, 5);
            var machine = ConnectionRangeProfile.CreateUniform(9, 9);
            var machineInfo = new BlockPositionInfo(new Vector3Int(5, 0, 0), BlockDirection.North, new Vector3Int(3, 1, 1));
            var result = ElectricConnectionRangeService.IsMutuallyConnectable(
                Cell(3, 0, 0), pole, true,
                machineInfo, machine, false);
            Assert.IsTrue(result);
        }

        [Test]
        public void 範囲0はクランプされ自セルのみ判定になる()
        {
            // 範囲0でも最低1にクランプされ、同一セル重なりのみ許容
            // Range 0 clamps to 1, allowing only same-cell overlap
            var zero = ConnectionRangeProfile.CreateUniform(0, 0);
            Assert.IsTrue(ElectricConnectionRangeService.Covers(Cell(0, 0, 0), (0, 0), Cell(0, 0, 0)));
            Assert.IsFalse(ElectricConnectionRangeService.IsMutuallyConnectable(
                Cell(0, 0, 0), zero, false, Cell(1, 0, 0), zero, false));
        }
    }
}
