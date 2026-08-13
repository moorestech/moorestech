using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.UI.UIState.State.CameraPolicy;
using Client.Tests.UIState.Fakes;
using Client.Tests.ViewMode;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Client.Tests.UIState.CameraPolicy
{
    /// <summary>
    ///     Gameplayゾーンの左Altホールド（自由カーソルと三人称照準ソース）専用
    ///     Covers the left-Alt hold in the Gameplay zone: free cursor and third-person aim source
    /// </summary>
    public class UiStateCameraPolicyAltHoldTest : InputTestFixture
    {
        private Keyboard _keyboard;
        private FakePlayerCameraInteractionApplier _applier;
        private PlayerViewModeController _viewModeController;
        private UiStateCameraPolicyService _service;

        public override void Setup()
        {
            base.Setup();
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
        public void GameplayZoneTpsFreesPointerWhileLeftAltHeld()
        {
            _service.EnterGameplay();

            // ロック解除を先に済ませてからワープする
            // The unlock lands before the warp
            _applier.Calls.Clear();
            Press(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            CollectionAssert.AreEqual(new[] { "Mode:PointerFree", "Warp" }, _applier.Calls);

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
        public void ReleaseAfterDiscardedHoldPushesNothing()
        {
            _service.EnterGameplay();
            Press(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();

            // フォーカス復帰でホールドは破棄済みなので、その後の解放は余計な再適用を出さない
            // The focus restore already discarded the hold, so the later release pushes no redundant re-apply
            _service.RestoreAfterApplicationFocus();
            _applier.Calls.Clear();
            Release(_keyboard.leftAltKey);
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
            Press(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            CollectionAssert.IsEmpty(_applier.Calls);
            Assert.AreEqual(AimPointMode.Mouse, AimPointProvider.GetCurrentMode());
        }

        [Test]
        public void ReenteringGameplayStartsLockedWhileAltStillHeld()
        {
            _service.EnterGameplay();
            Press(_keyboard.leftAltKey);
            _service.UpdateGameplayFreeCursorInput();
            _service.ExitToNeutral();

            // Altを押したまま戻っても押し直すまで自由カーソルにしない（ホールドはエッジ検出のため）
            // Re-entering with Alt still down stays locked until it is pressed again (the hold is edge-detected)
            _applier.Calls.Clear();
            _service.EnterGameplay();
            CollectionAssert.AreEqual(new[] { "Mode:CameraLook" }, _applier.Calls);
            Assert.AreEqual(AimPointMode.ScreenCenter, AimPointProvider.GetCurrentMode());
        }
    }
}
