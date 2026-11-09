using Client.Skit.Skit;
using Client.Skit.UI;
using Client.Ui.State.SkitPresence;
using NUnit.Framework;

namespace Client.Tests.Ui.State.SkitPresence
{
    /// <summary>
    /// blocking スキットの間だけ真になることを確かめる（L1。Web の useBlockingSkitActive。背景スキットの側は CurrentChallengeHudStateTest）
    /// Verifies the flag is true only during a blocking skit (L1; Web useBlockingSkitActive; the background-skit side is in CurrentChallengeHudStateTest)
    /// </summary>
    public class BlockingSkitPresenceTest
    {
        [Test]
        public void blockingスキットの間だけ真になる()
        {
            var store = new SkitPresentationStateStore();
            var presence = new BlockingSkitPresence(store);
            Assert.IsFalse(presence.IsBlocking.Value);

            // 始めると真、終えると偽に戻る
            // True once started, false again once ended
            store.BeginBlocking(new SkitActionContext());
            Assert.IsTrue(presence.IsBlocking.Value);
            store.End();
            Assert.IsFalse(presence.IsBlocking.Value);
        }
    }
}
