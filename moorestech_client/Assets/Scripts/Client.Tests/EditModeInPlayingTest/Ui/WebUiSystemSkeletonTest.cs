using System;
using System.Collections;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    /// <summary>
    /// Web 側で GameUiRoot の層が空か確かめる
    /// Verifies the GameUiRoot layers stay empty on Web
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class WebUiSystemSkeletonTest : UiSystemPlayModeTestBase
    {
        [UnityTest]
        public IEnumerator Web側ではGameUiRootの層が空のままでポーズもUITKに載らない()
        {
            SelectUiSystemBeforePlayMode(UiSystem.Web);
            EnterPlayModeUtil();

            // yield return new EnterPlayMode は必ず[UnityTest]関数の直下で呼び出すこと
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return TestBody().ToCoroutine();

            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask TestBody()
            {
                await LoadMainGame();
                await UniTask.DelayFrame(5);
                var uiStateControl = ClientDIContext.DIContainer.DIContainerResolver.Resolve<UIStateControl>();
                var gameUiRoot = GameUiRoot.Instance;

                // Web 起動は入力も Web で層が空
                // A Web boot arbitrates input as Web with empty layers
                Assert.AreEqual(UiSystem.Web, UiInputExclusivity.ActiveUiSystem, "起動の系が Web でない");
                AssertAllLayersEmpty(gameUiRoot, "ゲーム画面");

                // UITK のポーズは載らない
                // No UITK pause is mounted
                uiStateControl.RequestTransition(UIStateEnum.PauseMenu);
                await UniTask.DelayFrame(3);
                Assert.AreEqual(UIStateEnum.PauseMenu, uiStateControl.CurrentState, "ポーズへ遷移していない");
                AssertAllLayersEmpty(gameUiRoot, "ポーズ");
                uiStateControl.RequestTransition(UIStateEnum.GameScreen);
                await UniTask.DelayFrame(3);
            }

            #endregion
        }

        private static void AssertAllLayersEmpty(GameUiRoot gameUiRoot, string phase)
        {
            // Client.Ui 未登録で層に子が無い
            // Client.Ui is not registered, so layers are empty
            foreach (UiLayer layer in Enum.GetValues(typeof(UiLayer)))
            {
                foreach (UiFamily family in Enum.GetValues(typeof(UiFamily)))
                {
                    var count = gameUiRoot.GetLayer(layer, family).childCount;
                    Assert.AreEqual(0, count, $"{phase}: Web 側で層 {layer} の {family} に {count} 個の要素が載っている");
                }
            }
        }
    }
}
