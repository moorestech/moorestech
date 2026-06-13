using System.Collections.Generic;
using Game.Block.Blocks.CleanRoom;
using NUnit.Framework;

namespace Tests.UnitTest.Game
{
    public class SemiconductorChipDrawTest
    {
        // テスト用基礎分布（level 昇順）: Lv1:0.70 / Lv2:0.20 / Lv3:0.08 / Lv4:0.02
        // Base distribution for tests (ascending level order)
        private static readonly IReadOnlyList<(int level, double weight)> Dist = new (int, double)[]
        {
            (1, 0.70), (2, 0.20), (3, 0.08), (4, 0.02),
        };

        // MaxGrade=2 ではどの seed でも Lv2 を超えない（天井クランプ）
        // With MaxGrade=2, no seed ever yields above Lv2 (ceiling clamp)
        [Test]
        public void CeilingNeverExceededTest()
        {
            for (long seed = 0; seed < 1000; seed++)
            {
                var ok = SemiconductorChipDraw.TryDrawLevel(Dist, maxGrade: 2, downBinRate: 0.15, euvSuccessPercent: 1.0, seed, outputIndex: 0, out var lv);
                Assert.IsTrue(ok);
                Assert.LessOrEqual(lv, 2, $"seed {seed} exceeded ceiling");
                Assert.GreaterOrEqual(lv, 1);
            }
        }

        // 同一 seed・同一引数は常に同一結果（決定性）
        // Same seed and args always yield the same result (determinism)
        [Test]
        public void DeterministicForSameSeedTest()
        {
            var okA = SemiconductorChipDraw.TryDrawLevel(Dist, 3, 0.05, 1.0, 12345, 0, out var a);
            var okB = SemiconductorChipDraw.TryDrawLevel(Dist, 3, 0.05, 1.0, 12345, 0, out var b);
            Assert.AreEqual(okA, okB);
            Assert.AreEqual(a, b);
        }

        // 出力要素インデックスが違えば同一 seed でも独立に抽選される（相関排除）
        // Different output indices decorrelate even under the same cycle seed
        [Test]
        public void OutputIndexDecorrelatesTest()
        {
            var diff = 0;
            for (long seed = 0; seed < 1000; seed++)
            {
                SemiconductorChipDraw.TryDrawLevel(Dist, 4, 0.0, 1.0, seed, 0, out var lv0);
                SemiconductorChipDraw.TryDrawLevel(Dist, 4, 0.0, 1.0, seed, 1, out var lv1);
                if (lv0 != lv1) diff++;
            }
            Assert.Greater(diff, 0, "outputs are fully correlated across indices");
        }

        // down-bin 率は「格下げ可能な標本（baseLv≥2）」に条件付けて検証する。
        // Lv1 は下げ先が無く格下げを観測できないため、全標本に対する率は約 5%×P(baseLv≥2) に縮む。
        // Down-bin rate must be measured conditional on demotable samples (baseLv >= 2);
        // Lv1 cannot demote, so the unconditional rate shrinks to ~5% * P(baseLv>=2).
        [Test]
        public void DownBinConditionalRateApproximatelyFivePercentTest()
        {
            int eligible = 0, demoted = 0;
            for (long seed = 0; seed < 20000; seed++)
            {
                var baseLv = SemiconductorChipDraw.DrawBaseLevelForTest(Dist, maxGrade: 3, seed, outputIndex: 0);
                if (baseLv < 2) continue;
                eligible++;
                SemiconductorChipDraw.TryDrawLevel(Dist, 3, 0.05, 1.0, seed, 0, out var finalLv);
                if (finalLv < baseLv) demoted++;
            }
            Assert.Greater(eligible, 1000, "sample too small");
            var rate = demoted / (double)eligible;
            Assert.That(rate, Is.EqualTo(0.05).Within(0.015)); // 5% ± 1.5%（条件付き）
        }

        // MaxGrade=0（Out 相当）は抽選せず出力なし（false）。サイレントに Lv1 を出してはならない
        // MaxGrade=0 (Out) yields no output; silent Lv1 emission is forbidden
        [Test]
        public void MaxGradeZeroYieldsNoOutputTest()
        {
            for (long seed = 0; seed < 200; seed++)
                Assert.IsFalse(SemiconductorChipDraw.TryDrawLevel(Dist, maxGrade: 0, downBinRate: 0.0, euvSuccessPercent: 1.0, seed, 0, out _));
        }

        // EUV 成功率 0.8 → 約 20% が出力なし（catastrophic 失敗）。level 抽選とは独立（別 salt）
        // euvSuccessPercent 0.8 -> ~20% no-output, independent of the level draw (separate salt)
        [Test]
        public void EuvFailRateApproximatelyTwentyPercentTest()
        {
            int fail = 0; const int total = 20000;
            for (long seed = 0; seed < total; seed++)
                if (!SemiconductorChipDraw.TryDrawLevel(Dist, 4, 0.0, 0.8, seed, 0, out _)) fail++;
            Assert.That(fail / (double)total, Is.EqualTo(0.20).Within(0.01));
        }

        // 天井で切り落とした確率質量は比例配分で再正規化される（合計1の保存）
        // Truncated mass above the ceiling is renormalized proportionally (total probability preserved)
        [Test]
        public void TruncatedDistributionRenormalizedTest()
        {
            // dist {1:0.5, 2:0.3, 3:0.2}, ceiling=2 → P(2) = 0.3/0.8 = 0.375 ± 帯
            var dist = new (int, double)[] { (1, 0.5), (2, 0.3), (3, 0.2) };
            int lv2 = 0; const int total = 20000;
            for (long seed = 0; seed < total; seed++)
            {
                var baseLv = SemiconductorChipDraw.DrawBaseLevelForTest(dist, maxGrade: 2, seed, 0);
                if (baseLv == 2) lv2++;
            }
            Assert.That(lv2 / (double)total, Is.EqualTo(0.375).Within(0.015));
        }

        // down-bin が発火しても下げ幅は1段だけ（2段以上下げない）
        // Down-bin demotes exactly one level when it fires
        [Test]
        public void DownBinDemotesExactlyOneLevelTest()
        {
            for (long seed = 0; seed < 5000; seed++)
            {
                var baseLv = SemiconductorChipDraw.DrawBaseLevelForTest(Dist, 4, seed, 0);
                SemiconductorChipDraw.TryDrawLevel(Dist, 4, 0.35, 1.0, seed, 0, out var finalLv);
                Assert.That(baseLv - finalLv, Is.InRange(0, 1));
            }
        }
    }
}
