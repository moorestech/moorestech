using Client.Game.InGame.Control;
using Client.Game.InGame.Control.ViewMode;
using Client.Input;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.CameraPolicy
{
    /// <summary>
    ///     UIステート滞在中のカーソル/回転ポリシーの単一所有者。
    ///     - Gameplay: 常時回転（左Altホールド中のみ自由カーソル）
    ///     - Menu: 自由カーソル
    ///     - Build: 視点別
    ///     - Neutral: 所有者なし（自由カーソル）
    ///     三人称照準ソースもゾーンごとにAimPointProviderへプッシュする。
    ///     Single owner of the cursor/rotation policy while staying in UI states.
    ///     - Gameplay: always rotates (frees the cursor only while left Alt is held)
    ///     - Menu: frees the cursor
    ///     - Build: follows the view mode
    ///     - Neutral: owned by nobody (frees the cursor)
    ///     Also pushes the third-person aim source to AimPointProvider per zone.
    /// </summary>
    public class UiStateCameraPolicyService
    {
        private readonly IPlayerCameraInteractionApplier _cameraInteractionApplier;
        private readonly PlayerViewModeController _viewModeController;
        private PolicyZone _currentZone = PolicyZone.Neutral;
        private bool _isGameplayAltHeld;

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
            _isGameplayAltHeld = false;
            ApplyZonePolicy();
        }

        public void EnterMenu()
        {
            _currentZone = PolicyZone.Menu;
            _isGameplayAltHeld = false;
            ApplyZonePolicy();
        }

        public void EnterBuildMode()
        {
            _currentZone = PolicyZone.Build;
            _isGameplayAltHeld = false;
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

        public void UpdateGameplayFreeCursorInput()
        {
            // Gameplayを所有していない間の呼び出しでゾーンを取り戻さない
            // A call while this zone is not owned must not reclaim it
            if (_currentZone != PolicyZone.Gameplay) return;

            // 一人称は左Altを受け付けない（クロスヘア＋画面中央照準で一貫させる）
            // First person ignores left Alt to keep the crosshair/screen-center aim consistent
            if (IsFirstPerson) return;

            if (HybridInput.GetKeyDown(KeyCode.LeftAlt))
            {
                _isGameplayAltHeld = true;

                // ロック解除してからワープする（ロック中のワープはOSに握り潰される）
                // Unlock first, then warp: a warp while the cursor is locked is dropped by the OS
                ApplyZonePolicy();
                _cameraInteractionApplier.WarpCursorToScreenCenter();
            }

            // ホールドしていない状態の解放通知では余計な再適用をしない
            // A release without an active hold must not push a redundant re-apply
            if (HybridInput.GetKeyUp(KeyCode.LeftAlt) && _isGameplayAltHeld)
            {
                _isGameplayAltHeld = false;
                ApplyZonePolicy();
            }
        }

        public void ExitToNeutral()
        {
            // 退出時はゾーン所有を手放し自由カーソルへ戻す（退出後のV切替が旧ゾーンを再適用しないため）
            // Exit releases zone ownership and frees the cursor so a later V toggle never re-applies the old zone
            _currentZone = PolicyZone.Neutral;
            _isGameplayAltHeld = false;
            ApplyZonePolicy();
        }

        public void RestoreAfterApplicationFocus()
        {
            // 右ドラッグと左Altホールドを破棄し現ゾーンへ戻す
            // Discards any in-progress right-drag and Alt hold, then reapplies the current zone policy
            _isGameplayAltHeld = false;
            ApplyZonePolicy();
        }

        private void OnViewModeChanged(PlayerViewMode mode)
        {
            // Gameplayでホールド中の視点切替はホールドを破棄してから再適用する
            // A view toggle while holding Alt in Gameplay discards the hold before reapplying
            if (_currentZone == PolicyZone.Gameplay && _isGameplayAltHeld)
            {
                _isGameplayAltHeld = false;
                ApplyZonePolicy();
                return;
            }

            if (_currentZone != PolicyZone.Build) return;
            ApplyZonePolicy();
        }

        private void ApplyZonePolicy()
        {
            var isGameplayLocked = _currentZone == PolicyZone.Gameplay && !_isGameplayAltHeld;
            var cameraLook = isGameplayLocked || (_currentZone == PolicyZone.Build && IsFirstPerson);
            _cameraInteractionApplier.SetInteractionMode(cameraLook ? CameraInteractionMode.CameraLook : CameraInteractionMode.PointerFree);

            // 画面中央照準はカーソルを固定するGameplayだけ。他ゾーンは自由カーソルなのでカーソルを狙う
            // Only the cursor-locked Gameplay aims at screen center; other zones have a free cursor and aim at it
            var aimSource = isGameplayLocked ? ThirdPersonAimSource.ScreenCenter : ThirdPersonAimSource.Cursor;
            AimPointProvider.SetThirdPersonAimSource(aimSource);
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
