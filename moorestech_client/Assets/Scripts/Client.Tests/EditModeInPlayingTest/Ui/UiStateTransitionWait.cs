using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    /// <summary>
    /// GameUi の L3 が画面を遷移させて到達を待つ口（UiSystemPlayModeTestBase から移した）
    /// Lets GameUi L3 tests request a screen transition and wait until it is reached (moved from UiSystemPlayModeTestBase)
    /// </summary>
    public static class UiStateTransitionWait
    {
        private const int TransitionWaitFrameLimit = 120;

        // 遷移要求は次の Update で消費されるので、到達するまで 1 フレームずつ待つ
        // The request is consumed on a later Update, so wait frame by frame until the state is reached
        public static async UniTask TransitionTo(UIStateControl uiStateControl, UIStateEnum state)
        {
            uiStateControl.RequestTransition(state);
            for (var i = 0; i < TransitionWaitFrameLimit && uiStateControl.CurrentState != state; i++) await UniTask.Yield();
            Assert.AreEqual(state, uiStateControl.CurrentState, $"{state} へ遷移していない");
        }
    }
}
