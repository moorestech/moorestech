using System;
using MainGame.Basic;
using MainGame.Control.UI.Inventory;
using MainGame.Control.UI.UIState;
using MainGame.Network.Send;
using MainGame.UnityView.Block;
using MainGame.UnityView.Chunk;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace MainGame.Control.Game.MouseKeyboard
{
    /// <summary>
    /// マウスで地面をクリックしたときに発生するイベント
    /// </summary>
    public class MouseGroundClickInput : MonoBehaviour
    {
        private Camera _mainCamera;
        private GroundPlane _groundPlane;
        private MoorestechInputSettings _input;
        private SelectHotBarControl _hotBarControl;
        private SendPlaceHotBarBlockProtocol _sendPlaceHotBarBlockProtocol;
        private UIStateControl _uiStateControl;
        private IBlockPlacePreview _blockPlacePreview;
        
        
        private BlockDirection _currentBlockDirection;
        
        [Inject]
        public void Construct(Camera mainCamera,GroundPlane groundPlane,
            SelectHotBarControl selectHotBarControl,SendPlaceHotBarBlockProtocol sendPlaceHotBarBlockProtocol,UIStateControl uiStateControl,IBlockPlacePreview blockPlacePreview)
        {
            _uiStateControl = uiStateControl;
            _sendPlaceHotBarBlockProtocol = sendPlaceHotBarBlockProtocol;
            _hotBarControl = selectHotBarControl;
            _mainCamera = mainCamera;
            _groundPlane = groundPlane;
            _blockPlacePreview = blockPlacePreview;
            
            _input = new MoorestechInputSettings();
            _input.Enable();
        }

        private void Update()
        {
            BlockDirectionControl();
            GroundClickControl();
        }

        private void BlockDirectionControl()
        {
            //TODO 
        }
        
        
        private void GroundClickControl()
        {
            //基本はプレビュー非表示
            _blockPlacePreview.SetActive(false);
            
            
            var (isHit, hitPoint) = GetPreviewPosition();
            if (!isHit)
            { 
                return;   
            }

            //プレビュー表示
            _blockPlacePreview.SetActive(false);
            
            //クリックされてたらUIがゲームスクリーンの時にホットバーにあるブロックの設置
            if (_input.Playable.ScreenClick.triggered)
            {
                _sendPlaceHotBarBlockProtocol.Send(hitPoint.x,hitPoint.y,(short)_hotBarControl.SelectIndex);
                return;
            }
            
            
            //クリックされてなかったらプレビューを表示する
            _blockPlacePreview.SetPreview(hitPoint,_currentBlockDirection);
        }
        
        
        private (bool,Vector2Int pos) GetPreviewPosition()
        {
            var mousePosition = _input.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);
            
            //画面からのrayが何かにヒットしているか
            if (!Physics.Raycast(ray, out var hit)) return (false,new Vector2Int());
            //そのrayが地面のオブジェクトにヒットしてるか
            if (hit.transform.gameObject != _groundPlane.gameObject) return (false,new Vector2Int());
            //UIの状態がゲーム中か
            if (_uiStateControl.CurrentState != UIStateEnum.GameScreen) return (false,new Vector2Int());
            
            
            var x = Mathf.RoundToInt(hit.point.x);
            var y = Mathf.RoundToInt(hit.point.z);

            return (true ,new Vector2Int(x,y));
        }
    }
}