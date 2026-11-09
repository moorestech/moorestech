using System;
using System.Collections.Generic;
using Client.Tests.Ui.Components;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Stage;
using Client.Ui.Screens.Shell;
using Client.Ui.State.Routing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Shell
{
    /// <summary>
    /// 幕を出す画面が traits の Backdrop 列で Web と同じこと、View が幕を出し入れし実画面全面をヴィネットの手前で覆うことを確かめる（L1。設計書 A-44。Presenter の結線は録画 uitk-hud が見る）
    /// Verifies the traits Backdrop column matches the Web on every screen and the view toggles the dim over the whole viewport in front of the vignette (L1; design A-44; the recording uitk-hud checks the presenter wiring)
    /// </summary>
    public class ScreenBackdropViewTest : UiTestHost
    {
        // Web の uiScreenRouting.test.ts の screenShowsBackdrop の表（幕を出すのはモーダルな画面だけ）
        // The Web screenShowsBackdrop table in uiScreenRouting.test.ts (only modal screens show the dim)
        private static readonly Dictionary<UiScreen, bool> WebBackdropScreens = new()
        {
            { UiScreen.None, false },
            { UiScreen.PlayerInventory, true },
            { UiScreen.SubInventory, true },
            { UiScreen.ResearchTree, true },
            { UiScreen.BuildMenu, true },
            { UiScreen.ChallengeList, true },
            { UiScreen.PauseMenu, true },
            { UiScreen.TrainHud, false },
            { UiScreen.TrainPause, true },
            { UiScreen.SkitPause, true },
        };

        private ScreenBackdropView _view;

        [SetUp]
        public void MountView()
        {
            var stack = UiLayerStack.Build(rootVisualElement);
            _view = new ScreenBackdropView();
            stack.GetLayer(UiLayer.Backdrop, UiFamily.Viewport).Add(_view);
            simulate.FrameUpdate();
        }

        [Test]
        public void 幕を出す画面はtraitsのBackdrop列でWebの表と同じ()
        {
            // Presenter が読むのはこの列だけ（設計書 §4.4・A-44）。全画面の行を Web の表と突き合わせる
            // The presenter reads only this column (design §4.4, A-44); check every screen row against the Web table
            foreach (UiScreen screen in Enum.GetValues(typeof(UiScreen)))
            {
                Assert.IsTrue(WebBackdropScreens.ContainsKey(screen), $"Web の表に {screen} が無い");
                Assert.AreEqual(WebBackdropScreens[screen], UiScreenTraitsTable.Get(screen).Backdrop, screen.ToString());
            }
        }

        [Test]
        public void 幕は隠れて生まれSetBackdropShownで出て消える()
        {
            var backdrop = _view.Q("screen-backdrop");
            Assert.AreEqual(DisplayStyle.None, backdrop.resolvedStyle.display);

            // 出してから隠すと、幕が出て消える
            // Showing then hiding makes the dim appear and disappear
            _view.SetBackdropShown(true);
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.Flex, backdrop.resolvedStyle.display);
            _view.SetBackdropShown(false);
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.None, backdrop.resolvedStyle.display);
        }

        [Test]
        public void 幕は実画面全面をヴィネットの手前で覆い入力を奪わない()
        {
            _view.SetBackdropShown(true);
            simulate.FrameUpdate();
            var vignette = _view.Q("screen-vignette");
            var backdrop = _view.Q("screen-backdrop");

            // 後に足した子が前。幕の色は黒の幕の式で補正したトークン（A-130 の 2.15 で 0.42 → 0.69）
            // Later children draw in front; the dim color is the alpha-corrected token
            Assert.Less(_view.IndexOf(vignette), _view.IndexOf(backdrop));
            Assert.AreEqual(rootVisualElement.worldBound.width, backdrop.worldBound.width, 0.05f);
            Assert.AreEqual(rootVisualElement.worldBound.height, backdrop.worldBound.height, 0.05f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--screen-backdrop-face"), backdrop.resolvedStyle.backgroundColor, "backdrop face");
            Assert.AreEqual(0.69f, backdrop.resolvedStyle.backgroundColor.a, 1e-3f);
            Assert.AreEqual(0.42f, 1f - Mathf.Pow(1f - backdrop.resolvedStyle.backgroundColor.a, 1f / 2.15f), 0.001f);
            foreach (var element in _view.Query<VisualElement>().ToList()) Assert.AreEqual(PickingMode.Ignore, element.pickingMode, element.name);
        }
    }
}
