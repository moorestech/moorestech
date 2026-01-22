using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.Train;
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
        
        private readonly RailGraphClientCache _railGraphClientCache;
        
        public DeleteObjectState(DeleteBarObject deleteBarObject, InGameCameraController inGameCameraController, RailGraphClientCache cache)
        {
            _screenClickableCameraController = new ScreenClickableCameraController(inGameCameraController);
            _deleteBarObject = deleteBarObject;
            _railGraphClientCache = cache;
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
                    
                    _deleteTargetObject = deleteTarget;
                    _deleteTargetObject.SetRemovePreviewing();
                }
            }
            else if (_deleteTargetObject != null)
            {
                _deleteTargetObject.ResetMaterial();
                _deleteTargetObject = null;
            }
            
            if (_deleteTargetObject != null)
            {
                if (_deleteTargetObject.IsRemovable(out var reason))
                {
                    if (InputManager.Playable.ScreenLeftClick.GetKeyDown)
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
                            case DeleteTargetRail deleteTargetRail:
                            {
                                var carrier = deleteTargetRail.GetComponent<RailObjectIdCarrier>();
                                var railObjectId = carrier.GetRailObjectId();
                                var fromId = unchecked((int)(uint)railObjectId);
                                var toId = unchecked((int)(uint)(railObjectId >> 32));
                                
                                if (!_railGraphClientCache.TryGetNode(fromId, out var fromNode)) break;
                                if (!_railGraphClientCache.TryGetNode(toId, out var toNode)) break;
                                
                                ClientContext.VanillaApi.SendOnly.DisconnectRail(fromNode.NodeId, fromNode.NodeGuid, toNode.NodeId, toNode.NodeGuid);
                                break;
                            }
                            default:
                                throw new ArgumentOutOfRangeException(nameof(_deleteTargetObject));
                        }
                    }
                }
                else
                {
                    MouseCursorTooltip.Instance.Show(reason, isLocalize: false);
                    _isRemoveDeniedReasonShown = true;
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
