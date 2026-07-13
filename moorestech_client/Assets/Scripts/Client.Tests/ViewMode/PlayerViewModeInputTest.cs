using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.UI.UIState;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Client.Tests.ViewMode
{
    /// <summary>
    ///     仮想デバイス注入で ManualUpdate の入力結線（Vトグル・右ドラッグ回転）を検証するテスト
    ///     Tests driving ManualUpdate through virtual devices to cover the V toggle and right-drag rotation wiring
    /// </summary>
    public class PlayerViewModeInputTest : InputTestFixture
    {
        private FakePlayerViewApplier _applier;
        private PlayerViewModeController _controller;
        private Keyboard _keyboard;
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            _applier = new FakePlayerViewApplier();
            _controller = new PlayerViewModeController(_applier);
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        public override void TearDown()
        {
            AimPointProvider.SetMode(PlayerViewMode.ThirdPerson);
            base.TearDown();
        }

        [Test]
        public void VKeyTogglesViewModeOnGameScreen()
        {
            _controller.SetUIState(UIStateEnum.GameScreen);

            // ゲーム画面でVを押すとFPSへ切り替わる（建設モード外でもトグルが効く結線の検証）
            // Pressing V on the game screen switches to FPS (covers the toggle wiring outside build states)
            Press(_keyboard.vKey);
            _controller.ManualUpdate();
            Assert.AreEqual(PlayerViewMode.FirstPerson, _controller.CurrentMode);
            Assert.AreEqual(PlayerViewMode.FirstPerson, AimPointProvider.CurrentMode);
            Assert.AreEqual(true, _applier.LastFirstPersonCamera);

            Release(_keyboard.vKey);
            Press(_keyboard.vKey);
            _controller.ManualUpdate();
            Assert.AreEqual(PlayerViewMode.ThirdPerson, _controller.CurrentMode);
            Assert.AreEqual(false, _applier.LastFirstPersonCamera);
        }

        [Test]
        public void RightDragRotatesCameraInThirdPersonPlaceBlock()
        {
            _controller.SetUIState(UIStateEnum.PlaceBlock);
            _applier.Calls.Clear();

            // 三人称の設置モードは右ドラッグ中だけカーソルを隠して視点を回す
            // The third-person placement mode hides the cursor and rotates the view only while right-dragging
            Press(_mouse.rightButton);
            _controller.ManualUpdate();
            Assert.AreEqual(false, _applier.LastCursorVisible);
            Assert.AreEqual(true, _applier.LastCameraRotatable);

            Release(_mouse.rightButton);
            _controller.ManualUpdate();
            Assert.AreEqual(true, _applier.LastCursorVisible);
            Assert.AreEqual(false, _applier.LastCameraRotatable);
        }

        [Test]
        public void RightDragDoesNotTouchCursorOnGameScreen()
        {
            _controller.SetUIState(UIStateEnum.GameScreen);
            _applier.Calls.Clear();

            // ゲーム画面はカーソルロック済みで常時回転可のため、右ドラッグで何も適用しない
            // The game screen already locks the cursor and always rotates, so right-drag must apply nothing
            Press(_mouse.rightButton);
            _controller.ManualUpdate();
            Release(_mouse.rightButton);
            _controller.ManualUpdate();
            Assert.IsEmpty(_applier.Calls);
        }

        [Test]
        public void RightDragDoesNotTouchCursorInFirstPerson()
        {
            _controller.SetUIState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();
            _applier.Calls.Clear();

            // FPSはカーソルロック・常時回転のため、右ドラッグでカーソルを解放してはいけない
            // FPS keeps the cursor locked and always rotates, so right-drag must not free the cursor
            Press(_mouse.rightButton);
            _controller.ManualUpdate();
            Release(_mouse.rightButton);
            _controller.ManualUpdate();
            Assert.IsEmpty(_applier.Calls);
        }
    }
}
