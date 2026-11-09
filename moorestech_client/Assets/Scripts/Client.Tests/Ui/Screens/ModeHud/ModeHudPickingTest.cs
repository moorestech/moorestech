using Client.Game.InGame.UI.Tooltip;
using Client.Tests.Ui.Screens.CursorTooltip;
using Client.Ui.Screens.CursorTooltip;
using Client.Ui.Screens.ModeHud;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// 配置 HUD・削除警告・ツールチップは入力を奪わない（L1）
    /// Placement HUD, delete warning and tooltip never take input (L1)
    /// </summary>
    public class ModeHudPickingTest : ScreenTestHost
    {
        /// <web-test file="src/features/commonHud/alwaysOnHudWheel.test.ts">%s は入力を奪わない</web-test>
        [Test]
        public void 配置モードHUDは入力を奪わない()
        {
            var view = Mount(new ModeHudView());
            view.Render(ModeHudViewModelSamples.Placement());
            simulate.FrameUpdate();
            AssertPassesThrough(view, Find<VisualElement>(view, "placement-mode-hud").worldBound.center);
        }

        /// <web-test file="src/features/commonHud/alwaysOnHudWheel.test.ts">%s は入力を奪わない</web-test>
        [Test]
        public void 削除モード警告は入力を奪わない()
        {
            var view = Mount(new ModeHudView());
            view.Render(ModeHudViewModelSamples.Delete(3));
            simulate.FrameUpdate();
            AssertPassesThrough(view, Find<VisualElement>(view, "delete-mode-warning-band").worldBound.center);
        }

        /// <web-test file="src/features/commonHud/alwaysOnHudWheel.test.ts">%s は入力を奪わない</web-test>
        [Test]
        public void カーソルツールチップは入力を奪わない()
        {
            var view = Mount(NewViewWithFakeScreen());
            view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.WorldTarget)));
            simulate.FrameUpdate();
            AssertPassesThrough(view, Find<VisualElement>(view, "cursor-tooltip").worldBound.center);
        }

        private static void AssertPassesThrough(VisualElement view, Vector2 point)
        {
            // 拾われるのは画面の外だけ
            // Only things outside the screen are picked
            var picked = view.panel.Pick(point);
            Assert.IsFalse(picked == view || (picked != null && view.Contains(picked)), picked == null ? "null" : picked.name);
        }

        private static CursorTooltipView NewViewWithFakeScreen()
        {
            var screen = new FakeCursorTooltipScreen();
            return new CursorTooltipView(screen, screen, screen.StageScale);
        }
    }
}
