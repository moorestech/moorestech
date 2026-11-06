using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    /// <summary>
    /// 実ゲームで GameUi の入力（ナビゲーションの遮断など）が効くか確かめる
    /// Verifies in the real game that GameUi input handling (navigation blocking and so on) works
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class GameUiInputTest : UiSystemPlayModeTestBase
    {
        [UnityTest]
        public IEnumerator UI資産の差し替え前後でWASDと矢印とSpaceとEnterがナビゲーションイベントを出すかを数える()
        {
            // 全体スイッチを UITK にして起動する（plan §3.3 R4）
            // Boot with the global switch on UITK (plan §3.3 R4)
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

                var module = (InputSystemUIInputModule)EventSystem.current.currentInputModule;
                var sceneActionsAsset = module.actionsAsset;
                AssertSceneUsesUiOnlyAsset(module);
                var focusedField = await FocusProbeTextField(GameUiRoot.Instance);
                var counter = NavigationEventCounter.Attach(focusedField.panel.visualTree, focusedField);

                // 差し替え前: 実行中だけ既定の資産（Navigate・Submit・Cancel 入り）へ戻して測る。シーンは変えない
                // Before: measure with the default asset (with Navigate/Submit/Cancel) only at runtime; the scene is untouched
                module.AssignDefaultActions();
                var before = await counter.MeasureEachKey("before");
                module.actionsAsset = sceneActionsAsset;
                Assert.IsNull(module.move, "シーンの資産へ戻しても move が残っている");

                // 差し替え後: シーンに保存された UI 専用資産で測る
                // After: measure with the UI-only asset saved in the scene
                var after = await counter.MeasureEachKey("after");
                counter.Detach();
                focusedField.RemoveFromHierarchy();

                AssertBeforeSwap(before);
                AssertAfterSwap(after);
            }

            #endregion
        }

        [UnityTest]
        public IEnumerator 実ゲームでGameUiの入力欄がフォーカスとIMEとESCとポインタとDSLに結線されている()
        {
            // 全体スイッチを UITK にして起動する（plan §3.3 R4）
            // Boot with the global switch on UITK (plan §3.3 R4)
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
                var uiStateControl = ClientDIContext.DIContainer.DIContainerResolver.Resolve<UIStateControl>();
                GameUiPointerWiringChecks.AssertGameUiPanelSettingsIsNotShared();

                // 背面の Editor でも注入が届く設定にし、カーソルを解放してから Screen 層に入力欄を 2 つ置く
                // Let injection reach a background Editor, release the cursor, then place two text fields on the Screen layer
                using var backgroundInput = EditorBackgroundInputScope.Open();
                InputManager.MouseCursorVisible(true);
                var (box, fieldA, fieldB) = GameUiTextFieldWiringChecks.PlaceTwoTextFields(GameUiRoot.Instance);
                await UniTask.DelayFrame(3);

                var fieldCenter = await GameUiPointerWiringChecks.LocatorFindsFieldAndPointerIsOverIt(fieldA);
                await GameUiTextFieldWiringChecks.FocusMovesBetweenFieldsAndBlurs(fieldA, fieldB, uiStateControl);
                await GameUiTextFieldWiringChecks.WebTextInputIsIgnoredInGameUiSystem();
                await GameUiTextFieldWiringChecks.EscapeLatchHoldsOnlyItsOwnFrame();
                await GameUiPointerWiringChecks.PointerIsNotOverAfterRemoval(box, fieldCenter);
            }

            #endregion
        }

        private static void AssertSceneUsesUiOnlyAsset(InputSystemUIInputModule module)
        {
            // シーンは UI 専用資産を指し、選択移動・決定・取消の参照を持たない
            // The scene points at the UI-only asset and holds no move/submit/cancel references
            Assert.AreEqual("MoorestechUiInputActions", module.actionsAsset.name, "MainGame の UI 入力モジュールが UI 専用資産を指していない");
            Assert.IsNull(module.move, "シーンの move が null でない");
            Assert.IsNull(module.submit, "シーンの submit が null でない");
            Assert.IsNull(module.cancel, "シーンの cancel が null でない");
            Assert.IsNotNull(module.point, "シーンの point が外れている");
            Assert.IsNotNull(module.leftClick, "シーンの leftClick が外れている");
        }

        private static async UniTask<TextField> FocusProbeTextField(GameUiRoot gameUiRoot)
        {
            // 規則は入力欄以外のフォーカスを外すので、フォーカスを保てる入力欄を置く
            // The rules blur any non text-field focus, so place a text field that can keep it
            var field = new TextField { name = "game-ui-input-test-probe", focusable = true, tabIndex = 0 };
            gameUiRoot.GetLayer(UiLayer.Screen).Add(field);
            await UniTask.DelayFrame(2);
            field.Focus();
            await UniTask.DelayFrame(3);

            var focused = field.panel.focusController.focusedElement as VisualElement;
            Debug.Log($"[S4] focused={focused?.GetType().Name} '{focused?.name}' selected={EventSystem.current.currentSelectedGameObject?.name}");
            Assert.IsTrue(focused != null && (focused == field || field.Contains(focused)), $"入力欄にフォーカスが無い: {focused}");
            Assert.IsNotNull(EventSystem.current.currentSelectedGameObject, "EventSystem がパネルを選択していない（ナビゲーションが届かない）");
            return field;
        }

        private static void AssertBeforeSwap(Dictionary<Key, KeyNavigationCount> before)
        {
            // 既定の資産では WASD・矢印が移動、Enter が決定を出す（測定が効いている対照）
            // With the default asset WASD/arrows emit move and Enter emits submit (control proving the measurement works)
            foreach (var key in new[] { Key.W, Key.A, Key.S, Key.D, Key.UpArrow, Key.LeftArrow, Key.DownArrow, Key.RightArrow })
            {
                Assert.GreaterOrEqual(before[key].MoveBeforeRules, 1, $"差し替え前に {key} で NavigationMove が来ない");
            }
            Assert.GreaterOrEqual(before[Key.Enter].SubmitBeforeRules, 1, "差し替え前に Enter で NavigationSubmit が来ない");

            // Space は Submit の usage を持たないので既定の資産でも何も出さない
            // Space carries no Submit usage, so even the default asset emits nothing for it
            Assert.AreEqual(0, before[Key.Space].MoveBeforeRules, "差し替え前に Space で NavigationMove が来た");
            Assert.AreEqual(0, before[Key.Space].SubmitBeforeRules, "差し替え前に Space で NavigationSubmit が来た");

            // 規則が打ち消すので、来たものも要素自身へは届かない
            // The rules cancel them, so nothing that arrived reaches the element itself
            AssertNothingPastRules(before, "差し替え前");
        }

        private static void AssertAfterSwap(Dictionary<Key, KeyNavigationCount> after)
        {
            foreach (var pair in after)
            {
                Assert.AreEqual(0, pair.Value.MoveBeforeRules, $"差し替え後に {pair.Key} で NavigationMove が来た");
                Assert.AreEqual(0, pair.Value.SubmitBeforeRules, $"差し替え後に {pair.Key} で NavigationSubmit が来た");
            }
            AssertNothingPastRules(after, "差し替え後");
        }

        private static void AssertNothingPastRules(Dictionary<Key, KeyNavigationCount> counts, string phase)
        {
            foreach (var pair in counts)
            {
                Assert.AreEqual(0, pair.Value.MoveAfterRules, $"{phase}: {pair.Key} の NavigationMove が規則を越えて要素へ届いた");
                Assert.AreEqual(0, pair.Value.SubmitAfterRules, $"{phase}: {pair.Key} の NavigationSubmit が規則を越えて要素へ届いた");
            }
        }
    }
}
