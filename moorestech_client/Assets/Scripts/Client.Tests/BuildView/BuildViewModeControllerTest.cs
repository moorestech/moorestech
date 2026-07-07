using Client.Game.InGame.Control.BuildView;
using Client.Game.InGame.UI.UIState;
using NUnit.Framework;

namespace Client.Tests.BuildView
{
    /// <summary>
    ///     視点モードのセッション管理・トグル・記憶を検証するテスト
    ///     Tests verifying view-mode session handling, toggling, and memory
    /// </summary>
    public class BuildViewModeControllerTest
    {
        private FakeBuildViewApplier _applier;
        private BuildViewModeController _controller;

        [SetUp]
        public void SetUp()
        {
            _applier = new FakeBuildViewApplier();
            _controller = new BuildViewModeController(_applier);
        }

        [TearDown]
        public void TearDown()
        {
            AimPointProvider.SetMode(BuildViewMode.TopDown);
        }

        [Test]
        public void EnterPlaceBlockInTopDownAppliesTopDownCamera()
        {
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            Assert.Contains("Capture", _applier.Calls);
            Assert.Contains("TopDown", _applier.Calls);
            Assert.AreEqual(true, _applier.LastCursorVisible);
        }

        [Test]
        public void EnterDeleteBarInTopDownDoesNotMoveCamera()
        {
            _controller.OnEnterBuildState(UIStateEnum.DeleteBar);
            Assert.IsFalse(_applier.Calls.Contains("TopDown"));
            Assert.IsFalse(_applier.Calls.Contains("Restore"));
        }

        [Test]
        public void TransitBetweenBuildStatesCapturesCameraOnlyOnce()
        {
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            _controller.OnLeaveBuildState(UIStateEnum.DeleteBar);
            _controller.OnEnterBuildState(UIStateEnum.DeleteBar);
            Assert.AreEqual(1, _applier.Calls.FindAll(c => c == "Capture").Count);
        }

        [Test]
        public void LeaveToBuildStateDoesNotRestoreCamera()
        {
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            _controller.OnLeaveBuildState(UIStateEnum.BuildMenu);
            Assert.IsFalse(_applier.Calls.Contains("Restore"));
        }

        [Test]
        public void LeaveToGameScreenRestoresSavedCameraAndHidesCursor()
        {
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            _controller.OnLeaveBuildState(UIStateEnum.GameScreen);
            Assert.AreSame(_applier.CapturedCamera, _applier.LastRestoredCamera);
            Assert.AreEqual(false, _applier.LastCursorVisible);
        }

        [Test]
        public void ToggleToFirstPersonAppliesFpsCursorLockAndCrosshair()
        {
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();
            Assert.AreEqual(BuildViewMode.FirstPerson, _controller.CurrentMode);
            Assert.AreEqual(BuildViewMode.FirstPerson, AimPointProvider.CurrentMode);
            Assert.AreEqual(true, _applier.LastFirstPersonCamera);
            Assert.AreEqual(true, _applier.LastCrosshairVisible);
            Assert.AreEqual(false, _applier.LastCursorVisible);
        }

        [Test]
        public void FirstPersonModeIsRememberedAcrossSessions()
        {
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();
            _controller.OnLeaveBuildState(UIStateEnum.GameScreen);
            _applier.Calls.Clear();

            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            Assert.Contains("Fps:True", _applier.Calls);
            Assert.IsFalse(_applier.Calls.Contains("TopDown"));
            // 再入場で記憶したFPSが照準プロバイダにも再適用されること
            // Re-entering re-applies the remembered FPS mode to the aim provider
            Assert.AreEqual(BuildViewMode.FirstPerson, AimPointProvider.CurrentMode);
        }

        [Test]
        public void BuildMenuInFirstPersonFreesCursorAndHidesCrosshairKeepingCamera()
        {
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();
            _controller.OnLeaveBuildState(UIStateEnum.BuildMenu);
            _controller.OnEnterBuildState(UIStateEnum.BuildMenu);
            Assert.AreEqual(true, _applier.LastCursorVisible);
            Assert.AreEqual(false, _applier.LastCrosshairVisible);
            Assert.IsFalse(_applier.Calls.Contains("Fps:False"));
            // メニュー中はマウス移動で視点が回らないこと
            // The view must not rotate with mouse movement while the menu is open
            Assert.Contains("Rotatable:False", _applier.Calls);
        }

        [Test]
        public void LeaveFromFirstPersonDisablesFpsAndRestores()
        {
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();
            _controller.OnLeaveBuildState(UIStateEnum.GameScreen);
            Assert.Contains("Fps:False", _applier.Calls);
            Assert.Contains("Restore", _applier.Calls);
            Assert.AreEqual(false, _applier.LastCrosshairVisible);
            // セッション外の照準はマウスへ戻り、回転も解除されること
            // Outside the session the aim returns to the mouse and rotation is cleared
            Assert.AreEqual(BuildViewMode.TopDown, AimPointProvider.CurrentMode);
            Assert.Contains("Rotatable:False", _applier.Calls);
        }

        [Test]
        public void ToggleBackToTopDownInDeleteBarRestoresSavedCamera()
        {
            _controller.OnEnterBuildState(UIStateEnum.DeleteBar);
            _controller.ToggleViewMode();
            _applier.Calls.Clear();
            _controller.ToggleViewMode();
            Assert.Contains("Fps:False", _applier.Calls);
            Assert.Contains("Restore", _applier.Calls);
        }

        [Test]
        public void ToggleBackToTopDownInPlaceBlockAppliesTopDown()
        {
            _controller.OnEnterBuildState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();
            _applier.Calls.Clear();
            _controller.ToggleViewMode();
            Assert.Contains("Fps:False", _applier.Calls);
            Assert.Contains("TopDown", _applier.Calls);
            Assert.IsFalse(_applier.Calls.Contains("Restore"));
        }
    }
}
