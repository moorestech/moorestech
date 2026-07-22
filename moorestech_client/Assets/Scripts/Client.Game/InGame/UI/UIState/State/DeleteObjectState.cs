using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Input;
using UnityEngine;
using System;

namespace Client.Game.InGame.UI.UIState.State
{
    public class DeleteObjectState : IUIState, IApplicationFocusRestorer
    {
        private readonly DeleteBarObject _deleteBarObject;
        private readonly IPlayerCameraInteractionApplier _cameraInteractionApplier;

        private readonly DeleteObjectService _deleteObjectService;
        private readonly BuildUndoService _buildUndoService;

        public IObservable<string> OnUnavailableReasonChanged => _deleteObjectService.OnUnavailableReasonChanged;
        public string GetUnavailableReason() => _deleteObjectService.GetUnavailableReason();

        public DeleteObjectState(DeleteBarObject deleteBarObject, RailGraphClientCache cache, IPlayerCameraInteractionApplier cameraInteractionApplier, BuildOperationHistory buildOperationHistory, BuildUndoService buildUndoService)
        {
            _deleteBarObject = deleteBarObject;
            _cameraInteractionApplier = cameraInteractionApplier;
            _deleteObjectService = new DeleteObjectService(buildOperationHistory);
            _buildUndoService = buildUndoService;
            deleteBarObject.gameObject.SetActive(false);
        }

        public void OnEnter(UITransitContext context)
        {
            // 削除中は右ドラッグまで回転停止
            // Stop rotation until right-drag while deleting
            _cameraInteractionApplier.SetCursorVisible(true);
            _cameraInteractionApplier.SetCameraRotatable(false);

            _deleteBarObject.gameObject.SetActive(!WebUiScreenGate.IsWebUiMode);
            KeyControlDescription.Instance.SetText("ドラッグ: まとめて選択\n離す: まとめて削除\nV: 視点切替\nESC: 選択キャンセル\nG: 破壊モード終了\nB: 設置モード\nTab: インベントリ\nCtrl+Z: 元に戻す");
        }

        public UITransitContext GetNextUpdate()
        {
            // モード遷移を判定する（ESCはモードを抜けず削除サービス側で選択キャンセルに使う）
            // Handle mode transitions (ESC stays in the mode and is used as selection cancel by the delete service)
            var transit = HandleTransition();
            if (transit != null) return transit;

            // 右ドラッグ中のみ削除照準回転
            // Rotate deletion aim only during right-drag
            UpdateRightDragRotation();

            // 削除インタラクションはサービスに委譲する
            // Delegate the delete interaction to the service
            _deleteObjectService.Update();

            // Ctrl+Zで直前の建築操作を取り消す（判定はサービス内部）
            // Undo the latest build operation on Ctrl+Z (detection lives inside the service)
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
                return null;
            }

            void UpdateRightDragRotation()
            {
                if (HybridInput.GetMouseButtonDown(1))
                {
                    _cameraInteractionApplier.SetCursorVisible(false);
                    _cameraInteractionApplier.SetCameraRotatable(true);
                }

                if (!HybridInput.GetMouseButtonUp(1)) return;
                _cameraInteractionApplier.SetCursorVisible(true);
                _cameraInteractionApplier.SetCameraRotatable(false);
            }

            #endregion
        }

        public void OnExit()
        {
            _cameraInteractionApplier.SetCursorVisible(true);
            _cameraInteractionApplier.SetCameraRotatable(false);
            _deleteObjectService.CancelSelection();
            _deleteBarObject.gameObject.SetActive(false);
        }

        public void RestoreAfterApplicationFocus()
        {
            _cameraInteractionApplier.SetCursorVisible(true);
            _cameraInteractionApplier.SetCameraRotatable(false);
        }
    }
}
