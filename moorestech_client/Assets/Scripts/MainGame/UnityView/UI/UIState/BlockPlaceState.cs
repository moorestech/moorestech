using MainGame.UnityView.Block;
using MainGame.UnityView.Control;
using MainGame.UnityView.Control.MouseKeyboard;
using MainGame.UnityView.UI.Inventory.View.HotBar;

namespace MainGame.UnityView.UI.UIState
{
    public class BlockPlaceState : IUIState
    {
        private readonly IBlockClickDetect _blockClickDetect;
        private readonly SelectHotBarView _selectHotBarView;

        public BlockPlaceState(SelectHotBarView selectHotBarView, IBlockClickDetect blockClickDetect)
        {
            _selectHotBarView = selectHotBarView;
            _blockClickDetect = blockClickDetect;
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.UI.CloseUI.GetKeyDown) return UIStateEnum.GameScreen;
            if (InputManager.UI.BlockDelete.GetKeyDown) return UIStateEnum.DeleteBar;
            if (InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.PlayerInventory;
            if (InputManager.UI.OpenMenu.GetKeyDown) return UIStateEnum.PauseMenu;
            if (IsClickOpenableBlock()) return UIStateEnum.BlockInventory;

            return UIStateEnum.Current;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            _selectHotBarView.SetActiveSelectHotBar(true);
        }

        public void OnExit()
        {
            _selectHotBarView.SetActiveSelectHotBar(false);
        }

        private bool IsClickOpenableBlock()
        {
            if (_blockClickDetect.TryGetClickBlock(out var block)) return block.GetComponent<OpenableInventoryBlock>();

            return false;
        }
    }
}