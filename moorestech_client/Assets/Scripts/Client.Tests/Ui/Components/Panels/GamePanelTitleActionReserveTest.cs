using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Panels;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Panels
{
    /// <summary>
    /// 逃げを頼んだ間だけ題の右が空き、題の操作を載せるだけでは空かないことの L1
    /// L1: the title reserves room only while asked, not merely because it holds an action
    /// </summary>
    public class GamePanelTitleActionReserveTest : UiTestHost
    {
        private GamePanel _panel;

        [SetUp]
        public void AddTitledPanel()
        {
            Localize.Initialize();
            _panel = new GamePanel("test-game-panel", GamePanelFrame.Default);
            _panel.style.width = 400;
            _panel.style.height = 300;
            _panel.SetTitle(new UiText(LocalizationKeys.Ui.Inventory.Title));
            rootVisualElement.Add(_panel);
        }

        [Test]
        public void 題の操作が無い題は右を空けない()
        {
            simulate.FrameUpdate();
            Assert.AreEqual(0f, Title().resolvedStyle.paddingRight, 0.01f);
        }

        [Test]
        public void 題の操作を載せるだけでは右を空けない()
        {
            // 持ち物パネルの並べ替えは Web で流れの中に置かれ、題の右を空けない
            // The inventory sort button flows inline on the Web and reserves no room
            _panel.SetTitleAction(new VisualElement { name = "test-title-action" });
            simulate.FrameUpdate();
            Assert.AreEqual(0f, Title().resolvedStyle.paddingRight, 0.01f);
        }

        [Test]
        public void 逃げを頼んだ間は題の右を空ける()
        {
            _panel.SetTitleAction(new VisualElement { name = "test-title-action" });
            _panel.SetTitleActionReserve(true);
            simulate.FrameUpdate();
            // 幅の値は CalcConstantRecheckTest が --panel-action-button-width との和で守る。ここは逃げが付くことだけを見る
            // CalcConstantRecheckTest guards the value against --panel-action-button-width; this only checks that a reserve is applied
            Assert.Greater(Title().resolvedStyle.paddingRight, 0f);
        }

        [Test]
        public void 題の操作を外すと操作も右の逃げも消える()
        {
            var action = new VisualElement { name = "test-title-action" };
            _panel.SetTitleAction(action);
            _panel.SetTitleActionReserve(true);
            _panel.ClearTitleAction();
            simulate.FrameUpdate();
            Assert.IsNull(action.parent);
            Assert.IsFalse(_panel.ClassListContains(GamePanel.TitleActionUssClassName));
            Assert.AreEqual(0f, Title().resolvedStyle.paddingRight, 0.01f);
        }

        private VisualElement Title()
        {
            return _panel.Q(className: "game-panel__title");
        }
    }
}
