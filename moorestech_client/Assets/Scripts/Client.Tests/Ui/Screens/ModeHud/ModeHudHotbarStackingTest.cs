using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Hotbar;
using Client.Ui.Screens.KeyHints;
using Client.Ui.Screens.ModeHud;
using NUnit.Framework;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// 削除の帯・ホットバー・キー操作ヒントの重ね順が Web（App.tsx の viewportOverlay と z）と同じこと（L1）
    /// The stacking of delete bands, hotbar and key hints matches the Web (App.tsx viewportOverlay and z) (L1)
    /// </summary>
    public class ModeHudHotbarStackingTest : ScreenTestHost
    {
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

            // 層の容器は後ろほど前に描かれるので、帯はホットバーより後ろ
            // Later layer containers draw in front, so the bands stay behind the hotbar
            var stack = UiLayerStack.Build(rootVisualElement);
            Assert.Less(IndexOfLayer(stack, ModeHudPresenter.HostLayer), IndexOfLayer(stack, HotbarHudPresenter.HostLayer));
        }

        [Test]
        public void ホットバーは舞台の一覧より前で掴み・ツールチップ・ポーズより後ろ()
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

        private int IndexOfLayer(UiLayerStack stack, UiLayer layer)
        {
            return rootVisualElement.IndexOf(stack.GetLayer(layer, UiFamily.Viewport).parent);
        }
    }
}
