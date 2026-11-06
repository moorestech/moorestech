using System;
using System.Collections;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    /// <summary>
    /// 全体スイッチの Web 側で、Client.Ui が何も載らず GameUiRoot の層が空のままかを実ゲームで確かめる（plan §2.2 完了条件）
    /// Verifies in the real game that on the Web side of the global switch nothing from Client.Ui is mounted and the GameUiRoot layers stay empty (plan §2.2)
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

                // Web 側の起動は入力の裁きも Web で、ゲーム画面で層が空
                // A Web-side boot arbitrates input as Web, and the layers are empty on the game screen
                Assert.AreEqual(UiSystem.Web, UiInputExclusivity.ActiveUiSystem, "起動の系が Web でない");
                AssertAllLayersEmpty(gameUiRoot, "ゲーム画面");

                // ポーズにしても UITK のポーズは載らない（Web のポーズが出る）
                // Pausing mounts no UITK pause (the Web pause shows instead)
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
            // Web 側では Client.Ui を登録しないので、どの層にも子が無い（plan §3.3 R1）
            // The Web side registers nothing from Client.Ui, so no layer has children (plan §3.3 R1)
            foreach (UiLayer layer in Enum.GetValues(typeof(UiLayer)))
            {
                var count = gameUiRoot.GetLayer(layer).childCount;
                Assert.AreEqual(0, count, $"{phase}: Web 側で層 {layer} に {count} 個の要素が載っている");
            }
        }
    }
}
