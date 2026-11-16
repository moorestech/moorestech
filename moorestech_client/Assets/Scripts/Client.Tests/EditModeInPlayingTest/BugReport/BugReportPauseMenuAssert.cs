using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.BugReport
{
    internal static class BugReportPauseMenuAssert
    {
        // 送信後はポーズを閉じずにトップへ戻る（ADR 0069）
        // After a send the pause stays open and returns to its top (ADR 0069)
        internal static async UniTask AssertReturnedToTop(IObjectResolver resolver)
        {
            var uiState = resolver.Resolve<UIStateControl>();
            var pauseMenu = resolver.Resolve<PauseMenuStateService>();
            for (var i = 0; i < 40 && pauseMenu.CurrentPage.Value != PauseMenuPage.Top; i++) await UniTask.Delay(50);
            Assert.AreEqual(PauseMenuPage.Top, pauseMenu.CurrentPage.Value, "送信後にポーズメニューのトップへ戻っていない");
            Assert.AreEqual(UIStateEnum.PauseMenu, uiState.CurrentState, "送信後にポーズメニューが閉じている");
        }
    }
}
