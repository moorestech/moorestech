using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.UIState.Input;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState
{
    public class DeleteBlockState : IUIState
    {
        private readonly DeleteBarObject _deleteBarObject;
        
        private readonly ScreenClickableCameraController _screenClickableCameraController;
        
        private BlockGameObject _removeTargetBlock;
        
        public DeleteBlockState(DeleteBarObject deleteBarObject, InGameCameraController inGameCameraController)
        {
            _screenClickableCameraController = new ScreenClickableCameraController(inGameCameraController);
            _deleteBarObject = deleteBarObject;
            deleteBarObject.gameObject.SetActive(false);
        }
        
        public void OnEnter(UITransitContext context)
        {
            _screenClickableCameraController.OnEnter(false);
            _deleteBarObject.gameObject.SetActive(true);
            KeyControlDescription.Instance.SetText("左クリック: ブロックを削除\nECS: 破壊モード終了\nB: 設置モード\nTab: インベントリ");
        }

        public UITransitContext GetNextUpdate()
        {
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.BlockDelete.GetKeyDown)
                return new UITransitContext(UIStateEnum.GameScreen);
            if (UnityEngine.Input.GetKeyDown(KeyCode.B))
                return new UITransitContext(UIStateEnum.PlaceBlock);

            if (InputManager.UI.OpenInventory.GetKeyDown)
                return new UITransitContext(UIStateEnum.PlayerInventory);
            if (InputManager.UI.OpenMenu.GetKeyDown)
                return new UITransitContext(UIStateEnum.PauseMenu);

            if (BlockClickDetect.TryGetCursorOnBlock(out var blockGameObject))
            {
                if (_removeTargetBlock == null || _removeTargetBlock != blockGameObject)
                {
                    if (_removeTargetBlock != null) _removeTargetBlock.ResetMaterial();

                    _removeTargetBlock = blockGameObject;
                    _removeTargetBlock.SetRemovePreviewing();
                }
            }
            else if (_removeTargetBlock != null)
            {
                _removeTargetBlock.ResetMaterial();
                _removeTargetBlock = null;
            }

            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && _removeTargetBlock != null)
            {
                var blockPosition = _removeTargetBlock.BlockPosInfo.OriginalPos;
                ClientContext.VanillaApi.SendOnly.BlockRemove(blockPosition);
            }

            _screenClickableCameraController.GetNextUpdate();

            return new UITransitContext(UIStateEnum.Current);
        }
        
        
        public void OnExit()
        {
            if (_removeTargetBlock != null) _removeTargetBlock.ResetMaterial();
            _deleteBarObject.gameObject.SetActive(false);
            
            _screenClickableCameraController.OnExit();
        }
    }
}