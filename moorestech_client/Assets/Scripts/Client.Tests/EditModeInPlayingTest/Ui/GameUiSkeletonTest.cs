using System;
using System.Collections;
using Client.Game.InGame.BugReport.WebUiHealth;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.State.Routing;
using Client.WebUiHost.Cef;
using Client.WebUiHost.Health;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    /// <summary>
    /// UITK 起動の骨組みの結線を確かめる
    /// Verifies the skeleton wiring on a UITK boot
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class GameUiSkeletonTest : UiSystemPlayModeTestBase
    {
        [UnityTest]
        public IEnumerator UITK側の骨組みが層を持ちWebホスト無しで入力素通しが実ゲームで効く()
        {
            SelectUiSystemBeforePlayMode(UiSystem.GameUi);
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
                var routingState = resolver.Resolve<UiRoutingState>();
                var uiStateControl = resolver.Resolve<UIStateControl>();
                var gameUiRoot = GameUiRoot.Instance;

                AssertAllLayersExist(gameUiRoot);

                // UITK 起動に Web ホストと CEF は無い
                // A UITK boot has no web host or CEF
                Assert.AreEqual(UiSystem.GameUi, UiInputExclusivity.ActiveUiSystem, "起動の系が GameUi でない");
                Assert.IsNull(Client.WebUiHost.Boot.WebUiHost.Hub, "UITK 側で Web ホスト（WS の受け口）が起動している");
                Assert.IsTrue(Client.WebUiHost.Boot.WebUiHost.HostSkippedThisBoot, "Web ホストを飛ばした起動の状態が立っていない");
                Assert.AreEqual(WebUiHostStartupState.SkippedByEditorSwitch, WebUiHealthRecorder.HostStartup, "診断記録にスイッチで飛ばしたことが残っていない");
                Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<WebUiCefNavigator>(FindObjectsSortMode.None).Length, "CEF のブラウザ（RawImage と同じ GameObject）が有効のまま");

                // ルータは UIState だけで画面を決める
                // The router decides the screen from UIState alone
                await TransitionTo(uiStateControl, UIStateEnum.PauseMenu);
                Assert.AreEqual(UiScreen.PauseMenu, routingState.Current.Value.Screen);
                await TransitionTo(uiStateControl, UIStateEnum.GameScreen);
                Assert.AreEqual(UiScreen.None, routingState.Current.Value.Screen);

                await AssertEmptyPanelDoesNotTakePointer(gameUiRoot);
            }

            #endregion
        }

        private static void AssertAllLayersExist(GameUiRoot gameUiRoot)
        {
            // 全層のコンテナが組まれている
            // Every layer has its container built
            foreach (UiLayer layer in Enum.GetValues(typeof(UiLayer)))
            {
                var container = gameUiRoot.GetLayer(layer);
                Assert.IsNotNull(container, $"層 {layer} のコンテナが無い");
                Assert.IsNotNull(container.panel, $"層 {layer} のコンテナがパネルに載っていない");
            }
        }

        private static async UniTask TransitionTo(UIStateControl uiStateControl, UIStateEnum state)
        {
            // 遷移要求は次の Update で消費される
            // The transition request is consumed in the next Update
            uiStateControl.RequestTransition(state);
            await UniTask.DelayFrame(3);
            Assert.AreEqual(state, uiStateControl.CurrentState, $"{state} へ遷移していない");
        }

        private static async UniTask AssertEmptyPanelDoesNotTakePointer(GameUiRoot gameUiRoot)
        {
            // 背面の Editor・CI の batchmode でも注入マウスが届く設定にする
            // Let injected mouse input reach a background Editor or CI batchmode
            using var backgroundInput = EditorBackgroundInputScope.Open();

            // カーソルを画面中央へ置く
            // Place the cursor at the screen center
            InputManager.MouseCursorVisible(true);
            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            for (var i = 0; i < 5; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = center });
                InputSystem.Update();
                await UniTask.Yield();
            }

            var empty = await MeasurePointerAtCenter(gameUiRoot, center, "empty");
            Assert.IsNull(empty.picked, $"空の UITK パネルが {empty.picked?.name} を Pick した");
            Assert.IsFalse(empty.isOverGameObject, "空の UITK パネル越しに IsPointerOverGameObject が true");
            Assert.IsFalse(empty.isOverAnyUi, "空の UITK パネル越しに IsPointerOverAnyUi が true");

            // 対照: 拾える要素を置けば同じ判定が true になる（上の false が測定の空振りでない証拠。片側読みの GameUi 側の実配線）
            // Control: a pickable element flips the same checks to true (proof the false above is real, and the live wiring of the one-sided GameUi read)
            var blocker = new VisualElement { name = "skeleton-test-blocker", pickingMode = PickingMode.Position };
            blocker.style.position = Position.Absolute;
            blocker.style.left = 0;
            blocker.style.top = 0;
            blocker.style.right = 0;
            blocker.style.bottom = 0;
            gameUiRoot.GetLayer(UiLayer.Hud).Add(blocker);
            var covered = await MeasurePointerAtCenter(gameUiRoot, center, "covered");
            blocker.RemoveFromHierarchy();
            Assert.AreSame(blocker, covered.picked, "対照の要素が Pick されない");
            Assert.IsTrue(covered.isOverGameObject, "対照の要素の上で IsPointerOverGameObject が false（測定が効いていない）");
            Assert.IsTrue(covered.isOverAnyUi, "対照の要素の上で IsPointerOverAnyUi が false（GameUi の系の Pick が読まれていない）");
        }

        private static async UniTask<(VisualElement picked, bool isOverGameObject, bool isOverAnyUi)> MeasurePointerAtCenter(GameUiRoot gameUiRoot, Vector2 center, string label)
        {
            // EventSystem が新しい木で当たり直すまで数フレーム待つ
            // Wait a few frames so the EventSystem re-raycasts against the current tree
            await UniTask.DelayFrame(3);
            var panel = gameUiRoot.GetLayer(UiLayer.Backdrop).panel;
            var panelPosition = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(center.x, Screen.height - center.y));
            var picked = panel.Pick(panelPosition);
            var isOverGameObject = EventSystem.current.IsPointerOverGameObject();
            var isOverAnyUi = UiPointerHitTest.IsPointerOverAnyUi();
            Debug.Log($"[GameUiSkeletonTest] {label} center={center} panelPos={panelPosition} picked={(picked == null ? "null" : picked.name + "/" + picked.GetType().Name)} overGameObject={isOverGameObject} overAnyUi={isOverAnyUi} lock={UnityEngine.Cursor.lockState} system={UiInputExclusivity.ActiveUiSystem}");
            return (picked, isOverGameObject, isOverAnyUi);
        }
    }
}
