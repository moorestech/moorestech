using MainGame.Control.UI.Inventory;
using MainGame.UnityView.UI.Inventory.View;

namespace MainGame.Control.UI.UIState.UIState
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
            return _input.UI.CloseUI.triggered;
        }

        public UIStateEnum GetNext()
        {
            if (_input.UI.CloseUI.triggered)
            {
                return UIStateEnum.GameScreen;
            }
            
            return UIStateEnum.BlockPlace;
        }

        public void OnEnter()
        {
            _selectHotBarView.SetActiveSelectHotBar(true);
        }

        public void OnExit()
        {
            _selectHotBarView.SetActiveSelectHotBar(false);
        }
    }
}