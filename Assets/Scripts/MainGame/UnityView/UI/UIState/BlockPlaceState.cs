using MainGame.UnityView.Control;
using MainGame.UnityView.UI.Inventory.View.HotBar;

namespace MainGame.UnityView.UI.UIState
{
    public class BlockPlaceState : IUIState
    {
        private readonly SelectHotBarView _selectHotBarView;

        public BlockPlaceState(SelectHotBarView selectHotBarView)
        {
            _selectHotBarView = selectHotBarView;
        }
        
        public bool IsNext()
        {
            return InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.BlockDelete.GetKeyDown ||
                   InputManager.UI.OpenInventory.GetKeyDown || 
                   InputManager.UI.OpenMenu.GetKeyDown;;
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.UI.CloseUI.GetKeyDown)
            {
                return UIStateEnum.GameScreen;
            }
            if (InputManager.UI.BlockDelete.GetKeyDown)
            {
                return UIStateEnum.DeleteBar;
            }
            if (InputManager.UI.OpenInventory.GetKeyDown)
            {
                return UIStateEnum.PlayerInventory;
            }
            if (InputManager.UI.OpenMenu.GetKeyDown)
            {
                return UIStateEnum.PauseMenu;
            }
            
            return UIStateEnum.BlockPlace;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            _selectHotBarView.SetActiveSelectHotBar(true);
        }

        public void OnExit()
        {
            _selectHotBarView.SetActiveSelectHotBar(false);
        }
    }
}