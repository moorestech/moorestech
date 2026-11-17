using System.Collections;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Playtest.Input;
using Client.Playtest.UiElements;
using Client.Playtest.UiElements.GameUi;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.Routing;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    /// <summary>
    /// UITK 側でポーズ表示と設定往復が動くか確かめる（R-7 L3）
    /// Verifies pause display and settings round-trip on UITK (R-7 L3)
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class PauseMenuGameUiTest
    {
        [UnityTest]
        public IEnumerator UITK側でEscからポーズが出て設定へ行き来して閉じられる()
        {
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
                var resolver = ClientDIContext.DIContainer.DIContainerResolver;
                var uiStateControl = resolver.Resolve<UIStateControl>();
                var routingState = resolver.Resolve<UiRoutingState>();
                var pauseMenuState = resolver.Resolve<PauseMenuStateService>();
                using var backgroundInput = EditorBackgroundInputScope.Open();

                // Esc で UITK のポーズが出る
                // Esc shows the UITK pause
                await PressEscape();
                Assert.AreEqual(UIStateEnum.PauseMenu, uiStateControl.CurrentState, "Esc でポーズへ遷移しない");
                Assert.AreEqual(UiScreen.PauseMenu, routingState.Current.Value.Screen, "ルータの画面がポーズでない");
                Assert.IsNotNull(GameUiRoot.Instance.GetLayer(UiLayer.PauseMenu, UiFamily.Stage).Q("pause-menu"), "UiLayer.PauseMenu に pause-menu が無い");
                Assert.IsTrue(Find("pause-menu").Visible, "UITK のポーズが見えない");

                // ロケータはUITKパネルを探す
                // The locator looks up the UITK panel
                await AssertDslLocatorFindsGameUi();

                // 設定ボタンで設定ページへ
                // Settings button opens the settings page
                await ClickByTestid("pause-menu-open-settings");
                Assert.AreEqual(PauseMenuPage.Settings, pauseMenuState.CurrentPage.Value, "設定ボタンのクリックで設定のページへ行かない");
                Assert.IsTrue(Find("language-select").Visible, "設定のページに言語選択が見えない");

                // Esc 1 回目はトップへ戻り、2 回目でゲーム画面へ閉じる
                // The first Esc returns to the top and the second closes to the game screen
                await PressEscape();
                Assert.AreEqual(UIStateEnum.PauseMenu, uiStateControl.CurrentState, "設定のページの Esc でポーズが閉じた");
                Assert.AreEqual(PauseMenuPage.Top, pauseMenuState.CurrentPage.Value, "設定のページの Esc でトップへ戻らない");
                await PressEscape();
                Assert.AreEqual(UIStateEnum.GameScreen, uiStateControl.CurrentState, "トップの Esc でゲーム画面へ戻らない");
                var closedRoot = GameUiRoot.Instance.GetRootElement();
                Assert.AreEqual(UiElementLookupStatus.NotFound, GameUiElementQuery.Find(closedRoot, "pause-menu", new GameUiScreenMapper(closedRoot)).Status, "閉じた後も UITK のポーズが見えている");
                Assert.AreEqual(UiScreen.None, routingState.Current.Value.Screen, "閉じた後もルータの画面がポーズのまま");
            }

            #endregion
        }

        // DSL のロケータで UITK のポーズの設定ボタンが見つかる
        // The DSL locator finds the UITK pause settings button
        private static async UniTask AssertDslLocatorFindsGameUi()
        {
            // try/finally はセッションを必ず閉じるためで、例外は握らない
            // try/finally only guarantees the session closes; no exception is swallowed
            PlaytestUiLocatorSession.Start();
            try
            {
                var snapshot = await PlaytestUiLocatorSession.Locator.Find("pause-menu-open-settings", 5f);
                Assert.AreEqual(UiElementLookupStatus.Found, snapshot.Status, "DSL のロケータで設定ボタンが見つからない");
            }
            finally
            {
                PlaytestUiLocatorSession.Stop();
            }
        }

        private static async UniTask PressEscape()
        {
            await SemanticInput.TapKey(Key.Escape);
            await UniTask.DelayFrame(3);
        }

        private static UiElementSnapshot Find(string testid)
        {
            var root = GameUiRoot.Instance.GetRootElement();
            var snapshot = GameUiElementQuery.Find(root, testid, new GameUiScreenMapper(root));
            Debug.Log($"[PauseMenuGameUiTest] {testid} status={snapshot.Status} visible={snapshot.Visible} clickable={snapshot.Clickable} rect={snapshot.ScreenRect}");
            Assert.AreEqual(UiElementLookupStatus.Found, snapshot.Status, $"{testid} が 1 つだけ見つからない");
            return snapshot;
        }

        private static async UniTask ClickByTestid(string testid)
        {
            // ロケータのスクリーン矩形の中心へ動かし、数フレーム当たり直させてから押す
            // Move to the locator's screen-rect center, let it re-raycast for a few frames, then press
            var snapshot = Find(testid);
            Assert.IsTrue(snapshot.Clickable, $"{testid} がクリックできない");
            for (var i = 0; i < 5; i++)
            {
                SemanticInput.MouseMoveTo(snapshot.ScreenRect.center);
                await UniTask.Yield();
            }
            await SemanticInput.Click();
            await UniTask.DelayFrame(3);
        }
    }
}
