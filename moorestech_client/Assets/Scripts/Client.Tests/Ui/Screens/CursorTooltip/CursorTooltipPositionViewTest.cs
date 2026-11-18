using Client.Game.InGame.UI.Tooltip;
using Client.Localization;
using Client.Ui.Screens.CursorTooltip;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// ツールチップの位置: 右下 12px・端で押し戻す・ロック中は中央（L1）
    /// Tooltip position: 12px below-right, pushed back at edges, centre while locked (L1)
    /// </summary>
    public class CursorTooltipPositionViewTest : ScreenTestHost
    {
        private FakeCursorTooltipScreen _screen;
        private CursorTooltipView _view;

        [SetUp]
        public void MountTooltip()
        {
            _screen = new FakeCursorTooltipScreen();
            _view = Mount(new CursorTooltipView(_screen, _screen, _screen.StageScale));
        }

        /// <web-test file="src/shared/tooltip/CursorTooltip.test.ts">positions from the latest pointer when the tooltip first appears without further pointer moves</web-test>
        /// <web-test file="e2e/tests/system/commonHud.spec.ts">ツールチップは非表示中に届いたpointer位置へ初回表示から出る</web-test>
        [Test]
        public void 非表示中に動いたポインタの位置へ最初の表示から出る()
        {
            _screen.MovePointer(new Vector2(640f, 360f));
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.WorldTarget)));
            simulate.FrameUpdate();
            var face = Find<VisualElement>(_view, "cursor-tooltip");
            Assert.AreEqual(652f, face.worldBound.xMin, 0.5f);
            Assert.AreEqual(372f, face.worldBound.yMin, 0.5f);
        }

        /// <web-test file="src/shared/tooltip/CursorTooltip.test.ts">recalculates position when locale changes the resolved text</web-test>
        [Test]
        public void 言語で幅が変わると右端の押し戻しを置き直す()
        {
            _screen.MovePointer(new Vector2(1270f, 100f));
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.WorldTarget)));
            simulate.FrameUpdate();
            var face = Find<VisualElement>(_view, "cursor-tooltip");
            Assert.AreEqual(1280f - 12f, face.worldBound.xMax, 0.5f);

            Localize.TrySetChosenLanguage("japanese");
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.AreEqual(1280f - 12f, face.worldBound.xMax, 0.5f, "幅が変わっても右端から 12px");
        }

        [Test]
        public void ロック中は実画面の問い合わせが返す中央を指したものとして置く()
        {
            _screen.MovePointer(new Vector2(1240f, 40f));
            _screen.SetLocked(new Vector2(640f, 360f));
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.WorldTarget)));
            simulate.FrameUpdate();
            var face = Find<VisualElement>(_view, "cursor-tooltip");
            Assert.AreEqual(652f, face.worldBound.xMin, 0.5f);
            Assert.AreEqual(372f, face.worldBound.yMin, 0.5f);
        }

        [Test]
        public void 載った時は今のポインタを初期位置にする()
        {
            _screen.SetCurrentPointer(new Vector2(100f, 200f));
            var view = Mount(new CursorTooltipView(_screen, _screen, _screen.StageScale));
            view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.WorldTarget)));
            simulate.FrameUpdate();
            Assert.AreEqual(112f, view.Q<VisualElement>("cursor-tooltip").worldBound.xMin, 0.5f);
            Assert.AreEqual(212f, view.Q<VisualElement>("cursor-tooltip").worldBound.yMin, 0.5f);
        }

        [Test]
        public void 表示中にポインタが動くと面が付いて動き外れたら追わない()
        {
            _view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.WorldTarget)));
            _screen.MovePointer(new Vector2(100f, 100f));
            simulate.FrameUpdate();
            var face = Find<VisualElement>(_view, "cursor-tooltip");
            Assert.AreEqual(112f, face.worldBound.xMin, 0.5f);

            // 動いた先へ置き直す
            // Re-place at the moved position
            _screen.MovePointer(new Vector2(300f, 200f));
            simulate.FrameUpdate();
            Assert.AreEqual(312f, face.worldBound.xMin, 0.5f);
            Assert.AreEqual(212f, face.worldBound.yMin, 0.5f);

            // パネルから外れたらポインタの購読を切る
            // Once detached the pointer subscription ends
            Assert.IsTrue(_screen.HasPointerSubscribers);
            _view.RemoveFromHierarchy();
            Assert.IsFalse(_screen.HasPointerSubscribers);
        }
    }
}
