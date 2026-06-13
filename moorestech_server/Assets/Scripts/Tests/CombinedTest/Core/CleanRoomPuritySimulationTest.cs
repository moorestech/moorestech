using Core.Master;
using Game.CleanRoom;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomPuritySimulationTest
    {
        [Test]
        public void ThresholdMaster_LoadsFourRows_BestFirst()
        {
            // DIコンテナ生成で MasterHolder.Load が走る。
            // Creating the DI container loads MasterHolder.
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var master = MasterHolder.CleanRoomThresholdMaster;
            Assert.AreEqual(4, master.Rows.Count);
            Assert.AreEqual(4, master.OutThresholdIndex);

            // 行0が最良（A相当）。値はバランス確定書§1。
            // Row 0 is the cleanest tier; values from balance §1.
            Assert.AreEqual(10.0, master.Rows[0].MaxConcentration, 1e-9);
            Assert.AreEqual(0.0167, master.Rows[0].RequiredAirChangeRate, 1e-9);
            Assert.AreEqual(1000.0, master.Rows[3].MaxConcentration, 1e-9);
            Assert.AreEqual(0.0014, master.Rows[3].RequiredAirChangeRate, 1e-9);
        }

        // バランス確定書§1 の4行（A/B/C/D相当）。判定純関数テスト用。
        // The four rows from balance §1, for pure decision tests.
        private static readonly CleanRoomThresholdRow[] Rows =
        {
            new CleanRoomThresholdRow(10.0, 0.0167),
            new CleanRoomThresholdRow(50.0, 0.0083),
            new CleanRoomThresholdRow(200.0, 0.0042),
            new CleanRoomThresholdRow(1000.0, 0.0014),
        };

        // 全行のACH要求（昇格マージン込み）を満たす十分大きい値。
        // ACH large enough to satisfy every row incl. promotion margin.
        private const double AchAllPass = 1.0;

        [Test]
        public void Decide_Row0_HoldBand_StaysRow0()
        {
            // 現在行0・C=9.5（保持帯 8〜10）→ 行0維持。C=11 で素閾値10超え → 行1へ降格。
            // Row 0 holds at C=9.5 (8..10 band); C=11 exceeds 10 -> demote to row 1.
            Assert.AreEqual(0, CleanRoomPurityRules.DecideThresholdIndex(0, 9.5, AchAllPass, Rows));
            Assert.AreEqual(1, CleanRoomPurityRules.DecideThresholdIndex(0, 11.0, AchAllPass, Rows));
        }

        [Test]
        public void Decide_Row1_PromotesOnlyAtOrBelowMargin()
        {
            // C=9（昇格境界 10×0.8=8 超）→ 行1維持。C=8.0（境界ちょうど）→ 行0へ昇格。
            // C=9 above the 8.0 promote bound stays row 1; C=8.0 promotes to row 0.
            Assert.AreEqual(1, CleanRoomPurityRules.DecideThresholdIndex(1, 9.0, AchAllPass, Rows));
            Assert.AreEqual(0, CleanRoomPurityRules.DecideThresholdIndex(1, 8.0, AchAllPass, Rows));
        }

        [Test]
        public void Decide_AchShortfall_Demotes()
        {
            // 現在行0・C=3.2 だが ACH=0.01 < 0.0167。行0→行1 は降格なので
            // 行1の素の要求 0.0083 を使い（昇格境界は無関係）、行1 に落ち着く。
            // Demotion from row 0 uses row 1's raw ACH requirement (0.0083), not promote bounds.
            Assert.AreEqual(1, CleanRoomPurityRules.DecideThresholdIndex(0, 3.2, 0.01, Rows));
        }

        [Test]
        public void Decide_AchPromotion_RequiresMargin()
        {
            // 行1から行0への昇格は ACH ≥ 0.0167×1.25 = 0.020875 が必要。
            // Promotion to row 0 needs ACH ≥ required × 1.25 (anti-flicker margin).
            Assert.AreEqual(1, CleanRoomPurityRules.DecideThresholdIndex(1, 5.0, 0.018, Rows));
            Assert.AreEqual(0, CleanRoomPurityRules.DecideThresholdIndex(1, 5.0, 0.021, Rows));
        }

        [Test]
        public void Decide_VeryDirtyOrFromOut_BehavesPerHysteresis()
        {
            // C=2000 は全行不成立 → Out（=rows.Count=4）。
            Assert.AreEqual(4, CleanRoomPurityRules.DecideThresholdIndex(0, 2000.0, AchAllPass, Rows));
            // Out(4)からの復帰は全行が昇格扱い: C=9 は行0(≤8)不成立・行1(≤40)成立 → 1。
            // Recovery from Out treats every row as promotion: C=9 fails row0 (≤8), meets row1 (≤40).
            Assert.AreEqual(1, CleanRoomPurityRules.DecideThresholdIndex(4, 9.0, AchAllPass, Rows));
        }

        [Test]
        public void Integrate_ConvergesToWorkedExampleEquilibrium()
        {
            // worked example: V=75, A_total=16, n·q=5 → N_eq=240（C_eq=3.2）。
            // 2000tick（100秒 ≒ 6.7τ, τ=15秒）回して平衡へ。
            // Balance §4 worked example; run 2000 ticks (≈6.7τ) to equilibrium.
            var n = 0.0;
            for (var i = 0; i < 2000; i++)
                n = CleanRoomPurityRules.IntegrateTick(n, 75.0, 16.0, 5.0, 0.05);

            Assert.AreEqual(240.0, n, 2.0, "N_eq = A_total/(n·q) · V = 240");
            Assert.AreEqual(3.2, n / 75.0, 0.05, "C_eq = 16/5 = 3.2");
        }

        [Test]
        public void Integrate_ClampsAtZero()
        {
            // 除去が過剰でも N は負にならない。
            // Over-removal never drives N negative.
            var n = CleanRoomPurityRules.IntegrateTick(1.0, 1.0, 0.0, 100.0, 0.05);
            Assert.AreEqual(0.0, n, 1e-9);
        }

        [Test]
        public void Redistribute_SplitMergeExpand_ConserveCorrectly()
        {
            // 分割: 旧{Cells=10, N=100} → 新2部屋(各overlap=5) → 各50・総和100保存。
            // Split conserves total N; each part gets C_old·overlap.
            var n1 = CleanRoomPurityRules.RedistributeImpurity(100.0, 10, 5);
            var n2 = CleanRoomPurityRules.RedistributeImpurity(100.0, 10, 5);
            Assert.AreEqual(50.0, n1, 1e-9);
            Assert.AreEqual(100.0, n1 + n2, 1e-9);

            // 結合: 旧2部屋{Cells=5, N=50}が全セル重なりで1新部屋へ → 合算100。
            // Merge sums N.
            var merged = CleanRoomPurityRules.RedistributeImpurity(50.0, 5, 5)
                       + CleanRoomPurityRules.RedistributeImpurity(50.0, 5, 5);
            Assert.AreEqual(100.0, merged, 1e-9);

            // 拡張: 旧{Cells=27, N=100}を全て含む大部屋（overlap=27）→ N=100保存（濃度は希釈）。
            // Expansion preserves N; new cells are clean air (dilution).
            Assert.AreEqual(100.0, CleanRoomPurityRules.RedistributeImpurity(100.0, 27, 27), 1e-9);

            // 縮小: overlap=10/27 → N按分（残り17セル分のNは消滅）。
            // Shrink keeps concentration; N outside the overlap vanishes.
            Assert.AreEqual(100.0 * 10 / 27, CleanRoomPurityRules.RedistributeImpurity(100.0, 27, 10), 1e-9);
        }
    }
}
