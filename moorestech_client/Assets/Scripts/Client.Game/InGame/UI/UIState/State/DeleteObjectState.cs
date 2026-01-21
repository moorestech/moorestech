using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.UI.KeyControl;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState.Input;
using Client.Game.InGame.UI.UIState.UIObject;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class DeleteObjectState : IUIState
    {
        private readonly DeleteBarObject _deleteBarObject;
        
        private readonly ScreenClickableCameraController _screenClickableCameraController;
        
        private IDeleteTarget _deleteTargetObject;
        private bool _isRemoveDeniedReasonShown;
        
        public DeleteObjectState(DeleteBarObject deleteBarObject, InGameCameraController inGameCameraController)
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
            if (_isRemoveDeniedReasonShown)
            {
                MouseCursorTooltip.Instance.Hide();
                _isRemoveDeniedReasonShown = false;
            }
            if (InputManager.UI.CloseUI.GetKeyDown || InputManager.UI.BlockDelete.GetKeyDown) return new UITransitContext(UIStateEnum.GameScreen);
            if (UnityEngine.Input.GetKeyDown(KeyCode.B)) return new UITransitContext(UIStateEnum.PlaceBlock);

            if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory);
            if (InputManager.UI.OpenMenu.GetKeyDown) return new UITransitContext(UIStateEnum.PauseMenu);
            
            if (BlockClickDetectUtil.TryGetCursorOnComponent(out IDeleteTarget deleteTarget))
            {
                if (_deleteTargetObject == null || _deleteTargetObject != deleteTarget)
                {
                    if (_deleteTargetObject != null) _deleteTargetObject.ResetMaterial();
                    
                    if (!deleteTarget.IsRemovable(out var reason))
                    {
                        MouseCursorTooltip.Instance.Show(reason, isLocalize: false);
                        _isRemoveDeniedReasonShown = true;
                    }
                    else
                    {
                        _deleteTargetObject = deleteTarget;
                        _deleteTargetObject.SetRemovePreviewing();
                    }
                }
            }
            else if (_deleteTargetObject != null)
            {
                _deleteTargetObject.ResetMaterial();
                _deleteTargetObject = null;
            }
            
            if (InputManager.Playable.ScreenLeftClick.GetKeyDown && _deleteTargetObject != null)
            {
                switch (_deleteTargetObject)
                {
                    case BlockGameObjectChild deleteTargetBlock:
                        var blockPosition = deleteTargetBlock.BlockGameObject.BlockPosInfo.OriginalPos;
                        ClientContext.VanillaApi.SendOnly.BlockRemove(blockPosition);
                        break;
                    case TrainCarEntityChildrenObject deleteTargetTrainCar:
                        ClientContext.VanillaApi.SendOnly.RemoveTrain(deleteTargetTrainCar.TrainCarEntityObject.TrainCarId);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(_deleteTargetObject));
                }
            }

            _screenClickableCameraController.GetNextUpdate();

            return null;
        }
        
        
        public void OnExit()
        {
            if (_deleteTargetObject != null) _deleteTargetObject.ResetMaterial();
            _deleteBarObject.gameObject.SetActive(false);
            
            _screenClickableCameraController.OnExit();
        }
    }
}