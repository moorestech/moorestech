using System.Diagnostics;
using Client.Game.InGame.Map.MapObject;
using NUnit.Framework;

namespace Client.Tests.Map
{
    /// <summary>
    ///     フレーム時間予算の枯渇判定を検証
    ///     Verifies the frame time budget exhaustion decision
    /// </summary>
    public class FrameTimeBudgetTest
    {
        [Test]
        public void 予算ゼロは即座に枯渇する()
        {
            var budget = new FrameTimeBudget(0.0);
            Assert.IsTrue(budget.IsExhausted);
        }

        [Test]
        public void 十分大きい予算は枯渇しない()
        {
            var budget = new FrameTimeBudget(60000.0);
            Assert.IsFalse(budget.IsExhausted);
        }

        [Test]
        public void Restartで計測が仕切り直される()
        {
            var budget = new FrameTimeBudget(60000.0);
            budget.Restart();
            Assert.IsFalse(budget.IsExhausted);
        }

        [Test]
        public void 予算を使い切ってからRestartすると枯渇が解除される()
        {
            // Restart()の本体が退化してもトートロジーで緑になる既存3件では検出できないため、実際に枯渇させてから戻す
            // The existing 3 cases pass tautologically even if Restart()'s body degenerates, so this actually exhausts the budget before restoring it
            var budget = new FrameTimeBudget(20.0);
            var spinWatch = Stopwatch.StartNew();
            while (spinWatch.Elapsed.TotalMilliseconds < 40.0)
            {
            }

            Assert.IsTrue(budget.IsExhausted);

            budget.Restart();
            Assert.IsFalse(budget.IsExhausted);
        }
    }
}
