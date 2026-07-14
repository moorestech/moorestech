using Client.Game.InGame.UI.UIState;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.Control.ViewMode
{
    public class ThirdPersonCameraDistance
    {
        public const float MinimumDistance = 0.6f;
        public const float MaximumDistance = 10f;

        private float _distance;
        private bool _isTransitioning;

        public ThirdPersonCameraDistance(float initialDistance)
        {
            _distance = Mathf.Clamp(initialDistance, MinimumDistance, MaximumDistance);
        }

        public void SetTransitioning(bool transitioning)
        {
            _isTransitioning = transitioning;
        }

        public bool TryAddZoom(float delta)
        {
            if (_isTransitioning) return false;

            _distance = Mathf.Clamp(_distance + delta, MinimumDistance, MaximumDistance);
            return true;
        }

        public float GetDistance()
        {
            return _distance;
        }
    }

    /// <summary>
    ///     視点モード（FPS/TPS）の保持とトグル、UIステートごとのカーソル・視点回転・クロスヘア適用を担う
    ///     Owns the FPS/TPS view mode, its toggle, and the per-UI-state cursor, look-rotation, and crosshair policy
    ///     UIステートごとの視点方針を一元管理する
    ///     Receives every state transition and update centrally from UIStateControl and applies policy only to view states
    /// </summary>
    public class PlayerViewModeController
    {
        public PlayerViewMode CurrentMode { get; private set; } = PlayerViewMode.ThirdPerson;

        private readonly IPlayerViewApplier _applier;

        private UIStateEnum _currentUIState = UIStateEnum.GameScreen;
        private bool _isTextInputFocused;

        public PlayerViewModeController(IPlayerViewApplier applier)
        {
            _applier = applier;

            // 照準プロバイダはstaticで前回プレイの値が残りうるため初期モードを明示同期する
            // The aim provider is static and can hold a value from a previous play, so sync the initial mode explicitly
            AimPointProvider.SetMode(AimPointMode.ScreenCenter);
        }

        public void SetUIState(UIStateEnum state)
        {
            _currentUIState = state;
            _isTextInputFocused = false;
            ApplyCurrentState();
        }

        public void RestoreAfterApplicationFocus()
        {
            if (!IsViewState(_currentUIState)) return;
            ApplyCurrentState();
        }

        // 視点ステート中に毎フレーム呼ぶ（Vトグルと、三人称の照準ステートでの右ドラッグ回転）
        // Called every frame during view states (V toggle, and right-drag rotation in third-person aim states)
        public void ManualUpdate(bool isTextInputFocused)
        {
            SetTextInputFocused(isTextInputFocused);
            if (!IsViewState(_currentUIState) || _isTextInputFocused) return;

            //TODO InputSystem対応
            if (HybridInput.GetKeyDown(KeyCode.V)) ToggleViewMode();

            // カーソルを解放しているのは三人称の照準ステートだけで、そこでは右ドラッグ中のみ回転する
            // Only third-person aim states free the cursor, and there the view rotates only while right-dragging
            if (CurrentMode != PlayerViewMode.ThirdPerson || !IsMouseAimState(_currentUIState)) return;

            //TODO InputSystem対応
            if (HybridInput.GetMouseButtonDown(1))
            {
                _applier.SetCursorVisible(false);
                _applier.SetCameraRotatable(true);
            }
            if (HybridInput.GetMouseButtonUp(1))
            {
                _applier.SetCursorVisible(true);
                _applier.SetCameraRotatable(false);
            }
        }

        // テキスト入力中はFPSのカーソルロック・視点回転・クロスヘアを解除し、解除時は現ステート適用へ戻す
        // While a text field is focused, release the FPS cursor lock, look rotation, and crosshair; reapply the state on unfocus
        private void SetTextInputFocused(bool focused)
        {
            if (_isTextInputFocused == focused) return;
            _isTextInputFocused = focused;
            if (CurrentMode != PlayerViewMode.FirstPerson) return;
            ApplyCurrentState();
        }

        public void ToggleViewMode()
        {
            CurrentMode = CurrentMode == PlayerViewMode.ThirdPerson ? PlayerViewMode.FirstPerson : PlayerViewMode.ThirdPerson;

            ApplyCurrentState();
        }

        private void ApplyCurrentState()
        {
            // 視点外ではFPS表示だけ解除する
            // Outside view states, retain the mode while reliably disabling FPS camera, rotation, and crosshair
            if (!IsViewState(_currentUIState))
            {
                _applier.SetFirstPersonCamera(false);
                _applier.SetCameraRotatable(false);
                _applier.SetCrosshairVisible(false);
                AimPointProvider.SetMode(AimPointMode.Mouse);
                return;
            }

            var isFirstPerson = CurrentMode == PlayerViewMode.FirstPerson;
            var isCursorFree = _isTextInputFocused || _currentUIState == UIStateEnum.BuildMenu || (!isFirstPerson && IsMouseAimState(_currentUIState));

            _applier.SetFirstPersonCamera(isFirstPerson);
            _applier.SetCameraRotatable(!isCursorFree);
            _applier.SetCursorVisible(isCursorFree);
            _applier.SetCrosshairVisible(isFirstPerson && !isCursorFree);

            // 画面中央照準はカーソルをロックしている間だけ成立する（解放中はマウス位置が照準）
            // The center aim only holds while the cursor is locked; a freed cursor aims with the mouse
            AimPointProvider.SetMode(isCursorFree ? AimPointMode.Mouse : AimPointMode.ScreenCenter);
        }

        // マウスカーソルで照準するステート（設置・破壊）かどうか
        // Whether the state aims with the mouse cursor (place, delete)
        private static bool IsMouseAimState(UIStateEnum state)
        {
            return state is UIStateEnum.PlaceBlock or UIStateEnum.DeleteBar;
        }

        private static bool IsViewState(UIStateEnum state)
        {
            return state is UIStateEnum.GameScreen or UIStateEnum.PlaceBlock or UIStateEnum.DeleteBar or UIStateEnum.BuildMenu;
        }
    }
}
