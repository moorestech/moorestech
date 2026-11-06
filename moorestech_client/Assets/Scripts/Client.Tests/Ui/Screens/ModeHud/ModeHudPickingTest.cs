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
    /// 配置モード・削除モード警告・カーソルツールチップは、出ている真ん中でも下の入力を奪わない（L1。Web alwaysOnHudWheel.test.ts の 9 行のうちこのレーンの 3 行。残りは S2-hud の AlwaysOnHudPickingTest）
    /// The placement HUD, delete-mode warning and cursor tooltip never take input even at their centre (L1; this lane's 3 of the 9 rows in Web alwaysOnHudWheel.test.ts; the rest are S2-hud's AlwaysOnHudPickingTest)
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
            var view = Mount(new CursorTooltipView(new FakeCursorTooltipScreen()));
            view.Render(CursorTooltipViewModelSamples.Lines(new TooltipLine(LocalizationKeys.Ui.Tooltip.WorldTarget)));
            simulate.FrameUpdate();
            AssertPassesThrough(view, Find<VisualElement>(view, "cursor-tooltip").worldBound.center);
        }

        private static void AssertPassesThrough(VisualElement view, Vector2 point)
        {
            // 拾われるのは画面の外（下の層・ルート）だけで、画面の要素は拾われない
            // Only things outside the screen (lower layers, the root) are picked, never the screen's own elements
            var picked = view.panel.Pick(point);
            Assert.IsFalse(picked == view || (picked != null && view.Contains(picked)), picked == null ? "null" : picked.name);
        }
    }
}
