using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.UI.UIState.State.CameraPolicy;
using Client.Tests.UIState.Fakes;
using Client.Tests.ViewMode;
using NUnit.Framework;

namespace Client.Tests.UIState.CameraPolicy
{
    /// <summary>
    ///     Gameplayゾーンの左Altホールド（自由カーソルと三人称照準ソース）専用
    ///     Covers the left-Alt hold in the Gameplay zone: free cursor and third-person aim source
    /// </summary>
    public class UiStateCameraPolicyAltHoldTest : UIStateTestFixtureBase
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
        public void GameplayZoneTpsFreesPointerWhileLeftAltHeld()
        {
            _service.EnterGameplay();

            // ロック解除を先に済ませてからワープする
            // The unlock lands before the warp
            _applier.Calls.Clear();
            Press(KeyboardDevice.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            CollectionAssert.AreEqual(new[] { "Mode:PointerFree", "Warp" }, _applier.Calls);

            _applier.Calls.Clear();
            Release(KeyboardDevice.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, _applier.Calls);
        }

        [Test]
        public void GameplayZoneAltHoldSwitchesAimSourceToCursor()
        {
            _service.EnterGameplay();
            Assert.AreEqual(ThirdPersonAimSource.ScreenCenter, AimPointProvider.GetEffectiveAimSource());

            Press(KeyboardDevice.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            Assert.AreEqual(ThirdPersonAimSource.Cursor, AimPointProvider.GetEffectiveAimSource());

            Release(KeyboardDevice.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            Assert.AreEqual(ThirdPersonAimSource.ScreenCenter, AimPointProvider.GetEffectiveAimSource());
        }

        [Test]
        public void GameplayZoneFpsIgnoresLeftAlt()
        {
            _viewModeController.ToggleViewMode();
            _service.EnterGameplay();

            _applier.Calls.Clear();
            Press(KeyboardDevice.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            Release(KeyboardDevice.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            CollectionAssert.IsEmpty(_applier.Calls);
        }

        [Test]
        public void GameplayViewToggleDiscardsAltHold()
        {
            _service.EnterGameplay();
            Press(KeyboardDevice.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();

            // 視点切替で破棄され押し直すまで固定
            // A view toggle discards the hold and the cursor stays locked
            _applier.Calls.Clear();
            _viewModeController.ToggleViewMode();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, _applier.Calls);
            Assert.AreEqual(ThirdPersonAimSource.ScreenCenter, AimPointProvider.GetEffectiveAimSource());
        }

        [Test]
        public void ReleaseAfterDiscardedHoldPushesNothing()
        {
            _service.EnterGameplay();
            Press(KeyboardDevice.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();

            // 破棄済みホールドの解放は何も出さない
            // Releasing an already discarded hold pushes nothing
            _service.RestoreAfterApplicationFocus();
            _applier.Calls.Clear();
            Release(KeyboardDevice.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            CollectionAssert.IsEmpty(_applier.Calls);
        }

        [Test]
        public void NeutralZoneIgnoresLeftAltInput()
        {
            _service.EnterGameplay();
            _service.ExitToNeutral();

            // 手放したゾーンをAlt入力で取り戻さない
            // An Alt input must not reclaim a zone that was already released
            _applier.Calls.Clear();
            Press(KeyboardDevice.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            CollectionAssert.IsEmpty(_applier.Calls);
            Assert.AreEqual(ThirdPersonAimSource.Cursor, AimPointProvider.GetEffectiveAimSource());
        }

        [Test]
        public void ReenteringGameplayStartsLockedWhileAltStillHeld()
        {
            _service.EnterGameplay();
            Press(KeyboardDevice.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            _service.ExitToNeutral();

            // Altを押したまま戻っても押し直すまで自由カーソルにしない（ホールドはエッジ検出のため）
            // Re-entering with Alt still down stays locked until it is pressed again (the hold is edge-detected)
            _applier.Calls.Clear();
            _service.EnterGameplay();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, _applier.Calls);
            Assert.AreEqual(ThirdPersonAimSource.ScreenCenter, AimPointProvider.GetEffectiveAimSource());
        }
    }
}
