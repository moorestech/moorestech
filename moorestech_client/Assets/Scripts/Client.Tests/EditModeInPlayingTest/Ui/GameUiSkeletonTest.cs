using System;
using System.Collections;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.State.Routing;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.UIElements;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    /// <summary>
    /// 実ゲームで UITK の骨組み（層・Web 休止・空パネルの入力素通し）が結線されているか確かめる
    /// Verifies in the real game that the UITK skeleton (layers, web suspension, empty-panel input pass-through) is wired
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class GameUiSkeletonTest
    {
        [UnityTest]
        public IEnumerator 骨組みが層を持ちWeb休止と入力素通しが実ゲームで効く()
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
                var routingState = resolver.Resolve<UiRoutingState>();
                var uiStateControl = resolver.Resolve<UIStateControl>();
                var gameUiRoot = GameUiRoot.Instance;
                var cefRawImage = (RawImage)new SerializedObject(gameUiRoot).FindProperty("cefRawImage").objectReferenceValue;

                AssertAllLayersExist(gameUiRoot);

                // ゲーム画面（主パネルは Web 所有）では CEF が見え、Web は休止しない
                // On the game screen (main panel owned by Web) the CEF picture is visible and the web is not suspended
                Assert.IsTrue(cefRawImage.enabled, "既定で CEF の RawImage が無効になっている");
                Assert.IsFalse(routingState.Current.Value.WebUiSuspended, $"既定で Web が休止している: {routingState.Current.Value}");

                // ポーズ主パネルを GameUi として遷移すると Web が休止する（所有表は切替済みで、上書きは表の値と同じ）
                // Transitioning with the pause main panel as GameUi suspends the web (the table is already flipped; the override matches it)
                UiOwnershipEditorOverride.Set(UiScreen.PauseMenu, UiSurface.MainPanel, UiSurfaceOwner.GameUi);
                try
                {
                    await TransitionTo(uiStateControl, UIStateEnum.PauseMenu);
                    Assert.AreEqual(UiScreen.PauseMenu, routingState.Current.Value.Screen);
                    Assert.IsTrue(routingState.Current.Value.WebUiSuspended, $"ポーズで Web が休止していない: {routingState.Current.Value}");
                    Assert.IsFalse(cefRawImage.enabled, "休止中に CEF の RawImage が有効のまま");
                    Assert.IsFalse(UiInputExclusivity.IsPointerOverWebUi, "休止中に Web がポインタを主張している");

                    // 閉じると CEF が戻る
                    // Closing brings the CEF picture back
                    await TransitionTo(uiStateControl, UIStateEnum.GameScreen);
                    Assert.IsFalse(routingState.Current.Value.WebUiSuspended, $"ポーズを閉じても Web が休止したまま: {routingState.Current.Value}");
                    Assert.IsTrue(cefRawImage.enabled, "ポーズを閉じても CEF の RawImage が戻らない");
                }
                finally
                {
                    UiOwnershipEditorOverride.Clear();
                }

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

            // カーソルを解放して画面中央（Web・uGUI の要素が無い位置）へ置く
            // Release the cursor and place it at the screen center where no Web or uGUI element sits
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

            // 対照: 拾える要素を置けば同じ判定が true になる（上の false が測定の空振りでない証拠）
            // Control: a pickable element flips the same checks to true, proving the false above is not a dead measurement
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
            Debug.Log($"[GameUiSkeletonTest] {label} center={center} panelPos={panelPosition} picked={(picked == null ? "null" : picked.name + "/" + picked.GetType().Name)} overGameObject={isOverGameObject} overAnyUi={isOverAnyUi} lock={UnityEngine.Cursor.lockState} webPointer={UiInputExclusivity.IsPointerOverWebUi}");
            return (picked, isOverGameObject, isOverAnyUi);
        }
    }
}
