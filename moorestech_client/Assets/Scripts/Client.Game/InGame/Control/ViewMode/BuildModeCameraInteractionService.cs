using System;
using Client.Input;
using UniRx;

namespace Client.Game.InGame.Control.ViewMode
{
    /// <summary>
    ///     設置・破壊モード滞在中のカーソル表示とカメラ回転を視点モード別に制御する。
    ///     FPSは通常FPS操作（カーソルロック＋常時回転）、TPSはカーソル表示で右ドラッグ中のみ回転。
    ///     Controls cursor visibility and camera rotation per view mode while in build/delete modes.
    ///     FPS keeps normal FPS control (locked cursor + always rotating); TPS shows the cursor and rotates only during right-drag.
    /// </summary>
    public class BuildModeCameraInteractionService
    {
        private readonly IPlayerCameraInteractionApplier _cameraInteractionApplier;
        private readonly PlayerViewModeController _viewModeController;
        private IDisposable _viewModeSubscription;

        public BuildModeCameraInteractionService(IPlayerCameraInteractionApplier cameraInteractionApplier, PlayerViewModeController viewModeController)
        {
            _cameraInteractionApplier = cameraInteractionApplier;
            _viewModeController = viewModeController;
        }

        public void OnEnter()
        {
            // 滞在中のV切替にも追従できるよう購読して適用し直す
            // Subscribe so mid-stay V toggles re-apply the policy
            ApplyPolicy(_viewModeController.GetCurrentMode());
            _viewModeSubscription = _viewModeController.OnViewModeChanged.Subscribe(ApplyPolicy);
        }

        public void UpdateRotationInput()
        {
            // FPSは常時回転のため右ドラッグ切替はTPS限定
            // FPS always rotates, so right-drag toggling is TPS-only
            if (_viewModeController.GetCurrentMode() == PlayerViewMode.FirstPerson) return;

            if (HybridInput.GetMouseButtonDown(1))
            {
                _cameraInteractionApplier.SetCursorVisible(false);
                _cameraInteractionApplier.SetCameraRotatable(true);
            }

            if (!HybridInput.GetMouseButtonUp(1)) return;
            _cameraInteractionApplier.SetCursorVisible(true);
            _cameraInteractionApplier.SetCameraRotatable(false);
        }

        public void OnExit()
        {
            _viewModeSubscription.Dispose();
            _cameraInteractionApplier.SetCursorVisible(true);
            _cameraInteractionApplier.SetCameraRotatable(false);
        }

        public void RestoreAfterApplicationFocus()
        {
            ApplyPolicy(_viewModeController.GetCurrentMode());
        }

        private void ApplyPolicy(PlayerViewMode mode)
        {
            var isFirstPerson = mode == PlayerViewMode.FirstPerson;
            _cameraInteractionApplier.SetCursorVisible(!isFirstPerson);
            _cameraInteractionApplier.SetCameraRotatable(isFirstPerson);
        }
    }
}
