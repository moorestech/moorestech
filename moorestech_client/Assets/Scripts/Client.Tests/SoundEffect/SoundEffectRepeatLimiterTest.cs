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
            var limiter = new SoundEffectRepeatLimiter();
            var interval = SoundEffectRepeatLimiter.MinimumIntervalSeconds;

            // 同じ時刻に500回要求されても通るのは最初の1回だけ
            // Five hundred requests at one instant let only the first through
            var accepted = 0;
            for (var i = 0; i < 500; i++)
            {
                if (limiter.TryAccept(SoundEffectType.DestroyBlock, 10f)) accepted++;
            }
            Assert.AreEqual(1, accepted);

            Assert.IsFalse(limiter.TryAccept(SoundEffectType.DestroyBlock, 10f + interval * 0.5f));
            Assert.IsTrue(limiter.TryAccept(SoundEffectType.DestroyBlock, 10f + interval * 1.5f));
        }

        [Test]
        public void 別の種類の音は互いに妨げない()
        {
            var limiter = new SoundEffectRepeatLimiter();
            Assert.IsTrue(limiter.TryAccept(SoundEffectType.DestroyBlock, 10f));
            Assert.IsTrue(limiter.TryAccept(SoundEffectType.PlaceBlock, 10f));
            Assert.IsFalse(limiter.TryAccept(SoundEffectType.PlaceBlock, 10f));
        }
    }
}
