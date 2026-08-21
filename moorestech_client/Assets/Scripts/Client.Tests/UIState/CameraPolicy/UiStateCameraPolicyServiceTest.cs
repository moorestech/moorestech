using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.UI.UIState.State.CameraPolicy;
using Client.Tests.UIState.Fakes;
using Client.Tests.ViewMode;
using NUnit.Framework;

namespace Client.Tests.UIState.CameraPolicy
{
    public class UiStateCameraPolicyServiceTest : UIStateTestFixtureBase
    {
        private FakePlayerCameraInteractionApplier _applier;
        private PlayerViewModeController _viewModeController;
        private UiStateCameraPolicyService _service;

        public override void Setup()
        {
            base.Setup();
            _applier = new FakePlayerCameraInteractionApplier();
            _viewModeController = new PlayerViewModeController(new FakePlayerViewApplier());
            _service = CreateCameraPolicy(_applier, _viewModeController);
        }

        [Test]
        public void GameplayZoneAlwaysCameraLookAndIgnoresViewToggle()
        {
            _service.EnterGameplay();

            // ロック直前の中央寄せはInputManagerが担うため、ここではモード指定だけが見える
            // InputManager owns the centering right before the lock, so only the mode push is visible here
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
            Press(MouseDevice.rightButton);
            _service.UpdateRotationInput();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, _applier.Calls);

            _applier.Calls.Clear();
            Release(MouseDevice.rightButton);
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
            Press(MouseDevice.rightButton);
            _service.UpdateRotationInput();
            Release(MouseDevice.rightButton);
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
        public void BuildZoneKeepsCursorAimSourceDuringRightDrag()
        {
            _service.EnterBuildMode();
            Assert.AreEqual(ThirdPersonAimSource.Cursor, AimPointProvider.GetEffectiveAimSource());

            // 右ドラッグで回転しても照準はカーソルのまま（プレビューが画面中央へ跳ねない）
            // The aim stays on the cursor even while right-drag rotates, so the preview never jumps to the center
            Press(MouseDevice.rightButton);
            _service.UpdateRotationInput();
            Assert.AreEqual(ThirdPersonAimSource.Cursor, AimPointProvider.GetEffectiveAimSource());
        }

        [Test]
        public void MenuAndNeutralZonesKeepCursorAimSource()
        {
            _service.EnterGameplay();
            Assert.AreEqual(ThirdPersonAimSource.ScreenCenter, AimPointProvider.GetEffectiveAimSource());

            // 自由カーソルのゾーンで画面中央を狙うとパネル裏の対象がフォーカスされる
            // Centering the aim in a free-cursor zone would focus targets hidden behind panels
            _service.EnterMenu();
            Assert.AreEqual(ThirdPersonAimSource.Cursor, AimPointProvider.GetEffectiveAimSource());

            _service.EnterGameplay();
            _service.ExitToNeutral();
            Assert.AreEqual(ThirdPersonAimSource.Cursor, AimPointProvider.GetEffectiveAimSource());
        }
    }
}
