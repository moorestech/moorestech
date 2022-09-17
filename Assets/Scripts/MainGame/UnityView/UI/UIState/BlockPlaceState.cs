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
            return InputManager.Settings.UI.CloseUI.triggered || InputManager.Settings.UI.BlockDelete.triggered ||
                   InputManager.Settings.UI.OpenInventory.triggered || 
                   InputManager.Settings.UI.OpenMenu.triggered;;
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.Settings.UI.CloseUI.triggered)
            {
                return UIStateEnum.GameScreen;
            }
            if (InputManager.Settings.UI.BlockDelete.triggered)
            {
                return UIStateEnum.DeleteBar;
            }
            if (InputManager.Settings.UI.OpenInventory.triggered)
            {
                return UIStateEnum.PlayerInventory;
            }
            if (InputManager.Settings.UI.OpenMenu.triggered)
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