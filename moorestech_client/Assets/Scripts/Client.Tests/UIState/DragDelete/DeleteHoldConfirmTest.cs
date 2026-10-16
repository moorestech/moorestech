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
            hold.SetSubject(SelectionRev1);
            Assert.IsFalse(hold.Advance(true, 1f));
            Assert.IsTrue(bar.IsShown);
            Assert.AreEqual(0.5f, bar.CurrentProgress, 0.001f);
            hold.SetSubject(SelectionRev1);
            Assert.IsTrue(hold.Advance(true, 1f));
            Assert.IsFalse(bar.IsShown);
        }

        [Test]
        public void 途中で離すと進捗が0に戻りバーが消える()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            hold.SetSubject(SelectionRev1);
            hold.Advance(true, 1f);
            hold.SetSubject(SelectionRev1);
            Assert.IsFalse(hold.Advance(false, 0.1f));
            Assert.IsFalse(bar.IsShown);
            hold.SetSubject(SelectionRev1);
            Assert.IsFalse(hold.Advance(true, 1.5f));
            Assert.AreEqual(0.75f, bar.CurrentProgress, 0.001f);
        }

        [Test]
        public void 対象が変わると進捗が0からやり直しになる()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            hold.SetSubject(DeleteHoldSubject.Selection(1));
            hold.Advance(true, 1.5f);
            hold.SetSubject(DeleteHoldSubject.Selection(2));
            Assert.IsFalse(hold.Advance(true, 1f));
            Assert.AreEqual(0.5f, bar.CurrentProgress, 0.001f);
            var key = new object();
            hold.SetSubject(DeleteHoldSubject.Hovered(key));
            Assert.IsFalse(hold.Advance(true, 1f));
            Assert.AreEqual(0.5f, bar.CurrentProgress, 0.001f);
            hold.SetSubject(DeleteHoldSubject.Hovered(key));
            Assert.IsTrue(hold.Advance(true, 1f), "the same hovered key continues the hold");
        }

        [Test]
        public void 別のホバーキーへ移ったらその対象だけで2秒待つ()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            var first = DeleteHoldSubject.Hovered(new object());
            var second = DeleteHoldSubject.Hovered(new object());
            hold.SetSubject(first);
            Assert.IsFalse(hold.Advance(true, 1.5f));
            hold.SetSubject(second);
            Assert.IsFalse(hold.Advance(true, 0.5f));
            Assert.AreEqual(0.25f, bar.CurrentProgress, 0.001f);
            hold.SetSubject(second);
            Assert.IsFalse(hold.Advance(true, 1f));
            hold.SetSubject(second);
            Assert.IsTrue(hold.Advance(true, 0.5f));
        }

        [Test]
        public void 対象が無いときはバーを出さない()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            hold.SetSubject(DeleteHoldSubject.None);
            Assert.IsFalse(hold.Advance(true, 1f));
            Assert.IsFalse(bar.IsShown);
        }

        [Test]
        public void 満了後は一度離すまで次の長押しを始めない()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            hold.SetSubject(SelectionRev1);
            Assert.IsTrue(hold.Advance(true, 2f));
            hold.SetSubject(DeleteHoldSubject.Selection(2));
            Assert.IsFalse(hold.Advance(true, 5f));
            Assert.IsFalse(bar.IsShown);
            hold.SetSubject(DeleteHoldSubject.Selection(2));
            hold.Advance(false, 0.1f);
            hold.SetSubject(DeleteHoldSubject.Selection(2));
            Assert.IsTrue(hold.Advance(true, 2f));
        }

        [Test]
        public void 押下中のキャンセル後は離すまでホバーを削除しない()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            hold.SetSubject(SelectionRev1);
            hold.Advance(true, 1f);
            hold.Cancel(true);
            Assert.AreEqual(0f, bar.CurrentProgress);
            var hovered = DeleteHoldSubject.Hovered(new object());
            hold.SetSubject(hovered);
            Assert.IsFalse(hold.Advance(true, 5f));
            Assert.IsFalse(bar.IsShown);
            hold.SetSubject(hovered);
            hold.Advance(false, 0.1f);
            hold.SetSubject(hovered);
            Assert.IsTrue(hold.Advance(true, 2f));
        }

        [Test]
        public void キーを押していないキャンセル後は次の押下を受け付ける()
        {
            var hold = new DeleteHoldConfirm(new ProgressBarState());
            hold.Cancel(false);
            hold.SetSubject(SelectionRev1);
            Assert.IsTrue(hold.Advance(true, 2f));
        }

        [Test]
        public void Resetで進捗とバーを戻す()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            hold.SetSubject(SelectionRev1);
            hold.Advance(true, 1f);
            hold.Reset();
            Assert.AreEqual(0f, bar.CurrentProgress);
            Assert.IsFalse(bar.IsShown);
        }
    }
}
