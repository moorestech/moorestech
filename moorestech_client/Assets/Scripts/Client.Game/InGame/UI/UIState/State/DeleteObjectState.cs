using Client.Game.InGame.Control.BuildView;
using Client.Game.InGame.Train.RailGraph;
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

        private readonly BuildViewModeController _buildViewModeController;
        private readonly DeleteObjectService _deleteObjectService = new();

        public DeleteObjectState(DeleteBarObject deleteBarObject, BuildViewModeController buildViewModeController, RailGraphClientCache cache)
        {
            _buildViewModeController = buildViewModeController;
            _deleteBarObject = deleteBarObject;
            deleteBarObject.gameObject.SetActive(false);
        }

        public void OnEnter(UITransitContext context)
        {
            // カメラ・カーソルの適用はBuildViewModeControllerに委譲する
            // Camera and cursor handling is delegated to BuildViewModeController
            _buildViewModeController.OnEnterBuildState(UIStateEnum.DeleteBar);
            _deleteBarObject.gameObject.SetActive(true);
            KeyControlDescription.Instance.SetText("ドラッグ: まとめて選択\n離す: まとめて削除\nV: 視点切替\nESC: 選択キャンセル\nG: 破壊モード終了\nB: 設置モード\nTab: インベントリ");
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

            _buildViewModeController.ManualUpdate();
            return null;

            #region Internal

            UITransitContext HandleTransition()
            {
                // OpenMenu(ポーズ)もESCにbindされ、ここで拾うとESCの選択キャンセル/モード終了が死ぬため破壊モードでは扱わない
                // OpenMenu(pause) is also bound to ESC; handling it here would shadow ESC's cancel/exit, so skip it in destroy mode
                if (InputManager.UI.BlockDelete.GetKeyDown) return Leave(UIStateEnum.GameScreen);
                if (HybridInput.GetKeyDown(KeyCode.B)) return Leave(UIStateEnum.BuildMenu);
                if (InputManager.UI.OpenInventory.GetKeyDown) return Leave(UIStateEnum.PlayerInventory);

                // ESCはまず削除選択のキャンセルに使い、キャンセルする選択が無ければ破壊モードを抜ける
                // ESC is used first to cancel the delete selection; with nothing to cancel it leaves destroy mode
                if (InputManager.UI.CloseUI.GetKeyDown && !_deleteObjectService.TryCancelSelection())
                {
                    return Leave(UIStateEnum.GameScreen);
                }
                return null;
            }

            #endregion
        }

        // 遷移確定をコントローラへ通知してから遷移する（セッション終了判定はコントローラ側）
        // Notify the controller before transiting; it decides whether the session ends
        private UITransitContext Leave(UIStateEnum next)
        {
            _buildViewModeController.OnLeaveBuildState(next);
            return new UITransitContext(next);
        }

        public void OnExit()
        {
            _deleteObjectService.CancelSelection();
            _deleteBarObject.gameObject.SetActive(false);
        }
    }
}
