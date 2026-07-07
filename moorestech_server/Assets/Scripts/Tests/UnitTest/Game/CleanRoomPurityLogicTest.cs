using System.Collections.Generic;
using Game.CleanRoom;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    public class CleanRoomPurityLogicTest
    {
        [Test]
        public void IntegrateTickConvergesToEquilibriumConcentrationTest()
        {
            // A=16, nq=5, V=75 の反復で濃度 C=N/V が平衡値 A/nq=3.2 に収束する
            // Iterating with A=16, nq=5, V=75 converges concentration C=N/V to A/nq=3.2
            var impurity = 0.0;
            for (var i = 0; i < 1000; i++)
                impurity = CleanRoomPurityLogic.IntegrateTick(impurity, 75, 16.0, 5.0, 0.1);

            Assert.AreEqual(3.2, impurity / 75, 0.01);
        }

        [Test]
        public void IntegrateTickWithoutRemovalIncreasesMonotonicallyTest()
        {
            // 除去0なら流入分だけ毎tick単調増加する
            // With zero removal the count grows monotonically by the inflow
            var impurity = 0.0;
            for (var i = 0; i < 100; i++)
            {
                var next = CleanRoomPurityLogic.IntegrateTick(impurity, 10, 0.5, 0.0, 0.05);
                Assert.Greater(next, impurity);
                impurity = next;
            }
        }

        [Test]
        public void IntegrateTickNeverGoesNegativeTest()
        {
            // 過大な除去でも0でクランプされ負にならない
            // Excessive removal clamps at zero and never goes negative
            var result = CleanRoomPurityLogic.IntegrateTick(1.0, 1, 0.0, 1000.0, 1.0);
            Assert.AreEqual(0.0, result);
        }

        [Test]
        public void DecideThresholdIndexHysteresisTest()
        {
            var rows = new List<CleanRoomThresholdRow>
            {
                new(10, 0.017),
                new(50, 0.0083),
            };

            // 現在B: C=9 は昇格マージン（C≤8 かつ ACH≥0.02125）を満たさずBのまま
            // At B, C=9 misses the promotion margin (C≤8 and ACH≥0.02125), so it stays B
            Assert.AreEqual(1, CleanRoomPurityLogic.DecideThresholdIndex(1, 9.0, 0.02, rows));

            // 現在B: C=7.9, ACH=0.022 は昇格条件を満たしAへ昇格する
            // At B, C=7.9 with ACH=0.022 satisfies the promotion margin and promotes to A
            Assert.AreEqual(0, CleanRoomPurityLogic.DecideThresholdIndex(1, 7.9, 0.022, rows));

            // 現在A: C=10.5 は素の閾値でAを外れBへ降格する
            // At A, C=10.5 fails the raw threshold and demotes to B
            Assert.AreEqual(1, CleanRoomPurityLogic.DecideThresholdIndex(0, 10.5, 0.02, rows));

            // どの行も満たさなければ Out（rows.Count）になる
            // When no row is satisfied the result is Out (rows.Count)
            Assert.AreEqual(2, CleanRoomPurityLogic.DecideThresholdIndex(1, 100.0, 0.0, rows));
        }
    }
}
