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
    }
}
