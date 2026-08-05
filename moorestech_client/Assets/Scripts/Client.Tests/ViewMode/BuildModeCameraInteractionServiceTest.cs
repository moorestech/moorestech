using Client.Game.InGame.Control.ViewMode;
using Client.Tests.UIState;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Client.Tests.ViewMode
{
    public class BuildModeCameraInteractionServiceTest : InputTestFixture
    {
        private Mouse _mouse;
        private FakePlayerCameraInteractionApplier _applier;
        private PlayerViewModeController _viewModeController;
        private BuildModeCameraInteractionService _service;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _applier = new FakePlayerCameraInteractionApplier();
            _viewModeController = new PlayerViewModeController(new FakePlayerViewApplier());
            _service = new BuildModeCameraInteractionService(_applier, _viewModeController);
        }

        [Test]
        public void TpsEnterShowsCursorAndRotatesOnlyDuringRightDrag()
        {
            _service.OnEnter();
            CollectionAssert.AreEqual(new[] { "Cursor:True", "Rotatable:False" }, _applier.Calls);

            _applier.Calls.Clear();
            Press(_mouse.rightButton);
            _service.UpdateRotationInput();
            CollectionAssert.AreEqual(new[] { "Cursor:False", "Rotatable:True" }, _applier.Calls);

            _applier.Calls.Clear();
            Release(_mouse.rightButton);
            _service.UpdateRotationInput();
            CollectionAssert.AreEqual(new[] { "Cursor:True", "Rotatable:False" }, _applier.Calls);
        }

        [Test]
        public void FpsEnterLocksCursorAndAlwaysRotates()
        {
            _viewModeController.ToggleViewMode();
            _service.OnEnter();
            CollectionAssert.AreEqual(new[] { "Cursor:False", "Rotatable:True" }, _applier.Calls);

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
        public void ViewModeToggleWhileStayingReappliesPolicy()
        {
            _service.OnEnter();

            _applier.Calls.Clear();
            _viewModeController.ToggleViewMode();
            CollectionAssert.AreEqual(new[] { "Cursor:False", "Rotatable:True" }, _applier.Calls);

            _applier.Calls.Clear();
            _viewModeController.ToggleViewMode();
            CollectionAssert.AreEqual(new[] { "Cursor:True", "Rotatable:False" }, _applier.Calls);
        }

        [Test]
        public void ExitRestoresBaselineAndStopsFollowingToggle()
        {
            _service.OnEnter();

            _applier.Calls.Clear();
            _service.OnExit();
            CollectionAssert.AreEqual(new[] { "Cursor:True", "Rotatable:False" }, _applier.Calls);

            // 退出後はV切替に追従しない
            // After exit the service no longer follows V toggles
            _applier.Calls.Clear();
            _viewModeController.ToggleViewMode();
            CollectionAssert.IsEmpty(_applier.Calls);
        }

        [Test]
        public void ReenterAfterExitFollowsToggleExactlyOnce()
        {
            // 再入場で購読が張り直され、退出後は追従しないことを同一インスタンスで検証
            // Verify on one instance that re-entry resubscribes and exit stops following
            _service.OnEnter();
            _service.OnExit();
            _service.OnEnter();

            _applier.Calls.Clear();
            _viewModeController.ToggleViewMode();
            CollectionAssert.AreEqual(new[] { "Cursor:False", "Rotatable:True" }, _applier.Calls);

            _applier.Calls.Clear();
            _service.OnExit();
            _viewModeController.ToggleViewMode();
            CollectionAssert.AreEqual(new[] { "Cursor:True", "Rotatable:False" }, _applier.Calls);
        }

        [Test]
        public void RestoreAfterFocusAppliesCurrentModePolicy()
        {
            _service.OnEnter();

            _applier.Calls.Clear();
            _service.RestoreAfterApplicationFocus();
            CollectionAssert.AreEqual(new[] { "Cursor:True", "Rotatable:False" }, _applier.Calls);

            _viewModeController.ToggleViewMode();
            _applier.Calls.Clear();
            _service.RestoreAfterApplicationFocus();
            CollectionAssert.AreEqual(new[] { "Cursor:False", "Rotatable:True" }, _applier.Calls);
        }
    }
}
