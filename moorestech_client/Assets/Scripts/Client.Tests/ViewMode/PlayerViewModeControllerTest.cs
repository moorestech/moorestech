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
            AimPointProvider.SetMode(PlayerViewMode.ThirdPerson);
        }

        [Test]
        public void DefaultsToThirdPersonOnGameScreen()
        {
            _controller.OnEnterViewState(UIStateEnum.GameScreen);
            Assert.AreEqual(PlayerViewMode.ThirdPerson, _controller.CurrentMode);
            Assert.AreEqual(false, _applier.LastFirstPersonCamera);
            Assert.AreEqual(false, _applier.LastCursorVisible);
            Assert.AreEqual(true, _applier.LastCameraRotatable);
            Assert.AreEqual(false, _applier.LastCrosshairVisible);
        }

        [Test]
        public void EnterPlaceBlockInThirdPersonFreesCursorAndKeepsCamera()
        {
            _controller.OnEnterViewState(UIStateEnum.GameScreen);
            _applier.Calls.Clear();

            // 設置モードはカーソルを解放するだけで、カメラは三人称のまま動かさない
            // Entering placement only frees the cursor; the camera stays third-person and is never moved
            _controller.OnEnterViewState(UIStateEnum.PlaceBlock);
            Assert.AreEqual(true, _applier.LastCursorVisible);
            Assert.AreEqual(false, _applier.LastCameraRotatable);
            Assert.AreEqual(false, _applier.LastCrosshairVisible);
            Assert.IsFalse(_applier.Calls.Contains("Fps:True"));
        }

        [Test]
        public void ToggleOnGameScreenAppliesFpsCursorLockAndCrosshair()
        {
            _controller.OnEnterViewState(UIStateEnum.GameScreen);
            _controller.ToggleViewMode();

            Assert.AreEqual(PlayerViewMode.FirstPerson, _controller.CurrentMode);
            Assert.AreEqual(PlayerViewMode.FirstPerson, AimPointProvider.CurrentMode);
            Assert.AreEqual(true, _applier.LastFirstPersonCamera);
            Assert.AreEqual(false, _applier.LastCursorVisible);
            Assert.AreEqual(true, _applier.LastCameraRotatable);
            Assert.AreEqual(true, _applier.LastCrosshairVisible);
        }

        [Test]
        public void FirstPersonSurvivesEnteringAndLeavingBuildStates()
        {
            _controller.OnEnterViewState(UIStateEnum.GameScreen);
            _controller.ToggleViewMode();
            _applier.Calls.Clear();

            // FPSのまま設置モードへ入り、ゲーム画面へ戻ってもFPSが維持されること
            // FPS is kept when entering placement and again when returning to the game screen
            _controller.OnExitViewState();
            _controller.OnEnterViewState(UIStateEnum.PlaceBlock);
            Assert.AreEqual(true, _applier.LastFirstPersonCamera);
            Assert.AreEqual(false, _applier.LastCursorVisible);
            Assert.AreEqual(true, _applier.LastCrosshairVisible);

            _controller.OnExitViewState();
            _controller.OnEnterViewState(UIStateEnum.GameScreen);
            Assert.AreEqual(PlayerViewMode.FirstPerson, _controller.CurrentMode);
            Assert.AreEqual(true, _applier.LastFirstPersonCamera);
            Assert.AreEqual(true, _applier.LastCrosshairVisible);
            Assert.IsFalse(_applier.Calls.Contains("Fps:False"));
        }

        [Test]
        public void ToggleBackToThirdPersonInPlaceBlockFreesCursor()
        {
            _controller.OnEnterViewState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();
            _applier.Calls.Clear();

            _controller.ToggleViewMode();
            Assert.AreEqual(PlayerViewMode.ThirdPerson, _controller.CurrentMode);
            Assert.AreEqual(PlayerViewMode.ThirdPerson, AimPointProvider.CurrentMode);
            Assert.Contains("Fps:False", _applier.Calls);
            Assert.AreEqual(true, _applier.LastCursorVisible);
            Assert.AreEqual(false, _applier.LastCameraRotatable);
            Assert.AreEqual(false, _applier.LastCrosshairVisible);
        }

        [Test]
        public void BuildMenuInFirstPersonFreesCursorAndHidesCrosshairKeepingCamera()
        {
            _controller.OnEnterViewState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();
            _applier.Calls.Clear();

            _controller.OnExitViewState();
            _controller.OnEnterViewState(UIStateEnum.BuildMenu);
            Assert.AreEqual(true, _applier.LastCursorVisible);
            Assert.AreEqual(false, _applier.LastCrosshairVisible);
            Assert.IsFalse(_applier.Calls.Contains("Fps:False"));

            // メニュー中はマウス移動で視点が回らないこと
            // The view must not rotate with mouse movement while the menu is open
            Assert.AreEqual(false, _applier.LastCameraRotatable);

            // カーソル解放中は画面中央ではなくマウス位置が照準になること
            // A freed cursor aims with the mouse, not the screen center
            Assert.AreEqual(PlayerViewMode.ThirdPerson, AimPointProvider.CurrentMode);
        }

        [Test]
        public void ExitViewStateReturnsAimToMouse()
        {
            _controller.OnEnterViewState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();
            Assert.AreEqual(PlayerViewMode.FirstPerson, AimPointProvider.CurrentMode);

            // 視点管理外のステート（インベントリ・F3デバッグ等）はカーソルを解放するため照準をマウスへ戻す
            // States outside the view management (inventory, F3 debug, ...) free the cursor, so the aim returns to the mouse
            _controller.OnExitViewState();
            Assert.AreEqual(PlayerViewMode.ThirdPerson, AimPointProvider.CurrentMode);
            Assert.AreEqual(PlayerViewMode.FirstPerson, _controller.CurrentMode);
        }

        [Test]
        public void ExitViewStateDropsCrosshairAndRotationKeepingMode()
        {
            _controller.OnEnterViewState(UIStateEnum.GameScreen);
            _controller.ToggleViewMode();
            _applier.Calls.Clear();

            // インベントリ等へ抜ける際はクロスヘアと回転だけを落とし、モードとFPSカメラは維持する
            // Leaving to the inventory only drops the crosshair and rotation; the mode and FPS camera stay
            _controller.OnExitViewState();
            Assert.AreEqual(false, _applier.LastCrosshairVisible);
            Assert.AreEqual(false, _applier.LastCameraRotatable);
            Assert.AreEqual(PlayerViewMode.FirstPerson, _controller.CurrentMode);
            Assert.IsFalse(_applier.Calls.Contains("Fps:False"));
        }

        [Test]
        public void TextInputFocusInFirstPersonFreesCursorAndRestoresOnUnfocus()
        {
            _controller.OnEnterViewState(UIStateEnum.PlaceBlock);
            _controller.ToggleViewMode();

            // フォーカス中はカーソル解放・回転停止・クロスヘア非表示になり、照準もマウスへ戻ること
            // While focused the cursor is freed, rotation stops, the crosshair hides, and the aim returns to the mouse
            _controller.SetTextInputFocused(true);
            Assert.AreEqual(true, _applier.LastCursorVisible);
            Assert.AreEqual(false, _applier.LastCameraRotatable);
            Assert.AreEqual(false, _applier.LastCrosshairVisible);
            Assert.AreEqual(PlayerViewMode.ThirdPerson, AimPointProvider.CurrentMode);

            // フォーカス解除でFPSのカーソルロック・回転・クロスヘア・中央照準へ戻ること
            // On unfocus the FPS cursor lock, rotation, crosshair, and center aim are restored
            _controller.SetTextInputFocused(false);
            Assert.AreEqual(false, _applier.LastCursorVisible);
            Assert.AreEqual(true, _applier.LastCameraRotatable);
            Assert.AreEqual(true, _applier.LastCrosshairVisible);
            Assert.AreEqual(PlayerViewMode.FirstPerson, AimPointProvider.CurrentMode);
        }

        [Test]
        public void TextInputFocusInThirdPersonIsNoOp()
        {
            _controller.OnEnterViewState(UIStateEnum.PlaceBlock);
            var callCount = _applier.Calls.Count;

            // 三人称ではカーソルは元々解放済みのため何も適用しないこと
            // Third-person already has a free cursor, so focus changes apply nothing
            _controller.SetTextInputFocused(true);
            _controller.SetTextInputFocused(false);
            Assert.AreEqual(callCount, _applier.Calls.Count);
        }
    }
}
