using System;
using System.IO;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Hotbar;
using Client.Ui.Screens.KeyHints;
using Client.Ui.Screens.ModeHud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// 削除の帯・ホットバー・キー操作ヒントの重ね順が Web（App.tsx の DOM 順と z）と同じこと（L1）
    /// The stacking of delete bands, hotbar and key hints matches the Web (App.tsx DOM order and z) (L1)
    /// </summary>
    public class ModeHudHotbarStackingTest : ScreenTestHost
    {
        private const string MainRegistrationPath = "Scripts/Client.Starter/Registration/MainGameUiRegistration.cs";

        [Test]
        public void 帯とホットバーは同じOverlayPanel層でキー操作ヒントはその上のChrome層()
        {
            // Web は帯・ホットバーが z stage-overlay-panel、キー操作ヒントが z stage-overlay-panel-chrome
            // On the Web the bands and hotbar are z stage-overlay-panel and the key hints z stage-overlay-panel-chrome
            Assert.AreEqual(UiLayer.OverlayPanel, ModeHudPresenter.HostLayer);
            Assert.AreEqual(UiLayer.OverlayPanel, HotbarHudPresenter.HostLayer);
            Assert.AreEqual(UiLayer.Chrome, KeyHintHudPresenter.HostLayer);
            Assert.AreEqual(UiFamily.Viewport, ModeHudPresenter.HostFamily);
            Assert.AreEqual(UiFamily.Viewport, HotbarHudPresenter.HostFamily);
            Assert.AreEqual(UiFamily.Viewport, KeyHintHudPresenter.HostFamily);

            // 層の容器は後ろほど前に描かれる
            // Later layer containers draw in front
            var stack = UiLayerStack.Build(rootVisualElement);
            Assert.Less(IndexOfLayer(stack, HotbarHudPresenter.HostLayer), IndexOfLayer(stack, KeyHintHudPresenter.HostLayer));
        }

        [Test]
        public void 操作モードHUDの登録はホットバーより先で帯が前の兄弟になる()
        {
            // 入口は登録順に Initialize され、層へ足した順に兄弟が並ぶ（同じ登録口の GameUiSrgbCompositeInstaller もこの順に頼る）。Web の App.tsx も帯がホットバーより先
            // Entry points initialize in registration order and siblings follow add order (GameUiSrgbCompositeInstaller in the same file relies on it); App.tsx also puts the bands before the hotbar
            var source = File.ReadAllText(Path.Combine(Application.dataPath, MainRegistrationPath));
            var modeHud = source.IndexOf("ModeHudGameUiRegistration.Register(builder);", StringComparison.Ordinal);
            var hotbar = source.IndexOf("HotbarGameUiRegistration.Register(builder);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(modeHud, 0, "操作モード HUD の登録が見つからない");
            Assert.GreaterOrEqual(hotbar, 0, "ホットバーの登録が見つからない");
            Assert.Less(modeHud, hotbar, "操作モード HUD がホットバーより後に登録されている（帯がホットバーの上に描かれる）");
        }

        [Test]
        public void 帯や配置HUDを描いても層の中の位置を動かさない()
        {
            // 本番と同じ順（操作モード HUD → ホットバー）で層へ足す。ホットバーは後の兄弟の代わりの要素で表す
            // Add in production order (mode HUD then hotbar); a stand-in element plays the later hotbar sibling
            var stack = UiLayerStack.Build(rootVisualElement);
            var layer = stack.GetLayer(ModeHudPresenter.HostLayer, ModeHudPresenter.HostFamily);
            var view = new ModeHudView();
            var laterHotbar = new VisualElement { name = "hotbar-stand-in" };
            layer.Add(view);
            layer.Add(laterHotbar);

            // 削除・配置のどれを描いても、帯はホットバーより前の兄弟のまま
            // Whether drawing delete or placement, the bands stay the earlier sibling
            foreach (var viewModel in new[] { ModeHudViewModelSamples.Delete(1), ModeHudViewModelSamples.Placement(), ModeHudViewModelSamples.Delete(2) })
            {
                view.Render(viewModel);
                simulate.FrameUpdate();
                Assert.Less(layer.IndexOf(view), layer.IndexOf(laterHotbar), viewModel.GetType().Name);
            }
        }

        private int IndexOfLayer(UiLayerStack stack, UiLayer layer)
        {
            return rootVisualElement.IndexOf(stack.GetLayer(layer, UiFamily.Viewport).parent);
        }
    }
}
