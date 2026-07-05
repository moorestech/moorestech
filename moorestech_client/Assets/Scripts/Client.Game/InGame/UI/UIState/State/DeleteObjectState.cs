using Client.Game.InGame.Control;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.Input;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class DeleteObjectState : IUIState
    {
        private readonly DeleteBarObject _deleteBarObject;

        private readonly ScreenClickableCameraController _screenClickableCameraController;
        private readonly DeleteObjectService _deleteObjectService = new();

        public DeleteObjectState(DeleteBarObject deleteBarObject, InGameCameraController inGameCameraController, RailGraphClientCache cache)
        {
            _screenClickableCameraController = new ScreenClickableCameraController(inGameCameraController);
            _deleteBarObject = deleteBarObject;
            deleteBarObject.gameObject.SetActive(false);
        }

        public void OnEnter(UITransitContext context)
        {
            _screenClickableCameraController.OnEnter(false);
            _deleteBarObject.gameObject.SetActive(true);
            KeyControlDescription.Instance.SetText("ドラッグ: まとめて選択\n離す: まとめて削除\nESC: 選択キャンセル\nG: 破壊モード終了\nB: 設置モード\nTab: インベントリ");
        }

        public UITransitContext GetNextUpdate()
        {
            // モード遷移を判定する（ESCはモードを抜けず削除サービス側で選択キャンセルに使う）
            // Handle mode transitions (ESC stays in the mode and is used as selection cancel by the delete service)
            var transit = HandleTransition();
            if (transit != null) return transit;

            // 削除インタラクションはサービスに委譲する
            // Delegate the delete interaction to the service
            _deleteObjectService.Update();

            _screenClickableCameraController.GetNextUpdate();
            return null;

            #region Internal

            UITransitContext HandleTransition()
            {
                // OpenMenu(ポーズ)もESCにbindされ、ここで拾うとESCの選択キャンセル/モード終了が死ぬため破壊モードでは扱わない
                // OpenMenu(pause) is also bound to ESC; handling it here would shadow ESC's cancel/exit, so skip it in destroy mode
                if (InputManager.UI.BlockDelete.GetKeyDown) return new UITransitContext(UIStateEnum.GameScreen);
                if (UnityEngine.Input.GetKeyDown(KeyCode.B)) return new UITransitContext(UIStateEnum.BuildMenu);
                if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory);

                // ESCはまず削除選択のキャンセルに使い、キャンセルする選択が無ければ破壊モードを抜ける
                // ESC is used first to cancel the delete selection; with nothing to cancel it leaves destroy mode
                if (InputManager.UI.CloseUI.GetKeyDown && !_deleteObjectService.TryCancelSelection())
                {
                    return new UITransitContext(UIStateEnum.GameScreen);
                }
                return null;
            }

            #endregion
        }

        public void OnExit()
        {
            _deleteObjectService.CancelSelection();
            _deleteBarObject.gameObject.SetActive(false);

            _screenClickableCameraController.OnExit();
        }
    }
}
