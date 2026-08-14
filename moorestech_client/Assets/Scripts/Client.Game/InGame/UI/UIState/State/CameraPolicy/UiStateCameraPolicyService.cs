using Client.Game.InGame.Control;
using Client.Game.InGame.Control.ViewMode;
using Client.Input;
using UniRx;

namespace Client.Game.InGame.UI.UIState.State.CameraPolicy
{
    /// <summary>
    ///     UIステート滞在中のカーソル/回転ポリシーの単一所有者。
    ///     - Gameplay: 常時回転
    ///     - Menu: 自由カーソル
    ///     - Build: 視点別
    ///     - Neutral: 所有者なし（自由カーソル）
    ///     Single owner of the cursor/rotation policy while staying in UI states.
    ///     - Gameplay: always rotates
    ///     - Menu: frees the cursor
    ///     - Build: follows the view mode
    ///     - Neutral: owned by nobody (frees the cursor)
    /// </summary>
    public class UiStateCameraPolicyService
    {
        private readonly IPlayerCameraInteractionApplier _cameraInteractionApplier;
        private readonly PlayerViewModeController _viewModeController;
        private PolicyZone _currentZone = PolicyZone.Neutral;

        private bool IsFirstPerson => _viewModeController.GetCurrentMode() == PlayerViewMode.FirstPerson;

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
            // FPS常時回転ゆえ右ドラッグはTPS限定
            // FPS always rotates, so right-drag toggling is TPS-only
            if (IsFirstPerson) return;

            if (HybridInput.GetMouseButtonDown(1)) _cameraInteractionApplier.SetInteractionMode(CameraInteractionMode.CameraLook);
            if (HybridInput.GetMouseButtonUp(1)) _cameraInteractionApplier.SetInteractionMode(CameraInteractionMode.PointerFree);
        }

        public void ExitToNeutral()
        {
            // 退出時はゾーン所有を手放し自由カーソルへ戻す（退出後のV切替が旧ゾーンを再適用しないため）
            // Exit releases zone ownership and frees the cursor so a later V toggle never re-applies the old zone
            _currentZone = PolicyZone.Neutral;
            ApplyZonePolicy();
        }

        public void RestoreAfterApplicationFocus()
        {
            // 右ドラッグを破棄し現ゾーンへ戻す
            // Discards any in-progress right-drag and reapplies the current zone policy
            ApplyZonePolicy();
        }

        private void OnViewModeChanged(PlayerViewMode mode)
        {
            if (_currentZone != PolicyZone.Build) return;
            ApplyZonePolicy();
        }

        private void ApplyZonePolicy()
        {
            var cameraLook = _currentZone == PolicyZone.Gameplay || (_currentZone == PolicyZone.Build && IsFirstPerson);
            _cameraInteractionApplier.SetInteractionMode(cameraLook ? CameraInteractionMode.CameraLook : CameraInteractionMode.PointerFree);
        }

        private enum PolicyZone
        {
            Neutral,
            Gameplay,
            Menu,
            Build,
        }
    }
}
