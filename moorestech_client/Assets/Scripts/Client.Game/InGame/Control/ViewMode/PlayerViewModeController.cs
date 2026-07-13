using Client.Game.InGame.UI.UIState;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.Control.ViewMode
{
    /// <summary>
    ///     視点モード（FPS/TPS）の保持とトグル、UIステートごとのカーソル・視点回転・クロスヘア適用を担う
    ///     Owns the FPS/TPS view mode, its toggle, and the per-UI-state cursor, look-rotation, and crosshair policy
    ///     視点を扱うステートがOnEnterViewState/OnExitViewState/ManualUpdateで駆動する（UIStateControlへの依存なし）
    ///     Driven by view-aware states via OnEnterViewState/OnExitViewState/ManualUpdate (no UIStateControl dependency)
    /// </summary>
    public class PlayerViewModeController
    {
        public PlayerViewMode CurrentMode { get; private set; } = PlayerViewMode.ThirdPerson;

        private readonly IPlayerViewApplier _applier;

        private UIStateEnum _currentViewState = UIStateEnum.GameScreen;

        public PlayerViewModeController(IPlayerViewApplier applier)
        {
            _applier = applier;

            // 照準プロバイダはstaticで前回プレイの値が残りうるため初期モードを明示同期する
            // The aim provider is static and can hold a value from a previous play, so sync the initial mode explicitly
            AimPointProvider.SetMode(CurrentMode);
        }

        // 視点を扱うステート（ゲーム画面・設置・破壊・ビルドメニュー）のOnEnterで呼ぶ
        // Call from OnEnter of the view-aware states (game screen, place, delete, build menu)
        public void OnEnterViewState(UIStateEnum state)
        {
            _currentViewState = state;
            ApplyForState(state);
        }

        // 同じステートのOnExitで呼ぶ。カーソルは次ステートのOnEnterが自前の方針で適用する
        // Call from the same state's OnExit; the next state's OnEnter applies its own cursor policy
        public void OnExitViewState()
        {
            _applier.SetCameraRotatable(false);
            _applier.SetCrosshairVisible(false);

            // 視点管理外のステート（インベントリ・デバッグ等）はカーソルを解放するため照準もマウスへ戻す
            // States outside the view management (inventory, debug, ...) free the cursor, so the aim returns to the mouse
            AimPointProvider.SetMode(PlayerViewMode.ThirdPerson);
        }

        // 視点ステート中に毎フレーム呼ぶ（Vトグルと、三人称の照準ステートでの右ドラッグ回転）
        // Called every frame during view states (V toggle, and right-drag rotation in third-person aim states)
        public void ManualUpdate()
        {
            //TODO InputSystem対応
            if (HybridInput.GetKeyDown(KeyCode.V)) ToggleViewMode();

            // カーソルを解放しているのは三人称の照準ステートだけで、そこでは右ドラッグ中のみ回転する
            // Only third-person aim states free the cursor, and there the view rotates only while right-dragging
            if (CurrentMode != PlayerViewMode.ThirdPerson || !IsMouseAimState(_currentViewState)) return;

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
        public void SetTextInputFocused(bool focused)
        {
            if (CurrentMode != PlayerViewMode.FirstPerson) return;

            if (focused)
            {
                _applier.SetCameraRotatable(false);
                _applier.SetCursorVisible(true);
                _applier.SetCrosshairVisible(false);
                AimPointProvider.SetMode(PlayerViewMode.ThirdPerson);
            }
            else
            {
                ApplyForState(_currentViewState);
            }
        }

        public void ToggleViewMode()
        {
            CurrentMode = CurrentMode == PlayerViewMode.ThirdPerson ? PlayerViewMode.FirstPerson : PlayerViewMode.ThirdPerson;

            ApplyForState(_currentViewState);
        }

        private void ApplyForState(UIStateEnum state)
        {
            // カーソルを解放するのはビルドメニューと、三人称でマウス照準するステートのみ
            // The cursor is freed only in the build menu and in third-person mouse-aim states
            var isFirstPerson = CurrentMode == PlayerViewMode.FirstPerson;
            var isCursorFree = state == UIStateEnum.BuildMenu || (!isFirstPerson && IsMouseAimState(state));

            _applier.SetFirstPersonCamera(isFirstPerson);
            _applier.SetCameraRotatable(!isCursorFree);
            _applier.SetCursorVisible(isCursorFree);
            _applier.SetCrosshairVisible(isFirstPerson && !isCursorFree);

            // 画面中央照準はカーソルをロックしている間だけ成立する（解放中はマウス位置が照準）
            // The center aim only holds while the cursor is locked; a freed cursor aims with the mouse
            AimPointProvider.SetMode(isFirstPerson && !isCursorFree ? PlayerViewMode.FirstPerson : PlayerViewMode.ThirdPerson);
        }

        // マウスカーソルで照準するステート（設置・破壊）かどうか
        // Whether the state aims with the mouse cursor (place, delete)
        private static bool IsMouseAimState(UIStateEnum state)
        {
            return state is UIStateEnum.PlaceBlock or UIStateEnum.DeleteBar;
        }
    }
}
