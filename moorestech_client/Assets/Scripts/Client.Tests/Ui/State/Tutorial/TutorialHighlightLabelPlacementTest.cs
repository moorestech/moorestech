using Client.Ui.State.Tutorial.Logic;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Tutorial
{
    /// <summary>
    /// ラベル面が枠の下を既定に、収まる側へ反転し、clip の内へ収まることを確かめる（L0）
    /// Verifies the label face defaults below the ring, flips to the side that fits, and stays inside the clip (L0)
    /// </summary>
    public class TutorialHighlightLabelPlacementTest
    {
        private static readonly TutorialClipRect Clip = new(0, 0, 1280, 720);

        [Test]
        public void 既定は枠の下に隙間を空けて置く()
        {
            var position = TutorialHighlightLabelPlacement.Place(new TutorialClipRect(100, 100, 300, 140), Clip, 4f, 120f, 26f);
            Assert.AreEqual((100f, 144f), (position.Left, position.Top));
        }

        [Test]
        public void 下に収まらず上に収まるときだけ上へ反転する()
        {
            var position = TutorialHighlightLabelPlacement.Place(new TutorialClipRect(100, 680, 300, 710), Clip, 4f, 120f, 26f);
            Assert.AreEqual(650f, position.Top);
        }

        [Test]
        public void 上にも下にも収まらなければ下に置く()
        {
            var position = TutorialHighlightLabelPlacement.Place(new TutorialClipRect(100, 10, 300, 700), new TutorialClipRect(0, 0, 1280, 720), 4f, 120f, 26f);
            Assert.AreEqual(704f, position.Top);
        }

        [Test]
        public void 右端を越えるときはclipの内へ押し戻し器より広ければ左端に揃える()
        {
            Assert.AreEqual(1160f, TutorialHighlightLabelPlacement.Place(new TutorialClipRect(1200, 100, 1270, 140), Clip, 4f, 120f, 26f).Left);
            Assert.AreEqual(0f, TutorialHighlightLabelPlacement.Place(new TutorialClipRect(1200, 100, 1270, 140), Clip, 4f, 1400f, 52f).Left);
        }

        [Test]
        public void 折り返しの上限は器の幅と置き場の幅から枠の左端を引いた残りの小さい方()
        {
            // 1280 の右寄りの枠は残り 246 で折り返し、左寄りなら器の幅のまま
            // A ring near the right of 1280 wraps at the remaining 246; one near the left keeps the clip width
            Assert.AreEqual(246f, TutorialHighlightLabelPlacement.WrapLimit(new TutorialClipRect(1034, 600, 1090, 656), Clip, 1280f));
            Assert.AreEqual(1280f, TutorialHighlightLabelPlacement.WrapLimit(new TutorialClipRect(0, 600, 56, 656), Clip, 1280f));
            Assert.AreEqual(400f, TutorialHighlightLabelPlacement.WrapLimit(new TutorialClipRect(300, 100, 356, 156), new TutorialClipRect(0, 0, 400, 720), 1280f));
        }

        [Test]
        public void アンカーがclipで欠けていればラベルを出さない()
        {
            Assert.IsTrue(TutorialHighlightLabelPlacement.ShowsLabel(new TutorialClipRect(10, 10, 50, 50), Clip));
            Assert.IsFalse(TutorialHighlightLabelPlacement.ShowsLabel(new TutorialClipRect(-10, 10, 50, 50), Clip));
        }
    }
}
