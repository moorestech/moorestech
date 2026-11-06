using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Screens.ModeHud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// 削除モードは説明パネルではなく上下の高さ 20px の警告帯 2 本で、下の帯に錨 delete.hud（L1。Web の operation-mode-hud.spec.ts）
    /// Delete mode is two 20px warning bands at the top and bottom instead of a panel, the bottom one anchored as delete.hud (L1; Web operation-mode-hud.spec.ts)
    /// </summary>
    public class DeleteModeWarningBandsTest : ScreenTestHost
    {
        /// <web-test file="e2e/tests/modeHud/operation-mode-hud.spec.ts">削除モードをuGUI準拠の上下警告帯で表示する</web-test>
        [Test]
        public void 削除モードは上端と下端の警告帯2本で描き拾わない()
        {
            var view = Mount(new ModeHudView());
            view.Render(ModeHudViewModelSamples.Delete(3));
            simulate.FrameUpdate();

            // 配置 HUD は出ず、帯は 2 本で上端 0 と下端 0・高さ 20・全幅
            // No placement HUD; two bands at top 0 and bottom 0, 20px tall, full width
            Assert.IsNull(view.Q("placement-mode-hud"), "配置 HUD は木から外れる");
            var root = Find<VisualElement>(view, "delete-mode-warning");
            Assert.AreEqual(PickingMode.Ignore, root.pickingMode);
            var bands = root.Query<VisualElement>("delete-mode-warning-band").ToList();
            Assert.AreEqual(2, bands.Count);
            Assert.AreEqual(0f, bands[0].worldBound.yMin, 0.5f);
            Assert.AreEqual(720f, bands[1].worldBound.yMax, 0.5f);
            Assert.AreEqual(20f, bands[0].worldBound.height, 0.5f);
            Assert.AreEqual(1280f, bands[0].worldBound.width, 0.5f);

            // 黒は USS の背景。黄と角度と刻みは帯のクラスで受け直したトークン
            // Black is the USS background; yellow, angle and stripe size are tokens re-declared on the band class
            Assert.AreEqual(new Color(0f, 0f, 0f, 1f), bands[0].resolvedStyle.backgroundColor);
            Assert.IsTrue(bands[0].customStyle.TryGetValue(new CustomStyleProperty<Color>("--mode-hud-band-yellow"), out var yellow));
            Assert.AreEqual(new Color(1f, 187f / 255f, 36f / 255f, 1f), yellow);
            Assert.IsTrue(Client.Ui.Screens.ModeHud.Parts.DegreeCustomStyle.TryReadDegrees(bands[0].customStyle, "--mode-hud-band-stripe-angle", out var angle));
            Assert.AreEqual(117f, angle, 0.001f);

            // 錨 delete.hud は下の帯だけに付く
            // The delete.hud anchor sits only on the bottom band
            Assert.IsFalse(bands[0].ClassListContains(AnchorIds.DeleteHud));
            Assert.IsTrue(bands[1].ClassListContains(AnchorIds.DeleteHud));
        }
    }
}
