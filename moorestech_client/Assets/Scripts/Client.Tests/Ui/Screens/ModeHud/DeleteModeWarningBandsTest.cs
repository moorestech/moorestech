using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Ui.Screens.ModeHud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// 削除モードは高さ 20px の警告帯 2 本（L1）
    /// Delete mode is two 20px warning bands (L1)
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

            // 上端 0・下端 0・高さ 20・全幅
            // Top 0, bottom 0, 20px tall, full width
            Assert.IsNull(view.Q("placement-mode-hud"), "配置 HUD は木から外れる");
            var root = Find<VisualElement>(view, "delete-mode-warning");
            Assert.AreEqual(PickingMode.Ignore, root.pickingMode);
            var bands = root.Query<VisualElement>("delete-mode-warning-band").ToList();
            Assert.AreEqual(2, bands.Count);
            Assert.AreEqual(0f, bands[0].worldBound.yMin, 0.5f);
            Assert.AreEqual(720f, bands[1].worldBound.yMax, 0.5f);
            Assert.AreEqual(20f, bands[0].worldBound.height, 0.5f);
            Assert.AreEqual(1280f, bands[0].worldBound.width, 0.5f);

            // 黒は USS、黄・角度・刻みはトークン
            // Black is USS; yellow, angle, size are tokens
            Assert.AreEqual(new Color(0f, 0f, 0f, 1f), bands[0].resolvedStyle.backgroundColor);
            Assert.IsTrue(bands[0].customStyle.TryGetValue(new CustomStyleProperty<Color>("--mode-hud-band-yellow"), out var yellow));
            Assert.AreEqual(new Color(1f, 187f / 255f, 36f / 255f, 1f), yellow);
            Assert.IsTrue(Client.Ui.Foundation.Theme.UssCustomStyleReader.TryReadDegrees(bands[0].customStyle, "--mode-hud-band-stripe-angle", out var angle));
            Assert.AreEqual(117f, angle, 0.001f);

            // 錨 delete.hud は下の帯だけ
            // delete.hud anchors the bottom band only
            Assert.IsFalse(bands[0].ClassListContains(AnchorIds.DeleteHud));
            Assert.IsTrue(bands[1].ClassListContains(AnchorIds.DeleteHud));
        }
    }
}
