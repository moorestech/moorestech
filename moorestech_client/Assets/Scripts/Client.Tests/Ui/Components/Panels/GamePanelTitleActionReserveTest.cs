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
    /// 題の操作を持つ間だけ題の右に操作の幅と間（Web calc(--panel-action-button-width + --mantine-spacing-sm)）を空けることを確かめる（L1）
    /// Verifies the title reserves the action width plus gap (Web calc(--panel-action-button-width + --mantine-spacing-sm)) only while it holds an action (L1)
    /// </summary>
    public class GamePanelTitleActionReserveTest : UiTestHost
    {
        // Web の 87px（--panel-action-button-width）+ 12px（--mantine-spacing-sm）
        // The Web 87px (--panel-action-button-width) plus 12px (--mantine-spacing-sm)
        private const float WebTitleActionReserve = 87f + 12f;

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
        public void 題の操作を持つ間は題の右にWebと同じ幅を空ける()
        {
            _panel.SetTitleAction(new VisualElement { name = "test-title-action" });
            simulate.FrameUpdate();
            Assert.AreEqual(WebTitleActionReserve, Title().resolvedStyle.paddingRight, 0.01f);
        }

        [Test]
        public void 題の操作を外すと操作も右の逃げも消える()
        {
            var action = new VisualElement { name = "test-title-action" };
            _panel.SetTitleAction(action);
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
