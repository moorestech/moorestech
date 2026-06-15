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

        private readonly RailGraphClientCache _railGraphClientCache;

        public DeleteObjectState(DeleteBarObject deleteBarObject, InGameCameraController inGameCameraController, RailGraphClientCache cache)
        {
            _screenClickableCameraController = new ScreenClickableCameraController(inGameCameraController);
            _deleteBarObject = deleteBarObject;
            _railGraphClientCache = cache;
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
                // ESCはOpenMenuとCloseUI両方にbindされ、OpenMenuを先に拾うと選択キャンセルが死ぬため破壊モードでは扱わない
                // ESC is bound to both OpenMenu and CloseUI; handling OpenMenu here would shadow the cancel path, so skip it in destroy mode
                if (InputManager.UI.BlockDelete.GetKeyDown) return new UITransitContext(UIStateEnum.GameScreen);
                if (UnityEngine.Input.GetKeyDown(KeyCode.B)) return new UITransitContext(UIStateEnum.PlaceBlock);
                if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory);
                return null;
            }

            #endregion
        }

        public void OnExit()
        {
            _deleteObjectService.Cleanup();
            _deleteBarObject.gameObject.SetActive(false);

            _screenClickableCameraController.OnExit();
        }
    }
}
