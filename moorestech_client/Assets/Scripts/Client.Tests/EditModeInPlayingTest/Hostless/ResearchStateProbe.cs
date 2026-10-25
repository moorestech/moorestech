using Client.Game.InGame.UI.Research;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.Hostless
{
    // 研究画面へ入ると、Webホスト無しでもストアがサーバーから状態を取り直すことを確かめる
    // Verifies the store refetches node states from the server on entering the research screen, without the web host
    internal static class ResearchStateProbe
    {
        public static async UniTask AssertAvailableAsync(IObjectResolver resolver)
        {
            var store = resolver.Resolve<ResearchNodeStateStore>();
            var uiStateControl = resolver.Resolve<UIStateControl>();
            var before = store.NodeStates;

            uiStateControl.RequestTransition(UIStateEnum.ResearchTree);
            for (var i = 0; i < 600 && ReferenceEquals(store.NodeStates, before); i++) await UniTask.Yield();
            Assert.IsFalse(ReferenceEquals(store.NodeStates, before), "研究画面へ入っても研究ノード状態を取り直さない");

            // 後続のプローブのためにゲーム画面へ戻す
            // Return to the game screen for the following probes
            uiStateControl.RequestTransition(UIStateEnum.GameScreen);
            for (var i = 0; i < 120 && uiStateControl.CurrentState != UIStateEnum.GameScreen; i++) await UniTask.Yield();
            Assert.AreEqual(UIStateEnum.GameScreen, uiStateControl.CurrentState);
        }
    }
}
