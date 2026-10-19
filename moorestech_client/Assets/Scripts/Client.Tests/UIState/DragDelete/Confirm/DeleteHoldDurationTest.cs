using System;
using Client.Game.InGame.UI.ProgressBar;
using Client.Game.InGame.UI.UIState.State.DragDelete.Confirm;
using NUnit.Framework;

namespace Client.Tests.UIState.DragDelete.Confirm
{
    public class DeleteHoldDurationTest
    {
        [TestCase(1, 0.75f)]
        [TestCase(4, 0.75f)]
        [TestCase(5, 1.25f)]
        [TestCase(19, 1.25f)]
        [TestCase(20, 1.75f)]
        [TestCase(500, 1.75f)]
        public void 件数の段ごとに満了時間が決まる(int targetCount, float expectedSeconds)
        {
            var progress = new ProgressBarState();
            var hold = new DeleteHoldConfirm(progress);
            hold.SetSubject(DeleteHoldSubject.Selection(1, targetCount));
            Assert.IsFalse(hold.Advance(true, expectedSeconds - 0.125f));
            Assert.IsTrue(progress.IsShown);
            Assert.IsTrue(hold.Advance(true, 0.125f));
            Assert.IsFalse(progress.IsShown);
        }

        [Test]
        public void 対象0件の満了時間は求めない()
        {
            var hold = new DeleteHoldConfirm(new ProgressBarState());
            hold.SetSubject(DeleteHoldSubject.Selection(1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => hold.Advance(true, 0.1f));
        }
    }
}
