using Client.Game.InGame.UI.Inventory.Block.Research;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    /// <summary>
    /// 研究ツリーUIを制御するステート
    /// UI state that controls the research tree view
    /// </summary>
    public class ResearchTreeState : IUIState
    {
        private readonly ResearchTreeViewManager _researchTreeViewManager;

        public ResearchTreeState(ResearchTreeViewManager researchTreeViewManager)
        {
            _researchTreeViewManager = researchTreeViewManager;
        }

        public void OnEnter(UITransitContext context)
        {
            // リサーチUIの表示とカーソル制御
            // Show research UI and update cursor
            _researchTreeViewManager.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public UITransitContext GetNextUpdate()
        {
            // ESC入力でゲーム画面へ戻る
            // Return to the game screen when ESC is pressed
            if (InputManager.UI.CloseUI.GetKeyDown) return new UITransitContext(UIStateEnum.GameScreen);

            return null;
        }

        public void OnExit()
        {
            // リサーチUIを閉じてカーソルを隠す
            // Hide research UI and the cursor
            _researchTreeViewManager.SetActive(false);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}
