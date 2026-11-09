using Client.Ui.State.Tutorial.Logic;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Tutorial
{
    /// <summary>
    /// 枠の inset が、クリップの外へ出た辺だけを切り、見えない枠を描かないことを確かめる（L0）
    /// Verifies the ring inset cuts only edges beyond the clip and skips rings that cannot be seen (L0)
    /// </summary>
    public class TutorialHighlightClipTest
    {
        [Test]
        public void 切らない辺はグロー幅だけ外へ残す()
        {
            Assert.IsTrue(TutorialHighlightClip.TryComputeInset(new TutorialClipRect(100, 100, 200, 150), new TutorialClipRect(0, 0, 1280, 720), 4f, out var inset));
            Assert.AreEqual((-4f, -4f, -4f, -4f), (inset.Top, inset.Right, inset.Bottom, inset.Left));
        }

        [Test]
        public void clipの外へ出た辺だけを切る()
        {
            Assert.IsTrue(TutorialHighlightClip.TryComputeInset(new TutorialClipRect(100, 80, 200, 150), new TutorialClipRect(0, 100, 1280, 720), 4f, out var inset));
            Assert.AreEqual(20f, inset.Top);
            Assert.AreEqual(-4f, inset.Bottom);
        }

        [Test]
        public void 素の矩形が交わらなければ描かない()
        {
            Assert.IsFalse(TutorialHighlightClip.TryComputeInset(new TutorialClipRect(100, 60, 200, 98), new TutorialClipRect(0, 100, 1280, 720), 4f, out _));
        }

        [Test]
        public void 切った後に高さが残らなければ描かない()
        {
            // 素の矩形は交わる（IsDisjoint が偽）まま上 40＋下 60 が高さ 100 に届く入力で、高さ判定の分岐を通す
            // The raw rects overlap (IsDisjoint false) while top 40 + bottom 60 reaches the height 100, exercising the height branch
            Assert.IsFalse(TutorialHighlightClip.TryComputeInset(new TutorialClipRect(100, 60, 200, 160), new TutorialClipRect(0, 100, 1280, 100), 4f, out _));
        }
    }
}
