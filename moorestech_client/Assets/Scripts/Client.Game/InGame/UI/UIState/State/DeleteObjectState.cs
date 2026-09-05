using System.Collections.Generic;
using Mooresmaster.Localization.Generated;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.UI.UIState.State.CameraPolicy;
using Client.Game.InGame.UI.UIState.State.CancelInput;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Client.Game.InGame.UI.Tooltip;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class DeleteObjectState : IUIState, IApplicationFocusRestorer
    {
        private readonly UiStateCameraPolicyService _cameraPolicyService;
        private readonly PlacementTargetPickService _placementTargetPickService;
        private readonly RightShortPressInputService _rightShortPressInputService;

        private readonly DeleteObjectService _deleteObjectService;
        private readonly BuildUndoService _buildUndoService;

        public DeleteObjectState(RailGraphClientCache cache, UiStateCameraPolicyService cameraPolicyService, BuildOperationHistory buildOperationHistory, BuildUndoService buildUndoService, PlacementTargetPickService placementTargetPickService, RightShortPressInputService rightShortPressInputService, IMouseCursorTooltip tooltip)
        {
            _cameraPolicyService = cameraPolicyService;
            _deleteObjectService = new DeleteObjectService(buildOperationHistory, tooltip);
            _buildUndoService = buildUndoService;
            _placementTargetPickService = placementTargetPickService;
            _rightShortPressInputService = rightShortPressInputService;
        }

        public void OnEnter(UITransitContext context)
        {
            // 他UIState滞在中は右短押しがpollされないため、復帰直後の古い押下状態を破棄する
            // Right short press isn't polled while another UIState is active, so discard any stale press state on return
            _rightShortPressInputService.ResetPressState();

            // 視点別カーソル/回転ポリシーを適用
            // Apply the per-view-mode cursor/rotation policy
            _cameraPolicyService.EnterBuildMode();
        }

        public UITransitContext GetNextUpdate()
        {
            // パネル外の右短押し状態を毎フレーム取得（ManualUpdateが走る前に）
            // Evaluate right short press state every frame before early returns (ManualUpdate runs internally)
            var isRightShortPressed = _rightShortPressInputService.TryConsumeShortPressOutsideUi();

            // モード遷移を判定する（ESCはモードを抜けず削除サービス側で選択キャンセルに使う）
            // Handle mode transitions (ESC stays in the mode and is used as selection cancel by the delete service)
            var transit = HandleTransition(isRightShortPressed);
            if (transit != null) return transit;

            // TPSのみ右ドラッグで削除照準回転
            // TPS rotates the deletion aim only during right-drag
            _cameraPolicyService.UpdateRotationInput();

            // 削除インタラクションはサービスに委譲する
            // Delegate the delete interaction to the service
            _deleteObjectService.Update();

            // Ctrl+Z判定はサービス内部
            // Ctrl+Z detection lives inside the service
            _buildUndoService.ManualUpdate();

            return null;

            #region Internal

            UITransitContext HandleTransition(bool isRightShortPressed)
            {
                // OpenMenu(ポーズ)もESCにbindされ、ここで拾うとESCの選択キャンセル/モード終了が死ぬため破壊モードでは扱わない
                // OpenMenu(pause) is also bound to ESC; handling it here would shadow ESC's cancel/exit, so skip it in destroy mode
                if (InputManager.UI.BlockDelete.GetKeyDown) return new UITransitContext(UIStateEnum.GameScreen);
                if (HybridInput.GetKeyDown(KeyCode.B)) return new UITransitContext(UIStateEnum.BuildMenu);
                if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory);

                // ESC/パネル外の右短押しはまず削除選択のキャンセルに使い、キャンセルする選択が無ければ破壊モードを抜ける
                // ESC and a right short press outside UI first cancel the delete selection; with nothing to cancel they leave destroy mode
                var isCancelRequested = InputManager.UI.CloseUI.GetKeyDown || isRightShortPressed;
                if (isCancelRequested && !_deleteObjectService.TryCancelSelection())
                {
                    return new UITransitContext(UIStateEnum.GameScreen);
                }

                // ミドルクリックで設置物をスポイトし設置モードへ移る
                // Middle-click eyedrops a placed object and switches to placement mode
                if (_placementTargetPickService.TryPickTargetUnderCursor(out var pickedTarget))
                    return new UITransitContext(UIStateEnum.PlaceBlock, UITransitContextContainer.Create(new PlacementSelection(pickedTarget, PlacementOrigin.NonHotbar)));

                return null;
            }

            #endregion
        }

        public void OnExit()
        {
            _cameraPolicyService.ExitToNeutral();
            _deleteObjectService.CancelSelection();
        }

        public void RestoreAfterApplicationFocus()
        {
            _cameraPolicyService.RestoreAfterApplicationFocus();
        }

        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return DeleteObjectStateHints.Hints;
        }
    }

    internal static class DeleteObjectStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.LeftDrag, LocalizationKeys.Ui.KeyHint.Text.DragSelect),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.G, LocalizationKeys.Ui.KeyHint.Text.ExitDeleteMode),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.B, LocalizationKeys.Ui.KeyHint.Text.BuildMenu),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Inventory),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.MiddleClick, LocalizationKeys.Ui.KeyHint.Text.PickPlacedObject),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.CtrlZ, LocalizationKeys.Ui.KeyHint.Text.Undo),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.V, LocalizationKeys.Ui.KeyHint.Text.ToggleView),
        };
    }
}
