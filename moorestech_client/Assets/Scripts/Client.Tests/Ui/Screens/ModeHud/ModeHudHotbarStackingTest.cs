using System;
using System.IO;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Hotbar;
using Client.Ui.Screens.KeyHints;
using Client.Ui.Screens.ModeHud;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// 帯・ホットバー・ヒントの重ね順が Web と同じ（L1）
    /// Band, hotbar and hint stacking matches the Web (L1)
    /// </summary>
    public class ModeHudHotbarStackingTest : ScreenTestHost
    {
        private const string MainRegistrationPath = "Scripts/Client.Starter/Registration/MainGameUiRegistration.cs";

        [Test]
        public void ホットバーはキー操作ヒントと同じChrome層で帯はその下のOverlayPanel層()
        {
            // Web の viewportOverlay は z stage-overlay-panel-chrome の積み重ね文脈で、ホットバーもキーヒントもその中。帯は中の先頭の子
            // On the Web viewportOverlay is a stacking context at z stage-overlay-panel-chrome holding the hotbar and key hints; the bands are its first child
            Assert.AreEqual(UiLayer.OverlayPanel, ModeHudPresenter.HostLayer);
            Assert.AreEqual(UiLayer.Chrome, HotbarHudPresenter.HostLayer);
            Assert.AreEqual(UiLayer.Chrome, KeyHintHudPresenter.HostLayer);
            Assert.AreEqual(UiFamily.Viewport, ModeHudPresenter.HostFamily);
            Assert.AreEqual(UiFamily.Viewport, HotbarHudPresenter.HostFamily);
            Assert.AreEqual(UiFamily.Viewport, KeyHintHudPresenter.HostFamily);

            // 帯はホットバーより後ろ
            // The bands stay behind the hotbar
            var stack = UiLayerStack.Build(rootVisualElement);
            Assert.Less(IndexOfLayer(stack, ModeHudPresenter.HostLayer), IndexOfLayer(stack, HotbarHudPresenter.HostLayer));
        }

        [Test]
        public void ホットバーは舞台の一覧より前で掴みとツールチップとポーズより後ろ()
        {
            // Web は一覧（.stage の z stage-overlay-panel）より viewportOverlay（31）が前、掴み 40・ポーズ 40・ツールチップ 250 が更に前
            // On the Web viewportOverlay (31) is in front of stage lists (z stage-overlay-panel); grab 40, pause 40 and tooltip 250 are further in front
            var stack = UiLayerStack.Build(rootVisualElement);
            var hotbar = IndexOfLayer(stack, HotbarHudPresenter.HostLayer);
            Assert.Less(IndexOfLayer(stack, UiLayer.OverlayPanel), hotbar, "チャレンジ一覧・研究の層がホットバーより前にある");
            Assert.Less(hotbar, IndexOfLayer(stack, UiLayer.Grab));
            Assert.Less(hotbar, IndexOfLayer(stack, UiLayer.PauseMenu));
            Assert.Less(hotbar, IndexOfLayer(stack, UiLayer.Tooltip));
        }

        [Test]
        public void キー操作ヒントはホットバーより後に登録され前に描かれる()
        {
            // 入口は登録順に Initialize され、層へ足した順に兄弟が並ぶ。Web はヒント z31 > ホットバー z30
            // Entry points initialize in registration order and siblings follow add order; on the Web the hint is z31 > hotbar z30
            var source = File.ReadAllText(Path.Combine(Application.dataPath, MainRegistrationPath));
            // 前の空白まで含めて ModeHudGameUiRegistration の部分一致を避ける
            // Include the leading space so ModeHudGameUiRegistration does not match as a substring
            var hotbar = source.IndexOf(" HotbarGameUiRegistration.Register(builder);", StringComparison.Ordinal);
            var hud = source.IndexOf(" HudGameUiRegistration.Register(builder);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(hotbar, 0, "ホットバーの登録が見つからない");
            Assert.GreaterOrEqual(hud, 0, "常駐 HUD（キー操作ヒント）の登録が見つからない");
            Assert.Less(hotbar, hud, "ホットバーが常駐 HUD より後に登録されている（ホットバーがキー操作ヒントの上に描かれる）");
        }

        private int IndexOfLayer(UiLayerStack stack, UiLayer layer)
        {
            return rootVisualElement.IndexOf(stack.GetLayer(layer, UiFamily.Viewport).parent);
        }
    }
}
