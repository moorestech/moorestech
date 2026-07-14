using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class DeleteObjectState : IUIState
    {
        private readonly DeleteBarObject _deleteBarObject;
        private readonly IPlayerCameraInteractionApplier _cameraInteractionApplier;

        private readonly DeleteObjectService _deleteObjectService = new();

        public DeleteObjectState(DeleteBarObject deleteBarObject, RailGraphClientCache cache, IPlayerCameraInteractionApplier cameraInteractionApplier)
        {
            _deleteBarObject = deleteBarObject;
            _cameraInteractionApplier = cameraInteractionApplier;
            deleteBarObject.gameObject.SetActive(false);
        }

        public void OnEnter(UITransitContext context)
        {
            // 削除操作ではカーソルを解放し、右ドラッグ開始まで回転を止める
            // Deletion releases the cursor and stops rotation until right-drag begins
            _cameraInteractionApplier.SetCursorVisible(true);
            _cameraInteractionApplier.SetCameraRotatable(false);

            _deleteBarObject.gameObject.SetActive(true);
            KeyControlDescription.Instance.SetText("ドラッグ: まとめて選択\n離す: まとめて削除\nV: 視点切替\nESC: 選択キャンセル\nG: 破壊モード終了\nB: 設置モード\nTab: インベントリ");
        }

        public UITransitContext GetNextUpdate()
        {
            // モード遷移を判定する（ESCはモードを抜けず削除サービス側で選択キャンセルに使う）
            // Handle mode transitions (ESC stays in the mode and is used as selection cancel by the delete service)
            var transit = HandleTransition();
            if (transit != null) return transit;

            // 右ドラッグ中だけ削除照準を維持したまま視点回転を有効にする
            // Enable look rotation only during right-drag while preserving deletion aiming
            UpdateRightDragRotation();

            // 削除インタラクションはサービスに委譲する
            // Delegate the delete interaction to the service
            _deleteObjectService.Update();

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
            _cameraInteractionApplier.SetCameraRotatable(false);
            _deleteObjectService.CancelSelection();
            _deleteBarObject.gameObject.SetActive(false);
        }
    }
}
