using System;
using MainGame.Control.UI.Inventory;
using MainGame.Control.UI.UIState;
using MainGame.Network.Send;
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
        
        [Inject]
        public void Construct(Camera mainCamera,GroundPlane groundPlane,
            SelectHotBarControl selectHotBarControl,SendPlaceHotBarBlockProtocol sendPlaceHotBarBlockProtocol,UIStateControl uiStateControl)
        {
            _uiStateControl = uiStateControl;
            _sendPlaceHotBarBlockProtocol = sendPlaceHotBarBlockProtocol;
            _hotBarControl = selectHotBarControl;
            _mainCamera = mainCamera;
            _groundPlane = groundPlane;
            _input = new MoorestechInputSettings();
            _input.Enable();
        }

        private void Update()
        {
            var mousePosition = _input.Playable.ClickPosition.ReadValue<Vector2>();
            var ray = _mainCamera.ScreenPointToRay(mousePosition);
            
            
            // マウスの位置が地面の時にのみ実行
            if (!Physics.Raycast(ray, out var hit)) return;
            if (hit.transform.gameObject != _groundPlane.gameObject)return;
            
            var x = Mathf.RoundToInt(hit.point.x);
            var y = Mathf.RoundToInt(hit.point.z);
            
            //クリックされてたら
            if (_input.Playable.ScreenClick.triggered)
            {
                //UIがゲームスクリーンの時にホットバーにあるブロックの設置
                if (_uiStateControl.CurrentState != UIStateEnum.GameScreen) return;
                _sendPlaceHotBarBlockProtocol.Send(x,y,(short)_hotBarControl.SelectIndex);
                
                return;
            }
            //TODO クリックされてなかったらプレビューを表示する
            
            
        }
    }
}