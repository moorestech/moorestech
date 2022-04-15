using MainGame.Control.UI.UIState;
using MainGame.Control.UI.UIState.UIState;
using MainGame.UnityView.UI.Inventory.View;
using MainGame.UnityView.UI.Inventory.View.HotBar;

namespace MainGame.UnityView.UI.UIState
{
    public class BlockPlaceState : IUIState
    {
        private readonly SelectHotBarView _selectHotBarView;
        private readonly MoorestechInputSettings _input;

        public BlockPlaceState(SelectHotBarView selectHotBarView,MoorestechInputSettings input)
        {
            _selectHotBarView = selectHotBarView;
            _input = input;
        }
        
        public bool IsNext()
        {
            return _input.UI.CloseUI.triggered || _input.UI.BlockDelete.triggered ||
                   _input.UI.OpenInventory.triggered || 
                   _input.UI.OpenMenu.triggered;;
        }

        public UIStateEnum GetNext()
        {
            if (_input.UI.CloseUI.triggered)
            {
                return UIStateEnum.GameScreen;
            }
            if (_input.UI.BlockDelete.triggered)
            {
                return UIStateEnum.DeleteBar;
            }
            if (_input.UI.OpenInventory.triggered)
            {
                return UIStateEnum.PlayerInventory;
            }
            if (_input.UI.OpenMenu.triggered)
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