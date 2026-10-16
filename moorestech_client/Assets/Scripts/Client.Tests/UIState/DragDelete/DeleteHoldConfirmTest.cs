using Client.Game.InGame.UI.ProgressBar;
using Client.Game.InGame.UI.UIState.State.DragDelete.Confirm;
using NUnit.Framework;

namespace Client.Tests.UIState.DragDelete
{
    public class DeleteHoldConfirmTest
    {
        private static readonly DeleteHoldSubject SelectionRev1 = DeleteHoldSubject.Selection(1);

        [Test]
        public void 押し続けると進捗が伸び2秒で満了する()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            Assert.IsFalse(hold.Advance(true, SelectionRev1, 1f));
            Assert.IsTrue(bar.IsShown);
            Assert.AreEqual(0.5f, bar.CurrentProgress, 0.001f);
            Assert.IsTrue(hold.Advance(true, SelectionRev1, 1f));
            Assert.IsFalse(bar.IsShown);
            Assert.IsFalse(hold.IsHolding);
        }

        [Test]
        public void 途中で離すと進捗が0に戻りバーが消える()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            hold.Advance(true, SelectionRev1, 1f);
            Assert.IsFalse(hold.Advance(false, SelectionRev1, 0.1f));
            Assert.IsFalse(bar.IsShown);
            Assert.IsFalse(hold.Advance(true, SelectionRev1, 1.5f));
            Assert.AreEqual(0.75f, bar.CurrentProgress, 0.001f);
        }

        [Test]
        public void 対象が変わると進捗が0からやり直しになる()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            hold.Advance(true, DeleteHoldSubject.Selection(1), 1.5f);
            Assert.IsFalse(hold.Advance(true, DeleteHoldSubject.Selection(2), 1f));
            Assert.AreEqual(0.5f, bar.CurrentProgress, 0.001f);
            var key = new object();
            Assert.IsFalse(hold.Advance(true, DeleteHoldSubject.Hovered(key), 1f));
            Assert.AreEqual(0.5f, bar.CurrentProgress, 0.001f);
            Assert.IsTrue(hold.Advance(true, DeleteHoldSubject.Hovered(key), 1f), "the same hovered key continues the hold");
        }

        [Test]
        public void 対象が無いときはバーを出さない()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            Assert.IsFalse(hold.Advance(true, DeleteHoldSubject.None, 1f));
            Assert.IsFalse(bar.IsShown);
            Assert.IsFalse(hold.IsHolding);
        }

        [Test]
        public void 満了後は一度離すまで次の長押しを始めない()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            Assert.IsTrue(hold.Advance(true, SelectionRev1, 2f));
            Assert.IsFalse(hold.Advance(true, DeleteHoldSubject.Selection(2), 5f));
            Assert.IsFalse(bar.IsShown);
            hold.Advance(false, DeleteHoldSubject.Selection(2), 0.1f);
            Assert.IsTrue(hold.Advance(true, DeleteHoldSubject.Selection(2), 2f));
        }

        [Test]
        public void 押下中のキャンセル後は離すまでホバーを削除しない()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            hold.Advance(true, SelectionRev1, 1f);
            hold.Cancel(true);
            Assert.AreEqual(0f, bar.CurrentProgress);
            var hovered = DeleteHoldSubject.Hovered(new object());
            Assert.IsFalse(hold.Advance(true, hovered, 5f));
            Assert.IsFalse(bar.IsShown);
            hold.Advance(false, hovered, 0.1f);
            Assert.IsTrue(hold.Advance(true, hovered, 2f));
        }

        [Test]
        public void キーを押していないキャンセル後は次の押下を受け付ける()
        {
            var hold = new DeleteHoldConfirm(new ProgressBarState());
            hold.Cancel(false);
            Assert.IsTrue(hold.Advance(true, SelectionRev1, 2f));
        }

        [Test]
        public void Resetで進捗とバーを戻す()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            hold.Advance(true, SelectionRev1, 1f);
            hold.Reset();
            Assert.AreEqual(0f, bar.CurrentProgress);
            Assert.IsFalse(bar.IsShown);
            Assert.IsFalse(hold.IsHolding);
        }
    }
}
