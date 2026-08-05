using System;
using Client.Input;
using UniRx;

namespace Client.Game.InGame.Control.ViewMode
{
    /// <summary>
    ///     設置・破壊モードのカーソル/回転を視点別に制御する。
    ///     FPS:ロック+常時回転／TPS:右ドラッグ中のみ回転。
    ///     Controls cursor and camera rotation per view mode while in build/delete modes.
    ///     FPS keeps the cursor locked and always rotating; TPS rotates only during right-drag.
    /// </summary>
    public class BuildModeCameraInteractionService
    {
        private readonly IPlayerCameraInteractionApplier _cameraInteractionApplier;
        private readonly PlayerViewModeController _viewModeController;
        private IDisposable _viewModeSubscription;
        private bool _isFirstPerson;

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
            if (_isFirstPerson) return;

            if (HybridInput.GetMouseButtonDown(1)) SetCameraControl(true);
            if (HybridInput.GetMouseButtonUp(1)) SetCameraControl(false);
        }

        public void OnExit()
        {
            // 退出時は一律ベースラインへ戻す（次ステートのOnEnterが必ず上書きする前提）
            // Exit always resets to baseline; the next state's OnEnter is guaranteed to overwrite it
            _viewModeSubscription.Dispose();
            SetCameraControl(false);
        }

        public void RestoreAfterApplicationFocus()
        {
            // フォーカス復帰は進行中の右ドラッグを破棄して現在モードのポリシーへ戻す
            // Focus restore discards any in-progress right-drag and reapplies the current mode policy
            ApplyPolicy(_viewModeController.GetCurrentMode());
        }

        private void ApplyPolicy(PlayerViewMode mode)
        {
            _isFirstPerson = mode == PlayerViewMode.FirstPerson;
            SetCameraControl(_isFirstPerson);
        }

        private void SetCameraControl(bool rotating)
        {
            // カーソル表示と回転可否は常に逆相ペアで適用する
            // Cursor visibility and rotatability are always applied as an inverse pair
            _cameraInteractionApplier.SetCursorVisible(!rotating);
            _cameraInteractionApplier.SetCameraRotatable(rotating);
        }
    }
}
