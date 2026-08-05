using Client.Game.InGame.Control;
using Client.Game.InGame.Control.ViewMode;
using Client.Input;
using UniRx;

namespace Client.Game.InGame.UI.UIState.State.CameraPolicy
{
    /// <summary>
    ///     UIステート滞在中のカーソル/回転ポリシーの単一所有者。
    ///     Gameplay:常時回転／Menu:自由カーソル／Build:視点別。
    ///     Single owner of the cursor/rotation policy while staying in UI states.
    ///     Gameplay always rotates; Menu frees the cursor; Build follows the view mode.
    /// </summary>
    public class UiStateCameraPolicyService
    {
        private readonly IPlayerCameraInteractionApplier _cameraInteractionApplier;
        private readonly PlayerViewModeController _viewModeController;
        private PolicyZone _currentZone = PolicyZone.Menu;
        private bool _isFirstPerson;

        public UiStateCameraPolicyService(IPlayerCameraInteractionApplier cameraInteractionApplier, PlayerViewModeController viewModeController)
        {
            _cameraInteractionApplier = cameraInteractionApplier;
            _viewModeController = viewModeController;

            // 常設購読でBuildゾーン中のV切替だけ反映する（アプリ寿命Singletonのため破棄しない）
            // Permanent subscription; only V toggles during the Build zone re-apply (never disposed: app-lifetime singleton)
            viewModeController.OnViewModeChanged.Subscribe(OnViewModeChanged);
        }

        public void EnterGameplay()
        {
            _currentZone = PolicyZone.Gameplay;
            ApplyZonePolicy();
        }

        public void EnterMenu()
        {
            _currentZone = PolicyZone.Menu;
            ApplyZonePolicy();
        }

        public void EnterBuildMode()
        {
            _currentZone = PolicyZone.Build;
            ApplyZonePolicy();
        }

        public void UpdateRotationInput()
        {
            // FPSは常時回転のため右ドラッグ切替はTPS限定
            // FPS always rotates, so right-drag toggling is TPS-only
            if (_isFirstPerson) return;

            if (HybridInput.GetMouseButtonDown(1)) _cameraInteractionApplier.SetInteractionMode(CameraInteractionMode.CameraLook);
            if (HybridInput.GetMouseButtonUp(1)) _cameraInteractionApplier.SetInteractionMode(CameraInteractionMode.PointerFree);
        }

        public void ExitToNeutral()
        {
            // 退出時は自由カーソルへ戻し、ポリシーを押さない次のUIが背後の回転を継承しないようにする
            // Exit returns to a free cursor so UIs that push no policy never inherit background rotation
            _cameraInteractionApplier.SetInteractionMode(CameraInteractionMode.PointerFree);
        }

        public void RestoreAfterApplicationFocus()
        {
            // フォーカス復帰は進行中の右ドラッグを破棄して現ゾーンのポリシーへ戻す
            // Focus restore discards any in-progress right-drag and reapplies the current zone policy
            ApplyZonePolicy();
        }

        private void OnViewModeChanged(PlayerViewMode mode)
        {
            if (_currentZone != PolicyZone.Build) return;
            ApplyZonePolicy();
        }

        private void ApplyZonePolicy()
        {
            _isFirstPerson = _viewModeController.GetCurrentMode() == PlayerViewMode.FirstPerson;
            var cameraLook = _currentZone == PolicyZone.Gameplay || (_currentZone == PolicyZone.Build && _isFirstPerson);
            _cameraInteractionApplier.SetInteractionMode(cameraLook ? CameraInteractionMode.CameraLook : CameraInteractionMode.PointerFree);
        }

        private enum PolicyZone
        {
            Gameplay,
            Menu,
            Build,
        }
    }
}
