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
        public void ポーズ系3画面のMainPanelだけがGameUi所有で残りはWeb所有()
        {
            var table = new UiOwnershipTable();
            var gameUiScreens = new[] { UiScreen.PauseMenu, UiScreen.TrainPause, UiScreen.SkitPause };
            foreach (UiScreen screen in Enum.GetValues(typeof(UiScreen)))
            foreach (UiSurface surface in Enum.GetValues(typeof(UiSurface)))
            {
                var expected = surface == UiSurface.MainPanel && 0 <= Array.IndexOf(gameUiScreens, screen) ? UiSurfaceOwner.GameUi : UiSurfaceOwner.Web;
                Assert.AreEqual(expected, table.GetOwner(screen, surface), $"{screen}/{surface}");
            }
        }

        [Test]
        public void Editor上書きでPauseMenuのMainPanelがWebになりClearで戻る()
        {
            var table = new UiOwnershipTable();
            UiOwnershipEditorOverride.Set(UiScreen.PauseMenu, UiSurface.MainPanel, UiSurfaceOwner.Web);
            Assert.AreEqual(UiSurfaceOwner.Web, table.GetOwner(UiScreen.PauseMenu, UiSurface.MainPanel));
            Assert.AreEqual(UiSurfaceOwner.GameUi, table.GetOwner(UiScreen.TrainPause, UiSurface.MainPanel));

            UiOwnershipEditorOverride.Clear();
            Assert.AreEqual(UiSurfaceOwner.GameUi, table.GetOwner(UiScreen.PauseMenu, UiSurface.MainPanel));
        }
    }
}
