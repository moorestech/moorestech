using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.UI.UIState;
using NUnit.Framework;

namespace Client.Tests.ViewMode
{
    /// <summary>
    ///     視点モードの常時トグル・モード記憶・ステート別カーソル方針を検証するテスト
    ///     Tests verifying the always-available toggle, mode memory, and the per-state cursor policy
    /// </summary>
    public class PlayerViewModeControllerTest
    {
        private FakePlayerViewApplier _applier;
        private PlayerViewModeController _controller;

        [SetUp]
        public void SetUp()
        {
            _applier = new FakePlayerViewApplier();
            _controller = new PlayerViewModeController(_applier);
        }

        [TearDown]
        public void TearDown()
        {
            AimPointProvider.SetMode(AimPointMode.Mouse);
        }

        [Test]
        public void DefaultsToThirdPersonOnGameScreen()
        {
            _controller.SetUIState(UIStateEnum.GameScreen);
            Assert.AreEqual(PlayerViewMode.ThirdPerson, _controller.CurrentMode);
            Assert.AreEqual(false, _applier.LastFirstPersonCamera);
            Assert.AreEqual(false, _applier.LastCursorVisible);
            Assert.AreEqual(true, _applier.LastCameraRotatable);
            Assert.AreEqual(false, _applier.LastCrosshairVisible);
        }

        [Test]
        public void EnterPlaceBlockInThirdPersonFreesCursorAndKeepsCamera()
        {
            _controller.SetUIState(UIStateEnum.GameScreen);
            _applier.Calls.Clear();

            // 設置モードはカーソルを解放するだけで、カメラは三人称のまま動かさない
            // Entering placement only frees the cursor; the camera stays third-person and is never moved
            _controller.SetUIState(UIStateEnum.PlaceBlock);
            Assert.AreEqual(true, _applier.LastCursorVisible);
            Assert.AreEqual(false, _applier.LastCameraRotatable);
            Assert.AreEqual(false, _applier.LastCrosshairVisible);
            Assert.IsFalse(_applier.Calls.Contains("Fps:True"));
        }

        [Test]
        public void ToggleOnGameScreenAppliesFpsCursorLockAndCrosshair()
        {
            _controller.SetUIState(UIStateEnum.GameScreen);
            _controller.ToggleViewMode();

            Assert.AreEqual(PlayerViewMode.FirstPerson, _controller.CurrentMode);
            Assert.AreEqual(AimPointMode.ScreenCenter, AimPointProvider.CurrentMode);
            Assert.AreEqual(true, _applier.LastFirstPersonCamera);
            Assert.AreEqual(false, _applier.LastCursorVisible);
            Assert.AreEqual(true, _applier.LastCameraRotatable);
            Assert.AreEqual(true, _applier.LastCrosshairVisible);
        }

        [Test]
        public void FirstPersonSurvivesEnteringAndLeavingBuildStates()
        {
            _controller.SetUIState(UIStateEnum.GameScreen);
            _controller.ToggleViewMode();
            _applier.Calls.Clear();

            // FPSのまま設置モードへ入り、ゲーム画面へ戻ってもFPSが維持されること
            // FPS is kept when entering placement and again when returning to the game screen
            _controller.SetUIState(UIStateEnum.PlaceBlock);
            Assert.AreEqual(true, _applier.LastFirstPersonCamera);
            Assert.AreEqual(false, _applier.LastCursorVisible);
            Assert.AreEqual(true, _applier.LastCrosshairVisible);

            _controller.SetUIState(UIStateEnum.GameScreen);
            Assert.AreEqual(PlayerViewMode.FirstPerson, _controller.CurrentMode);
            Assert.AreEqual(true, _applier.LastFirstPersonCamera);
            Assert.AreEqual(true, _applier.LastCrosshairVisible);
            Assert.IsFalse(_applier.Calls.Contains("Fps:False"));
        }

        [Test]
        public void ToggleBackToThirdPersonInPlaceBlockFreesCursor()
        {
            _controller.SetUIState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();
            _applier.Calls.Clear();

            _controller.ToggleViewMode();
            Assert.AreEqual(PlayerViewMode.ThirdPerson, _controller.CurrentMode);
            Assert.AreEqual(AimPointMode.Mouse, AimPointProvider.CurrentMode);
            Assert.Contains("Fps:False", _applier.Calls);
            Assert.AreEqual(true, _applier.LastCursorVisible);
            Assert.AreEqual(false, _applier.LastCameraRotatable);
            Assert.AreEqual(false, _applier.LastCrosshairVisible);
        }

        [Test]
        public void BuildMenuInFirstPersonFreesCursorAndHidesCrosshairKeepingCamera()
        {
            _controller.SetUIState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();
            _applier.Calls.Clear();

            _controller.SetUIState(UIStateEnum.BuildMenu);
            Assert.AreEqual(true, _applier.LastCursorVisible);
            Assert.AreEqual(false, _applier.LastCrosshairVisible);
            Assert.IsFalse(_applier.Calls.Contains("Fps:False"));

            // メニュー中はマウス移動で視点が回らないこと
            // The view must not rotate with mouse movement while the menu is open
            Assert.AreEqual(false, _applier.LastCameraRotatable);

            // カーソル解放中は画面中央ではなくマウス位置が照準になること
            // A freed cursor aims with the mouse, not the screen center
            Assert.AreEqual(AimPointMode.Mouse, AimPointProvider.CurrentMode);
        }

        [Test]
        public void ExitViewStateReturnsAimToMouse()
        {
            _controller.SetUIState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();
            Assert.AreEqual(AimPointMode.ScreenCenter, AimPointProvider.CurrentMode);

            // 視点管理外のステート（インベントリ・F3デバッグ等）はカーソルを解放するため照準をマウスへ戻す
            // States outside the view management (inventory, F3 debug, ...) free the cursor, so the aim returns to the mouse
            _controller.SetUIState(UIStateEnum.PlayerInventory);
            Assert.AreEqual(AimPointMode.Mouse, AimPointProvider.CurrentMode);
            Assert.AreEqual(PlayerViewMode.FirstPerson, _controller.CurrentMode);
        }

        [Test]
        public void ExitViewStateDisablesFirstPersonPresentationKeepingMode()
        {
            _controller.SetUIState(UIStateEnum.GameScreen);
            _controller.ToggleViewMode();
            _applier.Calls.Clear();

            // 視点外ではFPS表示だけ解除する
            // Leaving for a train or inventory keeps the remembered mode while disabling the FPS presentation
            _controller.SetUIState(UIStateEnum.TrainHUDScreen);
            Assert.AreEqual(false, _applier.LastCrosshairVisible);
            Assert.AreEqual(false, _applier.LastCameraRotatable);
            Assert.AreEqual(PlayerViewMode.FirstPerson, _controller.CurrentMode);
            Assert.Contains("Fps:False", _applier.Calls);
        }

        [Test]
        public void NonViewStateDisablesFpsAndReturningViewStateRestoresRememberedMode()
        {
            _controller.SetUIState(UIStateEnum.GameScreen);
            _controller.ToggleViewMode();
            _applier.Calls.Clear();

            _controller.SetUIState(UIStateEnum.TrainHUDScreen);
            Assert.AreEqual(PlayerViewMode.FirstPerson, _controller.CurrentMode);
            Assert.AreEqual(false, _applier.LastFirstPersonCamera);

            _controller.SetUIState(UIStateEnum.GameScreen);
            Assert.AreEqual(true, _applier.LastFirstPersonCamera);
            Assert.Contains("Fps:False", _applier.Calls);
            Assert.Contains("Fps:True", _applier.Calls);
        }

        [Test]
        public void ApplicationFocusRestoreDoesNotOverrideNonViewStateCameraPolicy()
        {
            _controller.SetUIState(UIStateEnum.TrainHUDScreen);
            _applier.Calls.Clear();

            // 列車HUDのカメラ方針は列車側が所有する
            // Train HUD owns its camera policy
            _controller.RestoreAfterApplicationFocus();

            Assert.IsEmpty(_applier.Calls);
        }

    }
}
