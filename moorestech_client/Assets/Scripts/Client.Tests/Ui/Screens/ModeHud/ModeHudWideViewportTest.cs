using Client.Ui.Screens.ModeHud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// 2 つの論理解像度で右上の箱が実画面の右上 24px に固定される。2432x786 は Expand（参照 1280x720）で 2227.7x720
    /// At two logical sizes the top-right box stays 24px from the real top-right; 2432x786 becomes 2227.7x720 under Expand (reference 1280x720)
    /// </summary>
    [TestFixture(1280f, 720f)]
    [TestFixture(2227.7f, 720f)]
    public class ModeHudWideViewportTest : ScreenTestHost
    {
        private readonly Vector2 _logicalSize;
        private ModeHudView _view;

        public ModeHudWideViewportTest(float width, float height)
        {
            _logicalSize = new Vector2(width, height);
        }

        [OneTimeSetUp]
        public void UseLogicalSize()
        {
            panelSize = _logicalSize;
        }

        [SetUp]
        public void MountModeHud()
        {
            _view = Mount(new ModeHudView());
        }

        /// <web-test file="e2e/tests/modeHud/operation-mode-hud.spec.ts">横長画面でも配置モードHUDを実画面右上へ固定する</web-test>
        [Test]
        public void 配置モードHUDは実画面の右上に固定される()
        {
            _view.Render(ModeHudViewModelSamples.Placement());
            simulate.FrameUpdate();
            var corner = Find<VisualElement>(_view, "placement-mode-hud");
            Assert.AreEqual(_logicalSize.x - 24f, corner.worldBound.xMax, 1f);
            Assert.AreEqual(24f, corner.worldBound.yMin, 1f);
            Assert.AreEqual(288f, corner.worldBound.width, 1f);
        }

        /// <web-test file="e2e/tests/modeHud/operation-mode-hud.spec.ts">削除箱の高さだけを右上に表示して更新する ${viewport.width}</web-test>
        [Test]
        public void 削除箱は高さだけを右上に出し更新しゲーム画面で消える()
        {
            _view.Render(ModeHudViewModelSamples.Delete(3));
            simulate.FrameUpdate();
            var box = Find<VisualElement>(_view, "delete-box-height-hud");
            Assert.AreEqual(ModeHudViewTest.DetailLineHeightTag + "Height: 3", box.Q<Label>().text);
            Assert.AreEqual(_logicalSize.x - 24f, box.worldBound.xMax, 1f);
            Assert.AreEqual(24f, box.worldBound.yMin, 1f);
            Assert.AreEqual(288f, box.worldBound.width, 1f);
            Assert.IsNull(_view.Q("placement-mode-hud"), "配置 HUD は木から外れる");

            // 対象が空でも高さだけが更新される
            // Only the height updates even with no target
            _view.Render(ModeHudViewModelSamples.Delete(0));
            Assert.AreEqual(ModeHudViewTest.DetailLineHeightTag + "Height: 0", box.Q<Label>().text);

            _view.Render(Client.Ui.State.ModeHud.ModeHudViewModel.Hidden);
            Assert.IsNull(box.parent, "非表示では削除箱を木から外す");
        }
    }
}
