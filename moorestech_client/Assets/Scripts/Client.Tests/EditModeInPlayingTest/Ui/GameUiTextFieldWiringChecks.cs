using Client.Game.InGame.UI.UIState;
using Client.Input;
using Client.Playtest.Input;
using Client.Ui.Components.TextField;
using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Client.Tests.EditModeInPlayingTest.Ui
{
    /// <summary>
    /// 実ゲームで GameTextField のフォーカスが入力排他・IME モード・移動停止・ESC へ届くかを確かめる（R-7・R-8・R-4 の L3）
    /// Verifies in the real game that GameTextField focus reaches input exclusivity, IME mode, movement lock and ESC (L3 of R-7, R-8, R-4)
    /// </summary>
    public static class GameUiTextFieldWiringChecks
    {
        // 値は WS スレッド由来でも同じ経路（ObserveOnMainThread）で適用されるので、数フレーム待ってから読む
        // Values are applied through ObserveOnMainThread even when not from the WS thread, so read after a few frames
        private const int ApplyWaitFrames = 3;

        public static (VisualElement box, GameTextField fieldA, GameTextField fieldB) PlaceTwoTextFields(GameUiRoot gameUiRoot)
        {
            // 画面中央（Web・uGUI の要素が無い位置）に縦に 2 つ並べる
            // Stack two fields at the screen center, where no Web or uGUI element sits
            var box = new VisualElement { name = "game-ui-input-wiring-box", pickingMode = PickingMode.Ignore };
            box.style.position = Position.Absolute;
            box.style.left = Length.Percent(35);
            box.style.top = Length.Percent(40);
            box.style.width = Length.Percent(30);
            var fieldA = new GameTextField("game-ui-input-wiring-field-a", false);
            var fieldB = new GameTextField("game-ui-input-wiring-field-b", false);
            box.Add(fieldA);
            box.Add(fieldB);
            gameUiRoot.GetLayer(UiLayer.Screen).Add(box);
            return (box, fieldA, fieldB);
        }

        public static async UniTask FocusMovesBetweenFieldsAndBlurs(GameTextField fieldA, GameTextField fieldB, UIStateControl uiStateControl)
        {
            var playerInputs = Object.FindFirstObjectByType<StarterAssetsInputs>();
            Assert.IsNotNull(playerInputs, "プレイヤーの StarterAssetsInputs が無い");

            // (a) 入力欄にフォーカスで、入力排他 true・IME On・移動停止
            // (a) Focusing a text field sets exclusivity true, IME On and stops movement
            fieldA.Field.Focus();
            await UniTask.DelayFrame(ApplyWaitFrames);
            LogState("focus A");
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused, "入力欄 A にフォーカスしても IsTextInputFocused が false");
            Assert.AreEqual(IMECompositionMode.On, UnityEngine.Input.imeCompositionMode, "入力欄 A のフォーカスで IME が On でない");
            Assert.IsFalse(playerInputs.inputEnable, "入力欄 A のフォーカス中に移動が止まっていない");

            // 入力欄から入力欄へ移っても On のまま（A のフォーカスアウトで Off や Auto に落ちない）
            // Moving from one field to another keeps On (A's focus-out does not drop it to Off or Auto)
            fieldB.Field.Focus();
            await UniTask.DelayFrame(ApplyWaitFrames);
            LogState("focus A->B");
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused, "A→B のフォーカス移動で IsTextInputFocused が false");
            Assert.AreEqual(IMECompositionMode.On, UnityEngine.Input.imeCompositionMode, "A→B のフォーカス移動で IME が On でない");

            // (b) フォーカス中の ESC（注入）でポーズに入らない
            // (b) An injected ESC while focused does not open the pause menu
            await SemanticInput.TapKey(Key.Escape);
            await UniTask.DelayFrame(ApplyWaitFrames);
            LogState("escape while focused");
            Assert.AreEqual(UIStateEnum.GameScreen, uiStateControl.CurrentState, "入力欄フォーカス中の ESC でゲーム画面から遷移した");

            // (c) フォーカスを外すと false・IME Off・移動再開
            // (c) Blurring sets false, IME Off and resumes movement
            fieldB.Field.Blur();
            await UniTask.DelayFrame(ApplyWaitFrames);
            LogState("blur");
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused, "フォーカスを外しても IsTextInputFocused が true");
            Assert.AreEqual(IMECompositionMode.Off, UnityEngine.Input.imeCompositionMode, "フォーカスを外しても IME が Off でない");
            Assert.IsTrue(playerInputs.inputEnable, "フォーカスを外しても移動が止まったまま");

            // 対照: フォーカスが無ければ同じ注入の ESC でポーズに入る（(b) の不遷移が注入の空振りでない証拠）
            // Control: without focus the same injected ESC opens the pause menu, proving (b) was not a dead injection
            await SemanticInput.TapKey(Key.Escape);
            await UniTask.DelayFrame(ApplyWaitFrames);
            var stateAfterUnfocusedEscape = uiStateControl.CurrentState;
            uiStateControl.RequestTransition(UIStateEnum.GameScreen);
            await UniTask.DelayFrame(ApplyWaitFrames);
            Debug.Log($"[GameUiInputWiring] escape without focus state={stateAfterUnfocusedEscape} back={uiStateControl.CurrentState}");
            Assert.AreEqual(UIStateEnum.PauseMenu, stateAfterUnfocusedEscape, "フォーカスが無いのに注入の ESC でポーズに入らない（注入が届いていない）");
            Assert.AreEqual(UIStateEnum.GameScreen, uiStateControl.CurrentState, "ポーズからゲーム画面へ戻れない");
        }

        public static async UniTask WebTextInputIsIgnoredInGameUiSystem()
        {
            // (d) GameUi の系では Web の値（SetState）を読まず、文字入力にもポインタ占有にもならず IME も Off のまま（R2）
            // (d) In the GameUi system web values (SetState) are not read: no text-input focus, no pointer claim, and IME stays Off (R2)
            UiInputExclusivity.SetState(true, true);
            await UniTask.DelayFrame(ApplyWaitFrames);
            LogState("web values written in the GameUi system");
            var focusedWhileWebWritten = UiInputExclusivity.IsTextInputFocused;
            var pointerWhileWebWritten = UiInputExclusivity.IsPointerOverWebUi;
            var modeWhileWebWritten = UnityEngine.Input.imeCompositionMode;
            UiInputExclusivity.SetState(false, false);
            Assert.IsFalse(focusedWhileWebWritten, "GameUi の系で Web の入力欄フォーカスが文字入力になった");
            Assert.IsFalse(pointerWhileWebWritten, "GameUi の系で Web のポインタ占有が通った");
            Assert.AreEqual(IMECompositionMode.Off, modeWhileWebWritten, "GameUi の系で Web の入力欄フォーカスにより IME が Off でない");
        }

        public static async UniTask EscapeLatchHoldsOnlyItsOwnFrame()
        {
            // (f) ラッチを立てたフレームは CloseUI の押下を返さず、次のフレームでは返す（D-9 の「別フレームで false」）
            // (f) In the latched frame CloseUI returns no press, and in the next frame it does (D-9 "false on another frame")
            var closeUi = InputManager.UI.CloseUI;
            UiInputExclusivity.ConsumeEscapeThisFrame();
            closeUi.SetKeyDownForTest(true);
            var latchedFrame = Time.frameCount;
            var pressInLatchedFrame = closeUi.GetKeyDown;
            await UniTask.NextFrame();
            var nextFrame = Time.frameCount;
            var pressInNextFrame = closeUi.GetKeyDown;
            closeUi.SetKeyDownForTest(false);

            Debug.Log($"[GameUiInputWiring] escape latch latchedFrame={latchedFrame} pressInLatchedFrame={pressInLatchedFrame} nextFrame={nextFrame} pressInNextFrame={pressInNextFrame}");
            Assert.Greater(nextFrame, latchedFrame, "フレームが進んでいない");
            Assert.IsFalse(pressInLatchedFrame, "ラッチを立てたフレームで CloseUI が押下を返した");
            Assert.IsTrue(pressInNextFrame, "ラッチの次のフレームでも CloseUI が押下を返さない");
        }

        private static void LogState(string step)
        {
            Debug.Log($"[GameUiInputWiring] {step} textInputFocused={UiInputExclusivity.IsTextInputFocused} ime={UnityEngine.Input.imeCompositionMode} frame={Time.frameCount}");
        }
    }
}
