using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.UI.UIState.State.CameraPolicy;
using Client.Tests.ViewMode;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.UIState.CameraPolicy
{
    public class UiStateCameraPolicyServiceTest : InputTestFixture
    {
        private Mouse _mouse;
        private Keyboard _keyboard;
        private FakePlayerCameraInteractionApplier _applier;
        private PlayerViewModeController _viewModeController;
        private UiStateCameraPolicyService _service;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _applier = new FakePlayerCameraInteractionApplier();
            _viewModeController = new PlayerViewModeController(new FakePlayerViewApplier());
            _service = new UiStateCameraPolicyService(_applier, _viewModeController);
        }

        public override void TearDown()
        {
            // 静的状態のAimPointProviderをテスト間で持ち越さない
            // Reset the static AimPointProvider so no state leaks across tests
            AimPointProvider.SetViewMode(PlayerViewMode.ThirdPerson);
            AimPointProvider.SetThirdPersonAimSource(ThirdPersonAimSource.ScreenCenter);
            base.TearDown();
        }

        [Test]
        public void GameplayZoneAlwaysCameraLookAndIgnoresViewToggle()
        {
            _service.EnterGameplay();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, _applier.Calls);

            // 視点切替でも再適用なし
            // View toggles never re-push the policy
            _applier.Calls.Clear();
            _viewModeController.ToggleViewMode();
            CollectionAssert.IsEmpty(_applier.Calls);
        }

        [Test]
        public void MenuZoneFreesPointerAndIgnoresViewToggle()
        {
            _service.EnterMenu();
            CollectionAssert.AreEqual(new[] { "Mode:PointerFree" }, _applier.Calls);

            _applier.Calls.Clear();
            _viewModeController.ToggleViewMode();
            CollectionAssert.IsEmpty(_applier.Calls);
        }

        [Test]
        public void BuildZoneTpsRotatesOnlyDuringRightDrag()
        {
            _service.EnterBuildMode();
            CollectionAssert.AreEqual(new[] { "Mode:PointerFree" }, _applier.Calls);

            _applier.Calls.Clear();
            Press(_mouse.rightButton);
            _service.UpdateRotationInput();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, _applier.Calls);

            _applier.Calls.Clear();
            Release(_mouse.rightButton);
            _service.UpdateRotationInput();
            CollectionAssert.AreEqual(new[] { "Mode:PointerFree" }, _applier.Calls);
        }

        [Test]
        public void BuildZoneFpsLocksCursorAndIgnoresRightClick()
        {
            _viewModeController.ToggleViewMode();
            _service.EnterBuildMode();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, _applier.Calls);

            // FPSは右クリックで状態変化なし
            // Right clicks cause no state change in FPS
            _applier.Calls.Clear();
            Press(_mouse.rightButton);
            _service.UpdateRotationInput();
            Release(_mouse.rightButton);
            _service.UpdateRotationInput();
            CollectionAssert.IsEmpty(_applier.Calls);
        }

        [Test]
        public void BuildZoneFollowsViewToggleWhileStaying()
        {
            _service.EnterBuildMode();

            _applier.Calls.Clear();
            _viewModeController.ToggleViewMode();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, _applier.Calls);

            _applier.Calls.Clear();
            _viewModeController.ToggleViewMode();
            CollectionAssert.AreEqual(new[] { "Mode:PointerFree" }, _applier.Calls);
        }

        [Test]
        public void ExitToNeutralFreesPointerAndKeepsItAfterFocusRestore()
        {
            _service.EnterGameplay();

            _applier.Calls.Clear();
            _service.ExitToNeutral();
            CollectionAssert.AreEqual(new[] { "Mode:PointerFree" }, _applier.Calls);

            // ゾーン所有が外れ復帰も自由カーソル
            // Zone ownership is released, so focus restore stays pointer-free
            _applier.Calls.Clear();
            _service.RestoreAfterApplicationFocus();
            CollectionAssert.AreEqual(new[] { "Mode:PointerFree" }, _applier.Calls);
        }

        [Test]
        public void BuildZoneStopsFollowingViewToggleAfterExit()
        {
            _service.EnterBuildMode();
            _service.ExitToNeutral();

            // 退出後のV切替は旧ゾーンを再適用しない
            // A V toggle after exiting never re-applies the old zone
            _applier.Calls.Clear();
            _viewModeController.ToggleViewMode();
            CollectionAssert.IsEmpty(_applier.Calls);
        }

        [Test]
        public void GameplayZoneTpsFreesPointerWhileLeftAltHeld()
        {
            _service.EnterGameplay();

            _applier.Calls.Clear();
            Press(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            CollectionAssert.AreEqual(new[] { "Warp", "Mode:PointerFree" }, _applier.Calls);

            _applier.Calls.Clear();
            Release(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, _applier.Calls);
        }

        [Test]
        public void GameplayZoneAltHoldSwitchesAimSourceToCursor()
        {
            _service.EnterGameplay();
            Assert.AreEqual(AimPointMode.ScreenCenter, AimPointProvider.GetCurrentMode());

            Press(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            Assert.AreEqual(AimPointMode.Mouse, AimPointProvider.GetCurrentMode());

            Release(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            Assert.AreEqual(AimPointMode.ScreenCenter, AimPointProvider.GetCurrentMode());
        }

        [Test]
        public void GameplayZoneFpsIgnoresLeftAlt()
        {
            _viewModeController.ToggleViewMode();
            _service.EnterGameplay();

            _applier.Calls.Clear();
            Press(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            Release(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            CollectionAssert.IsEmpty(_applier.Calls);
        }

        [Test]
        public void GameplayViewToggleDiscardsAltHold()
        {
            _service.EnterGameplay();
            Press(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();

            // 視点切替でホールドを破棄し、押し直すまで自由カーソルにならない
            // A view toggle discards the hold; the cursor stays locked until Alt is pressed again
            _applier.Calls.Clear();
            _viewModeController.ToggleViewMode();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, _applier.Calls);
            Assert.AreEqual(AimPointMode.ScreenCenter, AimPointProvider.GetCurrentMode());
        }

        [Test]
        public void ExitToNeutralClearsAltHold()
        {
            _service.EnterGameplay();
            Press(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();

            _service.ExitToNeutral();

            // 退出でホールドが消え、Gameplayへ戻ると自由カーソルではなく回転状態から始まる
            // Exiting clears the hold, so re-entering gameplay starts from rotation, not a free cursor
            _applier.Calls.Clear();
            _service.EnterGameplay();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, _applier.Calls);
        }

        [Test]
        public void BuildZoneKeepsCursorAimSourceDuringRightDrag()
        {
            _service.EnterBuildMode();
            Assert.AreEqual(AimPointMode.Mouse, AimPointProvider.GetCurrentMode());

            // 右ドラッグで回転しても照準はカーソルのまま（プレビューが画面中央へ跳ねない）
            // The aim stays on the cursor even while right-drag rotates, so the preview never jumps to the center
            Press(_mouse.rightButton);
            _service.UpdateRotationInput();
            Assert.AreEqual(AimPointMode.Mouse, AimPointProvider.GetCurrentMode());
        }

        [Test]
        public void MenuZoneCentersAimSource()
        {
            _service.EnterBuildMode();
            _service.EnterMenu();
            Assert.AreEqual(AimPointMode.ScreenCenter, AimPointProvider.GetCurrentMode());
        }
    }
}
