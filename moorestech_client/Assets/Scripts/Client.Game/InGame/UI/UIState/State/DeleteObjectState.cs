using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.State.CameraPolicy;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Game.InGame.UI.UIState.State.PlacementPick;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class DeleteObjectState : IUIState, IApplicationFocusRestorer
    {
        private readonly DeleteBarObject _deleteBarObject;
        private readonly UiStateCameraPolicyService _cameraPolicyService;
        private readonly PlacementTargetPickService _placementTargetPickService;

        private readonly DeleteObjectService _deleteObjectService;
        private readonly BuildUndoService _buildUndoService;

        public DeleteObjectState(DeleteBarObject deleteBarObject, RailGraphClientCache cache, UiStateCameraPolicyService cameraPolicyService, BuildOperationHistory buildOperationHistory, BuildUndoService buildUndoService, PlacementTargetPickService placementTargetPickService)
        {
            _deleteBarObject = deleteBarObject;
            _cameraPolicyService = cameraPolicyService;
            _deleteObjectService = new DeleteObjectService(buildOperationHistory);
            _buildUndoService = buildUndoService;
            _placementTargetPickService = placementTargetPickService;
            deleteBarObject.gameObject.SetActive(false);
        }

        public void OnEnter(UITransitContext context)
        {
            // 視点別カーソル/回転ポリシーを適用
            // Apply the per-view-mode cursor/rotation policy
            _cameraPolicyService.EnterBuildMode();

            _deleteBarObject.gameObject.SetActive(!WebUiScreenGate.IsWebUiMode);
            KeyControlDescription.Instance.SetText("ドラッグ: まとめて選択\n離す: まとめて削除\nV: 視点切替\nESC: 選択キャンセル\nG: 破壊モード終了\nB: 設置モード\nミドルクリック: 設置物をスポイト\nTab: インベントリ\nCtrl+Z: 元に戻す");
        }

        public UITransitContext GetNextUpdate()
        {
            // モード遷移を判定する（ESCはモードを抜けず削除サービス側で選択キャンセルに使う）
            // Handle mode transitions (ESC stays in the mode and is used as selection cancel by the delete service)
            var transit = HandleTransition();
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

            UITransitContext HandleTransition()
            {
                // OpenMenu(ポーズ)もESCにbindされ、ここで拾うとESCの選択キャンセル/モード終了が死ぬため破壊モードでは扱わない
                // OpenMenu(pause) is also bound to ESC; handling it here would shadow ESC's cancel/exit, so skip it in destroy mode
                if (InputManager.UI.BlockDelete.GetKeyDown) return new UITransitContext(UIStateEnum.GameScreen);
                if (HybridInput.GetKeyDown(KeyCode.B)) return new UITransitContext(UIStateEnum.BuildMenu);
                if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory);

                // ESCはまず削除選択のキャンセルに使い、キャンセルする選択が無ければ破壊モードを抜ける
                // ESC is used first to cancel the delete selection; with nothing to cancel it leaves destroy mode
                if (InputManager.UI.CloseUI.GetKeyDown && !_deleteObjectService.TryCancelSelection())
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
            _deleteBarObject.gameObject.SetActive(false);
        }

        public void RestoreAfterApplicationFocus()
        {
            _cameraPolicyService.RestoreAfterApplicationFocus();
        }
    }
}
