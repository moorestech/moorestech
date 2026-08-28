using Client.Starter.EventMode;
using NUnit.Framework;

namespace Client.Tests.EventMode
{
    public class EventIdleTimerTest
    {
        [Test]
        public void 無変化のまま閾値へ達したらタイムアウトする()
        {
            var timer = new EventIdleTimer(3);

            Assert.IsFalse(timer.AdvanceAndCheckTimeout(false, 1f));
            Assert.IsFalse(timer.AdvanceAndCheckTimeout(false, 1f));
            Assert.IsTrue(timer.AdvanceAndCheckTimeout(false, 1f));
        }

        [Test]
        public void 入力変化があれば積算が0へ戻る()
        {
            var timer = new EventIdleTimer(3);

            timer.AdvanceAndCheckTimeout(false, 2f);
            Assert.IsFalse(timer.AdvanceAndCheckTimeout(true, 1f));
            Assert.IsFalse(timer.AdvanceAndCheckTimeout(false, 2f));
            Assert.IsTrue(timer.AdvanceAndCheckTimeout(false, 1f));
        }

        // 押しっぱなしは入力変化を生まないので、キーが刺さっても必ずタイムアウトへ到達する
        // A sustained hold produces no input change, so a stuck key still reaches the timeout
        [Test]
        public void 押しっぱなし相当でもタイムアウトへ到達する()
        {
            var timer = new EventIdleTimer(180);

            var elapsed = 0f;
            var isTimeout = false;
            while (elapsed < 300f && !isTimeout)
            {
                isTimeout = timer.AdvanceAndCheckTimeout(false, 1f / 60f);
                elapsed += 1f / 60f;
            }

            Assert.IsTrue(isTimeout);
            Assert.Less(elapsed, 181f);
        }

        [Test]
        public void Resetで積算が0へ戻る()
        {
            var timer = new EventIdleTimer(3);

            timer.AdvanceAndCheckTimeout(false, 2.5f);
            timer.Reset();

            Assert.IsFalse(timer.AdvanceAndCheckTimeout(false, 2.5f));
            Assert.IsTrue(timer.AdvanceAndCheckTimeout(false, 0.5f));
        }
    }
}
