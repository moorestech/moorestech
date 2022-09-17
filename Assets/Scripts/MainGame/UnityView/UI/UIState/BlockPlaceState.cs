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
            return InputManager.UI.CloseUI.GetKey || InputManager.UI.BlockDelete.GetKey ||
                   InputManager.UI.OpenInventory.GetKey || 
                   InputManager.UI.OpenMenu.GetKey;;
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.UI.CloseUI.GetKey)
            {
                return UIStateEnum.GameScreen;
            }
            if (InputManager.UI.BlockDelete.GetKey)
            {
                return UIStateEnum.DeleteBar;
            }
            if (InputManager.UI.OpenInventory.GetKey)
            {
                return UIStateEnum.PlayerInventory;
            }
            if (InputManager.UI.OpenMenu.GetKey)
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