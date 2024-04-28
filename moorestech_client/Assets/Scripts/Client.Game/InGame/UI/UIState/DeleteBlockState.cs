using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Input;

namespace Client.Game.InGame.UI.UIState
{
    public class DeleteBlockState : IUIState
    {
        private readonly DeleteBarObject _deleteBarObject;
        private BlockGameObject _removeTargetBlock;

        public DeleteBlockState(DeleteBarObject deleteBarObject)
        {
            _deleteBarObject = deleteBarObject;
            deleteBarObject.gameObject.SetActive(false);
        }

        public UIStateEnum GetNext()
        {
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.BlockDelete.GetKeyDown) return UIStateEnum.GameScreen;

            if (InputManager.UI.OpenInventory.GetKeyDown) return UIStateEnum.PlayerInventory;
            if (InputManager.UI.OpenMenu.GetKeyDown) return UIStateEnum.PauseMenu;

            if (BlockClickDetect.TryGetCursorOnBlock(out var blockGameObject))
            {
                if (_removeTargetBlock != null)
                {
                    _removeTargetBlock.ResetMaterial();
                }
                _removeTargetBlock = blockGameObject;
                _removeTargetBlock.SetRemovePreviewing();
            }
            else
            {
                if (_removeTargetBlock != null)
                {
                    _removeTargetBlock.ResetMaterial();
                    _removeTargetBlock = null;
                }
            }

            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && _removeTargetBlock != null)
            {
                var blockPosition = _removeTargetBlock.BlockPosition;
                MoorestechContext.VanillaApi.SendOnly.BlockRemove(blockPosition);
            }

            return UIStateEnum.Current;
        }

        public void OnEnter(UIStateEnum lastStateEnum)
        {
            _deleteBarObject.gameObject.SetActive(true);
        }

        public void OnExit()
        {
            if (_removeTargetBlock != null)
            {
                _removeTargetBlock.ResetMaterial();
            }
            _deleteBarObject.gameObject.SetActive(false);
        }
    }
}