using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Starter.Initialization.Boot;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    /// <summary>
    /// 系を選んで PlayMode に入る L3 の土台
    /// Base for L3 tests that pick the system before play mode
    /// </summary>
    public abstract class UiSystemPlayModeTestBase
    {
        private const int TransitionWaitFrameLimit = 120;

        // EnterPlayMode より前に呼ぶ。値は SessionState にありドメインリロードを越えて次の起動に効く
        // Call before EnterPlayMode; the value lives in SessionState and reaches the next boot across the domain reload
        protected static void SelectUiSystemBeforePlayMode(UiSystem system)
        {
            UiSystemEditorSwitch.Set(system);
        }

        // 遷移要求は次の Update で消費されるので、到達するまで 1 フレームずつ待つ
        // The request is consumed on a later Update, so wait frame by frame until the state is reached
        protected static async UniTask TransitionTo(UIStateControl uiStateControl, UIStateEnum state)
        {
            uiStateControl.RequestTransition(state);
            for (var i = 0; i < TransitionWaitFrameLimit && uiStateControl.CurrentState != state; i++) await UniTask.Yield();
            Assert.AreEqual(state, uiStateControl.CurrentState, $"{state} へ遷移していない");
        }

        [TearDown]
        public void RestoreDefaultUiSystem()
        {
            UiSystemEditorSwitch.Set(UiSystem.Web);
        }
    }
}
