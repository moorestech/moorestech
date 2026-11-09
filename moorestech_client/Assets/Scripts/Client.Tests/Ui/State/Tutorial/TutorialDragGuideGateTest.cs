using Client.Ui.State.Tutorial.Logic;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Tutorial
{
    /// <summary>
    /// ドラッグ誘導が両端 ready かつ起点が見えている時だけ出て、移動量が中心間になることを確かめる（L0）
    /// Verifies the drag guide shows only with both ends ready and a visible origin, travelling center to center (L0)
    /// </summary>
    public class TutorialDragGuideGateTest
    {
        private static readonly TutorialClipRect Clip = new(0, 0, 1280, 720);

        [Test]
        public void 両端がreadyなら起点の中心から終点の中心までの移動量を返す()
        {
            var from = new TutorialAnchorState.Ready(new TutorialClipRect(100, 100, 140, 140), Clip, TutorialAnchorGround.OverWorld);
            var to = new TutorialAnchorState.Ready(new TutorialClipRect(500, 300, 540, 340), Clip, TutorialAnchorGround.OverWorld);
            Assert.IsTrue(TutorialDragGuideGate.TryPlace(from, to, out var placement));
            Assert.AreEqual((120f, 120f, 400f, 200f), (placement.OriginX, placement.OriginY, placement.TravelX, placement.TravelY));
        }

        [Test]
        public void 片方が未解決か起点の中心がclipの外なら置かない()
        {
            var to = new TutorialAnchorState.Ready(new TutorialClipRect(500, 300, 540, 340), Clip, TutorialAnchorGround.OverWorld);
            Assert.IsFalse(TutorialDragGuideGate.TryPlace(TutorialAnchorState.NotFound.Missing(), to, out _));
            var scrolledOut = new TutorialAnchorState.Ready(new TutorialClipRect(100, 40, 140, 80), new TutorialClipRect(0, 100, 1280, 720), TutorialAnchorGround.OverWorld);
            Assert.IsFalse(TutorialDragGuideGate.TryPlace(scrolledOut, to, out _));
        }
    }
}
