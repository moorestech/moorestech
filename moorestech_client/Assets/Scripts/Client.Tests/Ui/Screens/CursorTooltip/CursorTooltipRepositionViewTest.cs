using System.Text.RegularExpressions;
using Client.Game.InGame.UI.Tooltip;
using Client.Ui.Screens.CursorTooltip;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// ポインタが動かなくても、ロック切替・倍率変化で置き直す（L1）
    /// Re-placed on lock toggles and scale changes even without pointer motion (L1)
    /// </summary>
    public class CursorTooltipRepositionViewTest : ScreenTestHost
    {
        private static readonly Vector2 GameUiPanelSize = new(1280f, 720f);

        private FakeCursorTooltipScreen _screen;
        private CursorTooltipView _view;

        [SetUp]
        public void MountTooltip()
        {
            _screen = new FakeCursorTooltipScreen();
            _view = Mount(new CursorTooltipView(_screen, _screen, _screen.StageScale));
        }

        [TearDown]
        public void RestorePanelSize()
        {
            panelSize = GameUiPanelSize;
        }

        [Test]
        public void 表示中にロックが切り替わるとポインタが動かなくても置き直す()
        {
            _screen.MovePointer(new Vector2(1000f, 100f));
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.WorldTarget)));
            simulate.FrameUpdate();
            var face = Find<VisualElement>(_view, "cursor-tooltip");
            Assert.AreEqual(1012f, face.worldBound.xMin, 0.5f);

            // ロックで中央へ、解除で元のポインタへ戻る
            // Lock moves it to the centre, unlock returns it to the pointer
            _screen.SetLocked(new Vector2(640f, 360f));
            simulate.FrameUpdate();
            Assert.AreEqual(652f, face.worldBound.xMin, 0.5f);
            Assert.AreEqual(372f, face.worldBound.yMin, 0.5f);

            _screen.Unlock();
            simulate.FrameUpdate();
            Assert.AreEqual(1012f, face.worldBound.xMin, 0.5f);
            Assert.AreEqual(112f, face.worldBound.yMin, 0.5f);

            // パネルから外れたらロックの購読も切る
            // Once detached the lock subscription ends too
            Assert.IsTrue(_screen.HasAnchorModeSubscribers);
            _view.RemoveFromHierarchy();
            Assert.IsFalse(_screen.HasAnchorModeSubscribers);
        }

        [Test]
        public void 倍率が変わると保存した実画面のポインタをいまの倍率で換算し直す()
        {
            _screen.MovePointer(new Vector2(200f, 300f));
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.WorldTarget)));
            simulate.FrameUpdate();
            var face = Find<VisualElement>(_view, "cursor-tooltip");
            Assert.AreEqual(212f, face.worldBound.xMin, 0.5f);

            // 倍率 2 でパネルが半分になる。実画面 (200,300)+12 はパネルの (106,156)
            // Scale 2 halves the panel; screen (200,300)+12 is panel (106,156)
            _screen.SetScale(2f);
            panelSize = GameUiPanelSize / 2f;
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.AreEqual(106f, face.worldBound.xMin, 0.5f);
            Assert.AreEqual(156f, face.worldBound.yMin, 0.5f);
        }

        [Test]
        public void 倍率だけが変わってもパネルの形の変化を待たず置き直す()
        {
            _screen.MovePointer(new Vector2(200f, 300f));
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.WorldTarget)));
            simulate.FrameUpdate();
            var face = Find<VisualElement>(_view, "cursor-tooltip");

            // パネルの大きさは変えず倍率だけ流す。形の変化の通知が来ないので、置き直すのは倍率の購読だけ
            // Push only the scale with the panel size unchanged; no geometry event fires, so only the scale subscription re-places it
            _screen.SetScale(2f);
            simulate.FrameUpdate();
            Assert.AreEqual(106f, face.worldBound.xMin, 0.5f);
            Assert.AreEqual(156f, face.worldBound.yMin, 0.5f);
        }

        [Test]
        public void 倍率が正でない間のErrorは入ったとき1回で正に戻ると次はまた出る()
        {
            _screen.MovePointer(new Vector2(200f, 300f));
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.WorldTarget)));
            simulate.FrameUpdate();

            // 入口で 1 回、再配置では出ない
            // Once on entry, not on re-placement
            LogAssert.Expect(LogType.Error, new Regex("stage scale is not positive"));
            _screen.SetScale(0f);
            simulate.FrameUpdate();
            _screen.MovePointer(new Vector2(250f, 300f));
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();

            // 正で旗が戻り、再び 0 で出る
            // Positive clears flag; a new zero reports again
            _screen.SetScale(1f);
            simulate.FrameUpdate();
            LogAssert.Expect(LogType.Error, new Regex("stage scale is not positive"));
            _screen.SetScale(0f);
            simulate.FrameUpdate();
        }
    }
}
