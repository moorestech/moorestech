using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.CancelInput;
using Client.Game.InGame.UI.BuildMenu;
using Client.Input;
using Client.Tests.Common;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;

namespace Client.Tests.UIState.BuildMenu
{
    /// <summary>
    /// GameUi の入力中・ESC 消費のフレームはビルドメニューの状態が閉じない（L1・UIState 側）
    /// The build-menu state does not close while GameUi text is focused or ESC was consumed (L1, state side)
    /// </summary>
    public class BuildMenuStateTextInputGuardTest : UIStateTestFixtureBase
    {
        private BuildMenuState _state;

        public override void Setup()
        {
            base.Setup();
            // 系の既定は Web なので GameUi にし、ESC の押下はテストから差し込む（前例 UiInputExclusivityEscapeLatchTest）
            // The default system is Web, so switch to GameUi; ESC presses are injected (precedent UiInputExclusivityEscapeLatchTest)
            UiInputExclusivity.SetActiveUiSystem(UiSystem.GameUi);
            TestReflection.ResetInputManagerCache();
            _state = new BuildMenuState(new BuildMenuSelection(), CreateCameraPolicy(new FakePlayerCameraInteractionApplier()), new RightShortPressInputService(new RightShortPressInput()));
            _state.OnEnter(new UITransitContext(UIStateEnum.BuildMenu));
        }

        public override void TearDown()
        {
            InputManager.UI.CloseUI.SetKeyDownForTest(false);
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.ClearEscapeLatchForTest();
            UiInputExclusivity.SetActiveUiSystem(UiSystem.Web);
            TestReflection.ResetInputManagerCache();
            base.TearDown();
        }

        [Test]
        public void Bは入力中だけ閉じない()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(true);
            Press(KeyboardDevice.bKey);
            Assert.IsNull(_state.GetNextUpdate(), "入力中の B では閉じない");

            UiInputExclusivity.SetGameUiTextInputFocused(false);
            Release(KeyboardDevice.bKey);
            Press(KeyboardDevice.bKey);
            Assert.AreEqual(UIStateEnum.GameScreen, _state.GetNextUpdate()?.NextStateEnum, "入力中でない B では閉じる");
        }

        [Test]
        public void ESCは入力欄が消費したフレームだけ閉じない()
        {
            InputManager.UI.CloseUI.SetKeyDownForTest(true);
            Assert.AreEqual(UIStateEnum.GameScreen, _state.GetNextUpdate()?.NextStateEnum, "ラッチの無い ESC では閉じる");

            UiInputExclusivity.ConsumeEscapeThisFrame();
            Assert.IsNull(_state.GetNextUpdate(), "入力欄が消費した ESC では閉じない");
        }
    }
}
