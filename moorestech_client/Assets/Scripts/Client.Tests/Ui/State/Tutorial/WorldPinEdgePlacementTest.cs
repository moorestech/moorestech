using Client.Ui.State.Tutorial.Logic;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Tutorial
{
    /// <summary>
    /// 画面外ピンの矢印が先に当たる辺でクランプされ、角度が atan2 の度になることを確かめる（L0）
    /// Verifies the off-screen pin arrow clamps at the edge it hits first with an atan2 angle in degrees (L0)
    /// </summary>
    public class WorldPinEdgePlacementTest
    {
        [Test]
        public void 真右の方向は右端から余白だけ内側の中央に置く()
        {
            Assert.IsTrue(WorldPinEdgePlacement.TryPlace(1280f, 720f, 40f, 1f, 0f, out var spot));
            Assert.AreEqual((1240f, 360f, 0f), (spot.Left, spot.Top, spot.AngleDegrees));
        }

        [Test]
        public void 斜めの方向は先に当たる辺でクランプし角度はatan2の度()
        {
            Assert.IsTrue(WorldPinEdgePlacement.TryPlace(1280f, 720f, 40f, 0.7071f, 0.7071f, out var spot));
            Assert.AreEqual(960f, spot.Left, 0.01f);
            Assert.AreEqual(680f, spot.Top, 0.01f);
            Assert.AreEqual(45f, spot.AngleDegrees, 0.01f);
        }

        [Test]
        public void 寸法が0なら置かない()
        {
            Assert.IsFalse(WorldPinEdgePlacement.TryPlace(0f, 720f, 40f, 1f, 0f, out _));
        }
    }
}
