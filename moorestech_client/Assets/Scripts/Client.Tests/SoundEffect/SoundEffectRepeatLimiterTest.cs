using Client.Game.InGame.SoundEffect;
using NUnit.Framework;

namespace Client.Tests.SoundEffect
{
    // 同じ効果音の連打が間隔ごとに1回へ間引かれることを確認
    // Verify rapid repeats of one sound thin out to once per interval
    public class SoundEffectRepeatLimiterTest
    {
        [Test]
        public void 同じ音は間隔が空くまで通さない()
        {
            var limiter = new SoundEffectRepeatLimiter(1f);

            // 同じ時刻に500回要求されても通るのは最初の1回だけ
            // Five hundred requests at one instant let only the first through
            var accepted = 0;
            for (var i = 0; i < 500; i++)
            {
                if (limiter.TryAccept(10f)) accepted++;
            }
            Assert.AreEqual(1, accepted);

            Assert.IsFalse(limiter.TryAccept(10.5f));
            Assert.IsTrue(limiter.TryAccept(11.5f));
        }

        [Test]
        public void 起動直後の最初の1回は通す()
        {
            var limiter = new SoundEffectRepeatLimiter(1f);
            Assert.IsTrue(limiter.TryAccept(0f));
        }
    }
}
