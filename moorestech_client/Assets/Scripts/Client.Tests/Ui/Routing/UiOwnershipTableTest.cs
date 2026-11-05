using System;
using Client.Ui.State.Routing;
using NUnit.Framework;

namespace Client.Tests.Ui.Routing
{
    public class UiOwnershipTableTest
    {
        [TearDown]
        public void TearDown()
        {
            // 上書きは静的状態なので他テストへ漏らさない
            // Overrides are static state, so never leak them into other tests
            UiOwnershipEditorOverride.Clear();
        }

        [Test]
        public void 段階1では全画面のMainPanelがWeb所有()
        {
            var table = new UiOwnershipTable();
            foreach (UiScreen screen in Enum.GetValues(typeof(UiScreen)))
            foreach (UiSurface surface in Enum.GetValues(typeof(UiSurface)))
            {
                Assert.AreEqual(UiSurfaceOwner.Web, table.GetOwner(screen, surface), $"{screen}/{surface}");
            }
        }

        [Test]
        public void Editor上書きでPauseMenuのMainPanelがGameUiになりClearで戻る()
        {
            var table = new UiOwnershipTable();
            UiOwnershipEditorOverride.Set(UiScreen.PauseMenu, UiSurface.MainPanel, UiSurfaceOwner.GameUi);
            Assert.AreEqual(UiSurfaceOwner.GameUi, table.GetOwner(UiScreen.PauseMenu, UiSurface.MainPanel));
            Assert.AreEqual(UiSurfaceOwner.Web, table.GetOwner(UiScreen.BuildMenu, UiSurface.MainPanel));

            UiOwnershipEditorOverride.Clear();
            Assert.AreEqual(UiSurfaceOwner.Web, table.GetOwner(UiScreen.PauseMenu, UiSurface.MainPanel));
        }
    }
}
