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
            InputManager.MouseCursorVisible(true);
        }

        public UITransitContext GetNextUpdate()
        {
            // ESCまたはR入力でゲーム画面へ戻る
            // Return to game screen when ESC or R is pressed
            if (InputManager.UI.CloseUI.GetKeyDown || UnityEngine.Input.GetKeyDown(KeyCode.R)) return new UITransitContext(UIStateEnum.GameScreen);

            return null;
        }

        public void OnExit()
        {
            // リサーチUIを閉じてカーソルを隠す
            // Hide research UI and the cursor
            _researchTreeViewManager.SetActive(false);
            InputManager.MouseCursorVisible(false);
        }
    }
}
