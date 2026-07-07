using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.Input;
using UnityEngine;

namespace Client.Game.InGame.Control.BuildView
{
    /// <summary>
    ///     建設系視点モードの記憶・トグル・カメラセッションを管理する
    ///     Owns build view mode memory, toggling, and the camera session across build states
    ///     各建設系ステートがOnEnterBuildState/OnLeaveBuildState/ManualUpdateで駆動する（UIStateControlへの依存なし）
    ///     Driven by each build state via OnEnterBuildState/OnLeaveBuildState/ManualUpdate (no UIStateControl dependency)
    /// </summary>
    public class BuildViewModeController
    {
        public BuildViewMode CurrentMode { get; private set; } = BuildViewMode.TopDown;

        private readonly IBuildViewApplier _applier;

        private TweenCameraInfo _savedCamera;
        private bool _isSessionActive;
        private UIStateEnum _currentBuildState;

        public BuildViewModeController(IBuildViewApplier applier)
        {
            _applier = applier;
        }

        // 建設系ステートのOnEnter先頭で呼ぶ
        // Call at the top of a build state's OnEnter
        public void OnEnterBuildState(UIStateEnum state)
        {
            _currentBuildState = state;

            // セッション開始時のみ復帰用に現在カメラを保存する
            // Save the current camera for restoration only when the session starts
            if (!_isSessionActive)
            {
                _savedCamera = _applier.CaptureCurrentCamera();
                _isSessionActive = true;
            }

            ApplyForState(state);
        }

        // 遷移確定時（UITransitContextを返す直前）に呼ぶ。建設系への遷移ならno-op
        // Call right before returning a UITransitContext; no-op when moving to another build state
        public void OnLeaveBuildState(UIStateEnum next)
        {
            if (!_isSessionActive || IsBuildState(next)) return;

            // FPSを解除して保存カメラへ復帰し、カーソルは非表示へ戻す（現行踏襲。遷移先のOnEnterが必要なら再表示する）
            // Leave FPS, restore the saved camera, and hide the cursor (existing behavior; the next state's OnEnter re-shows it if needed)
            if (CurrentMode == BuildViewMode.FirstPerson)
            {
                _applier.SetFirstPersonCamera(false);
                _applier.SetCrosshairVisible(false);
            }
            _applier.RestoreCamera(_savedCamera);
            _applier.SetCursorVisible(false);
            _isSessionActive = false;
        }

        // 建設系ステート中に毎フレーム呼ぶ（Vトグルと俯瞰時の右クリック回転）
        // Called every frame during build states (V toggle and top-down right-click rotation)
        public void ManualUpdate()
        {
            //TODO InputSystemのリファクタ対象
            if (UnityEngine.Input.GetKeyDown(KeyCode.V)) ToggleViewMode();

            if (CurrentMode != BuildViewMode.TopDown) return;

            //TODO InputSystemのリファクタ対象
            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                _applier.SetCursorVisible(false);
                _applier.SetCameraRotatable(true);
            }
            if (UnityEngine.Input.GetMouseButtonUp(1))
            {
                _applier.SetCursorVisible(true);
                _applier.SetCameraRotatable(false);
            }
        }

        public void ToggleViewMode()
        {
            CurrentMode = CurrentMode == BuildViewMode.TopDown ? BuildViewMode.FirstPerson : BuildViewMode.TopDown;
            AimPointProvider.SetMode(CurrentMode);

            // 俯瞰へ戻る際はFPSカメラを解除し、設置ステート以外は保存カメラへ戻す
            // When returning to top-down, leave the FPS camera; outside PlaceBlock restore the saved camera
            if (CurrentMode == BuildViewMode.TopDown)
            {
                _applier.SetFirstPersonCamera(false);
                if (_currentBuildState != UIStateEnum.PlaceBlock) _applier.RestoreCamera(_savedCamera);
            }

            ApplyForState(_currentBuildState);
        }

        private void ApplyForState(UIStateEnum state)
        {
            if (CurrentMode == BuildViewMode.FirstPerson)
            {
                // メニュー表示中はカーソルを解放しクロスヘアを消す
                // Free the cursor and hide the crosshair while the menu is open
                _applier.SetFirstPersonCamera(true);
                var isMenu = state == UIStateEnum.BuildMenu;
                _applier.SetCursorVisible(isMenu);
                _applier.SetCrosshairVisible(!isMenu);
            }
            else
            {
                _applier.SetCursorVisible(true);
                _applier.SetCrosshairVisible(false);
                if (state == UIStateEnum.PlaceBlock) _applier.ApplyTopDownCamera();
            }
        }

        private static bool IsBuildState(UIStateEnum state)
        {
            return state is UIStateEnum.BuildMenu or UIStateEnum.PlaceBlock or UIStateEnum.DeleteBar;
        }
    }
}
