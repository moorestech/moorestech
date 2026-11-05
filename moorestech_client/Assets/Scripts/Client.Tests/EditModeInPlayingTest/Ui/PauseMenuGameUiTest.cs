using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.Input;
using Client.Playtest.Input;
using Client.Playtest.UiElements;
using Client.Playtest.UiElements.GameUi;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.State.Routing;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Topics;
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
    /// 実ゲームで Esc から UITK のポーズが載り、Web が休止し、閉じると戻るかを確かめる（R-7 の L3）
    /// Verifies in the real game that Esc brings up the UITK pause, suspends the web, and closing restores it (L3 of R-7)
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class PauseMenuGameUiTest
    {
        [UnityTest]
        public IEnumerator EscでUITKのポーズが出てWebが休止し設定へ行き来して閉じるとWebが戻る()
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

                // 実ルーティングで作った ui_state の配信 JSON が、ポーズで休止 true・戻って false になり、変化のたびに再配信される（R1）
                // The ui_state JSON built from the real routing reads suspended=true in pause and false after, and republishes on each change (R1)
                var hub = new WebSocketHub();
                using var uiStateTopic = new UiStateTopic(hub, uiStateControl, resolver.Resolve<UIStateDictionary>(), resolver.Resolve<IReadOnlyList<INestedPauseScreenState>>(), routingState);
                StringAssert.Contains("\"webUiSuspended\":false", await uiStateTopic.GetSnapshotJsonAsync(), "ゲーム画面で休止が false でない");

                // ゲーム画面で Esc を押すと UITK のポーズが出て Web が休止する
                // Pressing Esc on the game screen shows the UITK pause and suspends the web
                await PressEscape();
                Assert.AreEqual(UIStateEnum.PauseMenu, uiStateControl.CurrentState, "Esc でポーズへ遷移しない");
                Assert.IsNotNull(GameUiRoot.Instance.GetLayer(UiLayer.PauseMenu).Q("pause-menu"), "UiLayer.PauseMenu に pause-menu が無い");
                Assert.IsTrue(Find("pause-menu").Visible, "UITK のポーズが見えない");
                Assert.IsTrue(routingState.Current.Value.WebUiSuspended, $"ポーズで Web が休止していない: {routingState.Current.Value}");
                StringAssert.Contains("\"webUiSuspended\":true", await uiStateTopic.GetSnapshotJsonAsync(), "ポーズの ui_state に休止 true が載らない");
                var revisionInPause = hub.GetTopicRevision(UiStateTopic.TopicName);
                Assert.Greater(revisionInPause, 0, "ポーズへの遷移で ui_state が再配信されない");
                Assert.IsFalse(UiInputExclusivity.IsPointerOverWebUi, "休止中に Web がポインタを主張している");

                // 休止中に Web 側の値が届いても入力欄フォーカスにならない
                // Web values arriving during suspension never become text-input focus
                UiInputExclusivity.SetState(true, true);
                Assert.IsFalse(UiInputExclusivity.IsTextInputFocused, "休止中の Web の入力欄フォーカスが通った");
                Assert.IsFalse(UiInputExclusivity.IsPointerOverWebUi, "休止中の Web のポインタ占有が通った");

                // DSL の UITK ロケータで設定ボタンを引いてクリックすると設定のページになる
                // Locating the settings button with the DSL UITK locator and clicking it opens the settings page
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
                Assert.IsFalse(Find("pause-menu").Visible, "閉じた後も UITK のポーズが見えている");
                Assert.IsFalse(routingState.Current.Value.WebUiSuspended, $"閉じても Web が休止したまま: {routingState.Current.Value}");
                StringAssert.Contains("\"webUiSuspended\":false", await uiStateTopic.GetSnapshotJsonAsync(), "閉じた後の ui_state に休止 false が載らない");
                Assert.Greater(hub.GetTopicRevision(UiStateTopic.TopicName), revisionInPause, "ポーズを閉じても ui_state が再配信されない");

                // 休止前に入れた true は閉じた直後に残っていない
                // The true written before closing does not survive right after closing
                Assert.IsFalse(UiInputExclusivity.IsPointerOverWebUi, "閉じた直後に Web のポインタ占有が残っている");
                Assert.IsFalse(UiInputExclusivity.IsTextInputFocused, "閉じた直後に Web の入力欄フォーカスが残っている");
            }

            #endregion
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
